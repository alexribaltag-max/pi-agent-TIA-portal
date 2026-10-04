using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace TiaLocalBridge.Services
{
    internal sealed class DeviceInventoryQuery
    {
        private static readonly string[] AllowedFields = { "reference", "legacyReference", "name", "typeIdentifier", "containerKind", "groupPath", "capabilities" };
        public string ProjectName;
        public string Scope = "all";
        public int Limit = 100;
        public string Cursor;
        public bool Capabilities;
        public string[] Fields = { "reference", "legacyReference", "name", "typeIdentifier", "containerKind", "groupPath" };

        public static DeviceInventoryQuery Parse(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                throw new ArgumentException("GETDEVICEINVENTORY requires a project name followed by optional key=value arguments.");
            var query = new DeviceInventoryQuery { ProjectName = args[0].Trim() };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in args.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(argument)) throw new ArgumentException("Empty inventory option; positional blanks are not dropped.");
                var separator = argument.IndexOf('=');
                if (separator <= 0) throw new ArgumentException("Inventory options must use key=value.");
                var key = argument.Substring(0, separator).Trim();
                var value = argument.Substring(separator + 1).Trim();
                if (!seen.Add(key) || value.Length == 0) throw new ArgumentException("Duplicate or empty inventory option: " + key);
                switch (key)
                {
                    case "scope":
                        if (!new[] { "all", "root", "ungrouped", "groups" }.Contains(value)) throw new ArgumentException("Invalid scope.");
                        query.Scope = value; break;
                    case "limit":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out query.Limit) || query.Limit < 1 || query.Limit > 500)
                            throw new ArgumentException("limit must be an integer between 1 and 500.");
                        break;
                    case "cursor":
                        if (value.Length > 256) throw new ArgumentException("Cursor too long.");
                        query.Cursor = value; break;
                    case "capabilities":
                        if (value != "true" && value != "false") throw new ArgumentException("capabilities must be true or false.");
                        query.Capabilities = value == "true"; break;
                    case "fields":
                        query.Fields = value.Split(',').Select(f => f.Trim()).ToArray();
                        if (query.Fields.Distinct().Count() != query.Fields.Length || query.Fields.Any(f => !AllowedFields.Contains(f)))
                            throw new ArgumentException("Invalid or duplicate field.");
                        break;
                    default: throw new ArgumentException("Unknown inventory option: " + DeviceInventory.Bounded(key, 80));
                }
            }
            if (query.Fields.Contains("capabilities") && !query.Capabilities)
                throw new ArgumentException("fields=capabilities requires capabilities=true.");
            if (query.Capabilities && !seen.Contains("fields")) query.Fields = query.Fields.Concat(new[] { "capabilities" }).ToArray();
            query.Fields = query.Fields.Concat(new[] { "reference" }).Distinct().OrderBy(f => f, StringComparer.Ordinal).ToArray();
            return query;
        }
    }

    [DataContract]
    internal sealed class InventoryRow
    {
        [DataMember(Name = "reference", Order = 0)] public string Reference;
        [DataMember(Name = "legacyReference", EmitDefaultValue = false, Order = 1)] public string LegacyReference;
        [DataMember(Name = "name", EmitDefaultValue = false, Order = 2)] public string Name;
        [DataMember(Name = "typeIdentifier", EmitDefaultValue = false, Order = 3)] public string TypeIdentifier;
        [DataMember(Name = "containerKind", EmitDefaultValue = false, Order = 4)] public string ContainerKind;
        [DataMember(Name = "groupPath", EmitDefaultValue = false, Order = 5)] public string[] GroupPath;
        [DataMember(Name = "capabilities", EmitDefaultValue = false, Order = 6)] public string[] Capabilities;
    }

    [DataContract]
    internal sealed class InventoryPage
    {
        [DataMember(Name = "schemaVersion", Order = 0)] public int SchemaVersion = 1;
        [DataMember(Name = "project", Order = 1)] public string Project;
        [DataMember(Name = "complete", Order = 2)] public bool Complete;
        [DataMember(Name = "warnings", Order = 3)] public string[] Warnings;
        [DataMember(Name = "warningCount", Order = 4)] public int WarningCount;
        [DataMember(Name = "identityPolicy", Order = 5)] public string IdentityPolicy = "same-handle-only; wrapper equality unverified";
        [DataMember(Name = "pagination", Order = 6)] public string Pagination = "fresh-inventory-fingerprint; not an immutable engineering snapshot";
        [DataMember(Name = "total", Order = 7)] public int Total;
        [DataMember(Name = "devices", Order = 8)] public List<InventoryRow> Devices = new List<InventoryRow>();
        [DataMember(Name = "hasMore", Order = 9)] public bool HasMore;
        [DataMember(Name = "nextCursor", Order = 10)] public string NextCursor;
    }

    internal static class DeviceInventoryPaging
    {
        private static readonly byte[] CursorKey = CreateKey();
        private static byte[] CreateKey()
        {
            var key = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(key);
            return key;
        }

        public static InventoryPage Read(DeviceInventory inventory, DeviceInventoryQuery query, Func<InventoryDevice, string[]> capabilities = null)
        {
            var filtered = inventory.Devices.Where(d => query.Scope == "all" ||
                (query.Scope == "groups" ? d.Identity.ContainerKind == "userGroup" : d.Identity.ContainerKind == query.Scope)).ToList();
            string fingerprint;
            using (var hash = SHA256.Create())
            {
                var parts = new[] { inventory.ProjectName, query.Scope, string.Join(",", query.Fields), query.Capabilities.ToString(), inventory.WarningCount.ToString(CultureInfo.InvariantCulture) }
                    .Concat(inventory.Warnings).Concat(inventory.Devices.Select(d => InventoryJson.Serialize(new[] { d.CanonicalReference, d.LegacyReference, d.TypeIdentifier })));
                fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(InventoryJson.Serialize(parts.ToArray())))).Replace("-", "").ToLowerInvariant();
            }
            int start = query.Cursor == null ? 0 : ParseCursor(query.Cursor, fingerprint);
            if (start < 0 || start >= filtered.Count && start != 0) throw new ArgumentException("Inventory cursor is out of range.");
            var page = new InventoryPage { Project = inventory.ProjectName, Complete = inventory.Complete, Warnings = inventory.Warnings.ToArray(), WarningCount = inventory.WarningCount, Total = filtered.Count };
            for (int index = start; index < filtered.Count && page.Devices.Count < query.Limit; index++)
            {
                var device = filtered[index];
                var row = new InventoryRow { Reference = device.CanonicalReference };
                foreach (var field in query.Fields)
                {
                    switch (field)
                    {
                        case "legacyReference": row.LegacyReference = device.LegacyReference; break;
                        case "name": row.Name = device.Identity.Name; break;
                        case "typeIdentifier": row.TypeIdentifier = device.TypeIdentifier ?? ""; break;
                        case "containerKind": row.ContainerKind = device.Identity.ContainerKind; break;
                        case "groupPath": row.GroupPath = device.Identity.GroupPath; break;
                        case "capabilities":
                            if (capabilities == null) throw new InvalidOperationException("Capability provider unavailable.");
                            row.Capabilities = capabilities(device); break;
                    }
                }
                page.Devices.Add(row);
                SetContinuation(page, index + 1, fingerprint);
                if (Encoding.UTF8.GetByteCount(InventoryJson.Serialize(page)) > 16384)
                {
                    page.Devices.RemoveAt(page.Devices.Count - 1);
                    if (page.Devices.Count == 0) throw new InvalidOperationException("One device plus warnings exceeds the 16 KiB inventory result budget. Request fewer fields.");
                    break;
                }
            }
            SetContinuation(page, start + page.Devices.Count, fingerprint);
            if (Encoding.UTF8.GetByteCount(InventoryJson.Serialize(page)) > 16384)
                throw new InvalidOperationException("Inventory metadata exceeds the result budget.");
            return page;
        }

        private static void SetContinuation(InventoryPage page, int next, string fingerprint)
        {
            page.HasMore = next < page.Total;
            var body = "1." + next.ToString(CultureInfo.InvariantCulture) + "." + fingerprint;
            page.NextCursor = page.HasMore ? body + "." + Sign(body) : null;
        }

        private static string Sign(string body)
        {
            using (var hmac = new HMACSHA256(CursorKey)) return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static int ParseCursor(string cursor, string fingerprint)
        {
            var parts = cursor.Split('.');
            int index;
            if (parts.Length != 4 || parts[0] != "1" || parts[2] != fingerprint ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) ||
                index.ToString(CultureInfo.InvariantCulture) != parts[1] ||
                Sign(string.Join(".", parts.Take(3))) != parts[3])
                throw new ArgumentException("Invalid, stale, or expired inventory cursor. Restart GETDEVICEINVENTORY without cursor.");
            return index;
        }
    }
}
