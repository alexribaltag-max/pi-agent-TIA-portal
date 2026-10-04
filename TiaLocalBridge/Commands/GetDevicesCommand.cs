using System;
using System.Linq;
using Siemens.Engineering;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    internal class GetDevicesCommand : ITiaCommand
    {
        public string Name => "GETDEVICES";
        public string Description => "Lists root, ungrouped, and recursively grouped devices. Returns reusable references; reports incomplete enumeration. If only one project is open, the project name is optional.";
        public string Usage => "GETDEVICES|[project-name]";
        public string Example => "GETDEVICES|DemoProject";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length > 1)
                throw new ArgumentException($"Expected zero or one optional project name. Usage: {Usage}");
            var project = OpennessDeviceInventory.SelectProject(portal, providedArgs.FirstOrDefault());
            var inventory = OpennessDeviceInventory.GetForListing(portal, project);
            var devices = inventory.Devices.Select(device =>
                $"{device.Identity.Name} [Reference={device.PreferredReference}, Type={device.TypeIdentifier}]").ToList();
            var text = devices.Any()
                ? $"Project '{project.Name}' devices: {string.Join(", ", devices)}"
                : inventory.Complete ? $"Project '{project.Name}' has no devices." : $"Project '{project.Name}': no devices could be read.";
            if (!inventory.Complete)
                text = $"[Complete=false, WarningCount={inventory.WarningCount}] " + string.Join("; ", inventory.Warnings) +
                    " References require a complete inventory before downstream resolution. Use GETDEVICEINVENTORY for structured diagnostics. " + text;
            return text;
        }
    }
}
