using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TiaLocalBridge.Services
{
    // Pure adapter boundary: no Siemens assemblies or cached engineering handles in this service.
    internal sealed class InventorySource
    {
        public string ProjectName;
        public object Project;
        public Func<IEnumerable<InventoryDevice>> RootDevices;
        public Func<IEnumerable<InventoryDevice>> UngroupedDevices;
        public Func<IEnumerable<InventoryGroup>> Groups;
    }

    internal sealed class InventoryGroup
    {
        public string Name;
        public object Handle;
        public Func<IEnumerable<InventoryDevice>> Devices;
        public Func<IEnumerable<InventoryGroup>> Groups;
    }

    internal sealed class InventoryDevice
    {
        public object Handle;
        public DeviceReference Identity;
        public string TypeIdentifier;
        public string CanonicalReference { get { return Identity.Encode(); } }
        public string LegacyReference;
        public string PreferredReference { get { return LegacyReference ?? CanonicalReference; } }
    }

    internal sealed class DeviceInventory
    {
        public object Project;
        public string ProjectName;
        public readonly List<InventoryDevice> Devices = new List<InventoryDevice>();
        public readonly List<string> Warnings = new List<string>();
        public int WarningCount { get; private set; }
        public bool Complete { get { return WarningCount == 0; } }

        public void Warn(string scope, string message)
        {
            WarningCount++;
            if (Warnings.Count < 20) Warnings.Add(Bounded(scope, 180) + ": " + Bounded(message, 240));
        }

        public void RequireComplete()
        {
            if (!Complete) throw new InvalidOperationException("INVENTORY_INCOMPLETE: Cannot establish unique device identity or safely check duplicates. " +
                string.Join("; ", Warnings.Take(3)) + " Use GETDEVICEINVENTORY to inspect warnings.");
        }

        public static string Bounded(string text, int length)
        {
            text = (text ?? "").Replace('\r', ' ').Replace('\n', ' ');
            if (text.Length <= length) return text;
            if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
            return text.Substring(0, length) + "...";
        }
    }

    internal sealed class HandleReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) { return ReferenceEquals(x, y); }
        public int GetHashCode(object obj) { return RuntimeHelpers.GetHashCode(obj); }
    }

    internal static class DeviceInventoryService
    {
        public static DeviceInventory Collect(InventorySource source)
        {
            var result = new DeviceInventory { Project = source.Project, ProjectName = source.ProjectName };
            var handles = new Dictionary<object, InventoryDevice>(new HandleReferenceComparer());
            var locations = new Dictionary<string, InventoryDevice>(StringComparer.Ordinal);
            var groups = new HashSet<object>(new HandleReferenceComparer());
            int visitedGroups = 0;
            ReadDevices(source.RootDevices, "root", result, handles, locations);
            ReadDevices(source.UngroupedDevices, "ungrouped", result, handles, locations);
            ReadGroups(source.Groups, new string[0], result, handles, locations, groups, ref visitedGroups);
            result.Devices.Sort(CompareDevices);

            // Legacy aliases are safe only when unique over the whole, complete project inventory.
            if (result.Complete)
            {
                foreach (var sameName in result.Devices.GroupBy(d => d.Identity.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (sameName.Count() != 1) continue;
                    var device = sameName.Single();
                    var alias = source.ProjectName + "/" + device.Identity.Name;
                    if (!alias.StartsWith(DeviceReference.ReservedPrefix, StringComparison.OrdinalIgnoreCase) &&
                        alias.IndexOfAny(new[] { '|', '\r', '\n', '\t' }) < 0 && alias == alias.Trim())
                        device.LegacyReference = alias;
                }
            }
            return result;
        }

        private static void ReadDevices(Func<IEnumerable<InventoryDevice>> enumerate, string scope, DeviceInventory result,
            Dictionary<object, InventoryDevice> handles, Dictionary<string, InventoryDevice> locations)
        {
            try
            {
                int exposed = 0;
                foreach (var item in enumerate())
                {
                    if (++exposed > 100000 || result.Devices.Count >= 100000) { result.Warn(scope, "Device enumeration safety limit reached."); break; }
                    if (item == null || item.Handle == null || item.Identity == null) throw new InvalidOperationException("Missing device identity/handle.");
                    if (item.Identity.ProjectName != result.ProjectName) throw new InvalidOperationException("Device owner project does not match inventory project.");
                    var reference = item.CanonicalReference;
                    InventoryDevice previous;
                    if (handles.TryGetValue(item.Handle, out previous))
                    {
                        // Same managed handle is positive duplicate evidence. Different wrappers are NOT
                        // assumed distinct, nor merged by name/location or unverified Openness Equals().
                        if (previous.CanonicalReference != reference || previous.TypeIdentifier != item.TypeIdentifier)
                            result.Warn(scope, "Device metadata changed during enumeration.");
                        continue;
                    }
                    if (locations.ContainsKey(reference))
                        result.Warn(scope, "IDENTITY_UNVERIFIED: Multiple handles share a canonical location; no wrapper-equality assumption was made.");
                    else locations.Add(reference, item);
                    item.LegacyReference = null;
                    handles.Add(item.Handle, item);
                    result.Devices.Add(item);
                }
            }
            catch (Exception ex)
            {
                result.Warn(scope, ex.Message);
            }
        }

        private static void ReadGroups(Func<IEnumerable<InventoryGroup>> enumerate, string[] path, DeviceInventory result,
            Dictionary<object, InventoryDevice> handles, Dictionary<string, InventoryDevice> locations,
            HashSet<object> groupHandles, ref int visited)
        {
            var scope = "groups/" + string.Join("/", path);
            try
            {
                foreach (var group in enumerate())
                {
                    if (path.Length >= 64) { result.Warn(scope, "Group depth safety limit reached."); break; }
                    if (++visited > 10000) { result.Warn(scope, "Group enumeration safety limit reached."); break; }
                    if (group == null || group.Handle == null || string.IsNullOrEmpty(group.Name)) throw new InvalidOperationException("Missing group identity.");
                    if (!groupHandles.Add(group.Handle)) { result.Warn(scope, "Repeated/cyclic group handle encountered."); continue; }
                    var childPath = path.Concat(new[] { group.Name }).ToArray();
                    ReadDevices(group.Devices, "groups/" + string.Join("/", childPath), result, handles, locations);
                    ReadGroups(group.Groups, childPath, result, handles, locations, groupHandles, ref visited);
                }
            }
            catch (Exception ex)
            {
                result.Warn(scope, ex.Message);
            }
        }

        private static int CompareDevices(InventoryDevice left, InventoryDevice right)
        {
            var kinds = new[] { "root", "ungrouped", "userGroup" };
            var compare = Array.IndexOf(kinds, left.Identity.ContainerKind).CompareTo(Array.IndexOf(kinds, right.Identity.ContainerKind));
            if (compare != 0) return compare;
            var a = left.Identity.GroupPath;
            var b = right.Identity.GroupPath;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                compare = StringComparer.Ordinal.Compare(a[i], b[i]);
                if (compare != 0) return compare;
            }
            compare = a.Length.CompareTo(b.Length);
            if (compare != 0) return compare;
            compare = StringComparer.Ordinal.Compare(left.Identity.Name, right.Identity.Name);
            return compare != 0 ? compare : StringComparer.Ordinal.Compare(left.TypeIdentifier, right.TypeIdentifier);
        }
    }
}
