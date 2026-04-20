using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Commands
{
    internal class AddModuleCommand : ITiaCommand
    {
        public string Name => "ADDMODULE";
        public string Description => "Adds a hardware module from the TIA hardware catalog into a device or parent device item at the specified plug position. Search the hardware catalog first, then inspect plug locations before inserting the module.";
        public string Usage => "ADDMODULE|<device-reference>|<parent-target-reference>|<type-identifier>|<module-name>|<position-number>";
        public string Example => "ADDMODULE|DemoProject/PLC_1|1|OrderNumber:6ES7 521-1BH50-0AA0/V1.0|DI_16x24VDC|4";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<parent-target-reference>", "<type-identifier>", "<module-name>", "<position-number>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var parentResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var typeIdentifier = providedArgs[2];
            var moduleName = providedArgs[3];

            if (!int.TryParse(providedArgs[4], out int positionNumber))
            {
                throw new ArgumentException($"Invalid <position-number> '{providedArgs[4]}'. Use GETPLUGLOCATIONS to inspect valid integer slot numbers.");
            }

            DeviceItem createdModule;
            try
            {
                createdModule = CommandSupport.PlugNewModule(parentResolution.TargetObject, typeIdentifier, moduleName, positionNumber);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add module '{moduleName}' with type identifier '{typeIdentifier}' at position {positionNumber} under '{parentResolution.TargetReference}'. Details: {ex.Message}");
            }

            var createdResolution = CommandSupport.GetAllDeviceItemResolutions(deviceResolution.Device)
                .FirstOrDefault(candidate => object.ReferenceEquals(candidate.Item, createdModule));
            var createdReference = createdResolution != null ? createdResolution.ItemReference : $"<position-{createdModule.PositionNumber}>";

            return $"Added module '{createdModule.Name}' to device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}' [Reference={createdReference}, Parent={parentResolution.TargetReference}, Type={createdModule.TypeIdentifier}, Position={createdModule.PositionNumber}, Plugged={(createdModule.IsPlugged ? "true" : "false")}].";
        }
    }
}
