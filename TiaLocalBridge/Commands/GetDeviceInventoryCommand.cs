using System;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    internal sealed class GetDeviceInventoryCommand : ITiaCommand
    {
        public string Name => "GETDEVICEINVENTORY";
        public string Description => "Returns a bounded structured root/ungrouped/recursive-group device inventory with canonical references, completeness warnings, field projection and change-detecting continuation.";
        public string Usage => "GETDEVICEINVENTORY|<project-name>|[scope=...]|[fields=field,...]|[limit=1..500]|[cursor=...]|[capabilities=true/false]";
        public string Example => "GETDEVICEINVENTORY|PackagingMachine|scope=all|limit=100";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var query = DeviceInventoryQuery.Parse(args);
            var project = OpennessDeviceInventory.SelectProject(portal, query.ProjectName);
            var inventory = OpennessDeviceInventory.GetForListing(portal, project);
            return InventoryJson.Serialize(DeviceInventoryPaging.Read(inventory, query, ReadCapabilities));
        }

        private static string[] ReadCapabilities(InventoryDevice entry)
        {
            try
            {
                var device = (Device)entry.Handle;
                if (CommandSupport.TryGetPlcSoftware(device) != null) return new[] { "PLC" };
                var hmi = CommandSupport.TryGetHmiSoftware(device);
                if (hmi is HmiTarget) return new[] { "classic-HMI" };
                if (hmi is HmiSoftware) return new[] { "Unified-HMI" };
                return new[] { "other" };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("CAPABILITY_READ_FAILED: " + DeviceInventory.Bounded(ex.Message, 240));
            }
        }
    }
}
