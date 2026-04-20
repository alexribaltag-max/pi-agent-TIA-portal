using System;
using Siemens.Engineering;
using Siemens.Engineering.SW;

namespace TiaLocalBridge.Commands
{
    internal class CompilePlcCommand : ITiaCommand
    {
        public string Name => "COMPILEPLC";
        public string Description => "Compiles the full PLC software for a device using the TIA compile provider, which is useful after imports or before checking overall program consistency.";
        public string Usage => "COMPILEPLC|<device-reference>";
        public string Example => "COMPILEPLC|DemoProject/PLC_1";
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

            var compileResult = CommandSupport.CompilePlcSoftware(plcSoftware, $"PLC software in device '{resolvedReference}'");
            return $"Compiled PLC software in device '{resolvedReference}'. {CommandSupport.FormatCompilerResult(compileResult)}";
        }
    }
}
