using System;
using System.Linq;
using System.Text;

namespace TiaLocalBridge.Services
{
    internal sealed class DeviceReference
    {
        public const string ReservedPrefix = "tia-device:";
        private const string Prefix = ReservedPrefix + "v1:";
        public string ProjectName { get; private set; }
        public string ContainerKind { get; private set; }
        public string[] GroupPath { get; private set; }
        public string Name { get; private set; }

        public DeviceReference(string project, string kind, string[] groups, string name)
        {
            ValidateName(project);
            ValidateName(name);
            if (groups == null || groups.Length > 64 || !new[] { "root", "ungrouped", "userGroup" }.Contains(kind) ||
                (kind == "userGroup" ? groups.Length == 0 : groups.Length != 0))
                throw new ArgumentException("Invalid device reference container or group path.");
            foreach (var group in groups) ValidateName(group);
            ProjectName = project;
            ContainerKind = kind;
            GroupPath = groups.ToArray();
            Name = name;
        }

        public string Encode()
        {
            // Fixed tuple canonicalization, NOT the bridge protocol serializer.
            var json = "[" + Quote(ProjectName) + "," + Quote(ContainerKind) + ",[" +
                string.Join(",", GroupPath.Select(Quote)) + "]," + Quote(Name) + "]";
            var result = Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            if (result.Length > 8192) throw new ArgumentException("Device reference exceeds the 8192-character limit.");
            return result;
        }

        public static DeviceReference Decode(string value)
        {
            try
            {
                if (value == null || value.Length > 8192 || !value.StartsWith(Prefix, StringComparison.Ordinal))
                    throw new ArgumentException("Unsupported device reference version or size.");
                var body = value.Substring(Prefix.Length);
                if (body.Length == 0 || body.Any(c => !(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') &&
                    !(c >= '0' && c <= '9') && c != '-' && c != '_'))
                    throw new ArgumentException("Invalid device reference encoding.");
                var padded = body.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
                var json = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(padded));
                var parts = InventoryJson.Deserialize<object[]>(json);
                if (parts == null || parts.Length != 4 || !(parts[2] is object[]))
                    throw new ArgumentException("Invalid device reference tuple.");
                var groups = ((object[])parts[2]).Select(p => p as string).ToArray();
                var reference = new DeviceReference(parts[0] as string, parts[1] as string, groups, parts[3] as string);
                if (reference.Encode() != value) throw new ArgumentException("Noncanonical device reference.");
                return reference;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                throw new ArgumentException("Invalid canonical device reference. Use an exact reference from GETDEVICEINVENTORY.", ex);
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Reference names must not be empty.");
            new UTF8Encoding(false, true).GetByteCount(name);
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (c < 32) result.Append("\\u" + ((int)c).ToString("x4"));
                        else result.Append(c);
                        break;
                }
            }
            return result.Append('"').ToString();
        }
    }
}
