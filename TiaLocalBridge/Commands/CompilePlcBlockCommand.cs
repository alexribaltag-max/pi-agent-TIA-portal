using System;
using Siemens.Engineering;
using Siemens.Engineering.SW;

namespace TiaLocalBridge.Commands
{
    internal class CompilePlcBlockCommand : ITiaCommand
    {
        public string Name => "COMPILEPLCBLOCK";
        public string Description => "Compiles one PLC block using the TIA compile provider so you can validate an imported or modified block before a broader program update.";
        public string Usage => "COMPILEPLCBLOCK|<device-reference>|<block-reference>";
        public string Example => "COMPILEPLCBLOCK|DemoProject/PLC_1|02_Global/Data";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var compileResult = CommandSupport.CompilePlcBlock(blockResolution.Block, $"PLC block '{blockResolution.BlockReference}' in device '{resolvedReference}'");
            return $"Compiled PLC block '{blockResolution.BlockReference}' in device '{resolvedReference}'. {CommandSupport.FormatCompilerResult(compileResult)}";
        }
    }
}
