using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Services
{
    internal static class OpennessDeviceInventory
    {
        [ThreadStatic] private static Dictionary<object, DeviceInventory> requestCache;

        public static IDisposable BeginRequest()
        {
            if (requestCache != null) throw new InvalidOperationException("Nested inventory request scope.");
            requestCache = new Dictionary<object, DeviceInventory>(new HandleReferenceComparer());
            return new RequestScope();
        }

        private sealed class RequestScope : IDisposable
        {
            public void Dispose() { requestCache = null; }
        }

        public static Project SelectProject(TiaPortal portal, string name)
        {
            var projects = portal.Projects.ToList();
            if (projects.Count == 0) throw new InvalidOperationException("No open projects. Open or create a project first.");
            var matches = name == null ? projects : projects.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException("Project selection is missing or ambiguous. Specify one unique open project name. Open projects: " +
                    string.Join(", ", projects.Take(10).Select(p => DeviceInventory.Bounded(p.Name, 120))));
            return matches[0];
        }

        public static DeviceInventory GetForListing(TiaPortal portal, Project project)
        {
            // SelectProject already required a unique project name. Compare other names
            // without making any assumption about project wrapper reference equality.
            var inventory = Get(project);
            DeviceReferenceResolver.SuppressAmbiguousLegacyAliases(inventory,
                portal.Projects.Select(p => p.Name).Where(name => name != project.Name));
            return inventory;
        }

        public static DeviceInventory Get(Project project)
        {
            DeviceInventory result;
            if (requestCache != null && requestCache.TryGetValue(project, out result)) return result;
            result = DeviceInventoryService.Collect(new InventorySource
            {
                Project = project,
                ProjectName = project.Name,
                RootDevices = () => ReadDevices(project, project.Devices),
                UngroupedDevices = () => project.UngroupedDevicesGroup == null
                    ? Enumerable.Empty<InventoryDevice>() : ReadDevices(project, project.UngroupedDevicesGroup.Devices),
                Groups = () => ReadGroups(project, project.DeviceGroups)
            });
            if (requestCache != null) requestCache.Add(project, result);
            return result;
        }

        private static IEnumerable<InventoryDevice> ReadDevices(Project project, IEnumerable<Device> devices)
        {
            foreach (var device in devices)
                yield return new InventoryDevice { Handle = device, Identity = Identify(project, device), TypeIdentifier = device.TypeIdentifier };
        }

        private static IEnumerable<InventoryGroup> ReadGroups(Project project, IEnumerable<DeviceUserGroup> groups)
        {
            foreach (var group in groups)
            {
                var current = group;
                yield return new InventoryGroup
                {
                    Name = current.Name, Handle = current,
                    Devices = () => ReadDevices(project, current.Devices),
                    Groups = () => ReadGroups(project, current.Groups)
                };
            }
        }

        public static DeviceReference Identify(Project project, Device device)
        {
            // The owner chain, not the composition through which a device was exposed,
            // defines its location. Do not mistake a DeviceItem/module for a Device.
            var groups = new List<string>();
            var owner = device.Parent;
            var kind = "root";
            int depth = 0;
            while (owner is DeviceUserGroup)
            {
                if (++depth > 64) throw new InvalidOperationException("Device owner chain exceeds depth limit.");
                var group = (DeviceUserGroup)owner;
                groups.Add(group.Name);
                kind = "userGroup";
                owner = group.Parent;
            }
            if (owner is DeviceSystemGroup && groups.Count == 0)
            {
                kind = "ungrouped";
                owner = ((DeviceSystemGroup)owner).Parent;
            }
            var owningProject = owner as Project;
            if (owningProject == null || owningProject.Name != project.Name)
                throw new InvalidOperationException("Unsupported or inconsistent device owner chain.");
            groups.Reverse();
            return new DeviceReference(project.Name, kind, groups.ToArray(), device.Name);
        }

        public static string GetReference(Project project, Device device)
        {
            // Resolution/listing already populated the request cache; do not discover again.
            DeviceInventory inventory;
            if (requestCache != null && requestCache.TryGetValue(project, out inventory))
            {
                var entry = inventory.Devices.FirstOrDefault(d => ReferenceEquals(d.Handle, device));
                if (entry != null) return entry.PreferredReference;
            }
            // Newly created objects are not in the preflight snapshot. Return their canonical
            // owner-based reference, never an unverified legacy alias or a stale cache entry.
            return Identify(project, device).Encode();
        }
    }
}
