using System;
using Siemens.Engineering;
using Siemens.Engineering.SW;

namespace TiaLocalBridge.Commands
{
    internal class UpdateProgramCommand : ITiaCommand
    {
        public string Name => "UPDATEPROGRAM";
        public string Description => "Runs PlcSoftware.UpdateProgram for the resolved PLC device so imported or changed program objects are refreshed at the PLC software level.";
        public string Usage => "UPDATEPROGRAM|<device-reference>";
        public string Example => "UPDATEPROGRAM|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            plcSoftware.UpdateProgram();
            return $"Updated PLC program in device '{resolvedReference}'. Review block consistency afterward if you imported or changed blocks.";
        }
    }
}
