using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class DeletePlcBlockCommand : ITiaCommand
    {
        public string Name => "DELETEPLCBLOCK";
        public string Description => "Deletes one PLC block from the specified PLC device reference. Prefer using this on disposable or imported test blocks unless the user explicitly wants to remove an existing project block.";
        public string Usage => "DELETEPLCBLOCK|<device-reference>|<block-reference>";
        public string Example => "DELETEPLCBLOCK|DemoProject/PLC_1|02_Global/Data_ImportedTest";
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
            var block = blockResolution.Block;
            var deletedBlockReference = blockResolution.BlockReference;
            var blockType = CommandSupport.GetPlcBlockTypeName(block);
            var programmingLanguage = block.ProgrammingLanguage.ToString();

            block.Delete();

            return $"Deleted PLC block '{deletedBlockReference}' from '{resolvedReference}' [Type={blockType}, Language={programmingLanguage}]. Prefer deleting imported or disposable test blocks unless production cleanup was explicitly intended.";
        }
    }
}
