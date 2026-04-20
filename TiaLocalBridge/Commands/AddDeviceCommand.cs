using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Commands
{
    internal class AddDeviceCommand : ITiaCommand
    {
        public string Name => "ADDDEVICE";
        public string Description => "Adds a new device to an open project using a concrete hardware catalog type identifier. For PLC stations and compact devices, this uses CreateWithItem so the station is created with its subcomponents.";
        public string Usage => "ADDDEVICE|<project-name>|<type-identifier>|<device-name>|[device-item-name]";
        public string Example => "ADDDEVICE|DemoProject|OrderNumber:6ES7 510-1DJ01-0AB0/V3.0|PLC_1|PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 3 || providedArgs.Length > 4)
            {
                throw new ArgumentException($"Expected three or four arguments (<project-name>, <type-identifier>, <device-name>, optional [device-item-name]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var projectName = providedArgs[0];
            var typeIdentifier = providedArgs[1];
            var deviceName = providedArgs[2];
            var hasExplicitDeviceItemName = providedArgs.Length >= 4;
            var deviceItemName = hasExplicitDeviceItemName ? providedArgs[3] : deviceName;

            var openProjects = portal.Projects.ToList();
            if (!openProjects.Any())
            {
                throw new InvalidOperationException("No open projects. Open or create a project first.");
            }

            var project = openProjects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            if (project == null)
            {
                throw new InvalidOperationException($"Project '{projectName}' is not open. Open projects: {string.Join(", ", openProjects.Select(p => p.Name))}");
            }

            if (project.Devices.Any(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A device named '{deviceName}' already exists in project '{project.Name}'. Use GETDEVICES to inspect current devices.");
            }

            Device device = null;
            Exception createWithItemException = null;

            try
            {
                device = project.Devices.CreateWithItem(typeIdentifier, deviceName, deviceItemName);
            }
            catch (Exception ex)
            {
                createWithItemException = ex;
            }

            if (device == null)
            {
                try
                {
                    device = project.Devices.Create(typeIdentifier, deviceName);
                }
                catch (Exception createException)
                {
                    var createWithItemMessage = createWithItemException != null
                        ? $"CreateWithItem failed: {createWithItemException.Message}"
                        : "CreateWithItem was not attempted.";

                    throw new InvalidOperationException(
                        $"Failed to add device '{deviceName}' with type identifier '{typeIdentifier}'. Search the catalog first with SEARCHHWCATALOG and use the exact TypeIdentifier from the result. {createWithItemMessage} Create failed: {createException.Message}");
                }
            }

            return $"Added device '{device.Name}' to project '{project.Name}' [Reference={CommandSupport.GetDeviceReference(project, device)}, Type={device.TypeIdentifier}, DeviceItemName={deviceItemName}, UsedCreateWithItem={(createWithItemException == null)}].";
        }
    }
}
