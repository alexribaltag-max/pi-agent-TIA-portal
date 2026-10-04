using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaLocalBridge.Services
{
    internal sealed class InventoryResolution
    {
        public DeviceInventory Inventory;
        public InventoryDevice Device;
    }

    internal static class DeviceReferenceResolver
    {
        public static InventoryResolution Resolve(IEnumerable<DeviceInventory> inventories, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentException("Device reference cannot be empty.");
            var projects = inventories.ToList();
            if (projects.Count == 0) throw new InvalidOperationException("No open projects. Open or create a project first.");
            // A project/name alias can also collide with another open project's prefix
            // (including project names that differ only by case). Canonical refs remain exact.
            foreach (var project in projects)
                SuppressAmbiguousLegacyAliases(project, projects.Where(other => !ReferenceEquals(other, project)).Select(other => other.ProjectName));
            reference = reference.Trim();
            List<DeviceInventory> scope;
            Func<InventoryDevice, bool> match;
            if (reference.StartsWith(DeviceReference.ReservedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var canonical = DeviceReference.Decode(reference);
                scope = projects.Where(p => p.ProjectName == canonical.ProjectName).ToList();
                if (scope.Count != 1) throw new InvalidOperationException("Canonical reference project is missing or ambiguous. Re-discover the target project.");
                match = d => d.CanonicalReference == reference;
            }
            else
            {
                scope = projects.Where(p => reference.StartsWith(p.ProjectName + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                if (scope.Count == 0)
                {
                    scope = projects;
                    match = d => string.Equals(d.Identity.Name, reference, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    if (scope.GroupBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                        throw new InvalidOperationException("Project-qualified reference is ambiguous across open projects.");
                    match = d => string.Equals(d.Identity.ProjectName + "/" + d.Identity.Name, reference, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (scope.GroupBy(p => p.ProjectName, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Device reference project identity is ambiguous across open projects.");
            foreach (var project in scope) project.RequireComplete();
            var matches = scope.SelectMany(p => p.Devices.Where(match).Select(d => new InventoryResolution { Inventory = p, Device = d })).ToList();
            if (matches.Count == 1) return matches[0];
            var candidates = matches.Count > 1 ? matches.Select(m => m.Device) : scope.SelectMany(p => p.Devices);
            throw new InvalidOperationException((matches.Count > 1 ? "AMBIGUOUS_DEVICE: " : "DEVICE_NOT_FOUND: ") +
                DeviceInventory.Bounded(reference, 160) + ". " + DescribeCandidates(candidates));
        }

        public static void SuppressAmbiguousLegacyAliases(DeviceInventory inventory, IEnumerable<string> otherProjectNames)
        {
            var names = otherProjectNames.ToArray();
            foreach (var device in inventory.Devices.Where(d => d.LegacyReference != null))
                if (names.Any(name => device.LegacyReference.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase)))
                    device.LegacyReference = null;
        }

        public static string DescribeCandidates(IEnumerable<InventoryDevice> devices)
        {
            var references = devices.Select(d => d.CanonicalReference).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var shown = new List<string>();
            int characters = 0;
            foreach (var reference in references.Take(10))
            {
                if (characters + reference.Length > 3000) break;
                shown.Add(reference);
                characters += reference.Length;
            }
            return "Canonical candidates: " + (shown.Count == 0 ? "(none displayed)" : string.Join(", ", shown)) +
                (shown.Count < references.Count ? "; " + (references.Count - shown.Count) + " more omitted" : "") +
                ". Use GETDEVICEINVENTORY|<project-name> for the paged inventory.";
        }

        public static void EnsureNameAvailable(DeviceInventory inventory, string name)
        {
            inventory.RequireComplete();
            var matches = inventory.Devices.Where(d => string.Equals(d.Identity.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 0) throw new InvalidOperationException("A device with this name already exists in the project. " + DescribeCandidates(matches));
        }
    }
}
