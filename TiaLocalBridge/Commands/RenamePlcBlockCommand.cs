using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class RenamePlcBlockCommand : ITiaCommand
    {
        public string Name => "RENAMEPLCBLOCK";
        public string Description => "Renames one PLC block in the specified PLC device reference. Prefer using this on imported or disposable test blocks unless the user explicitly wants to rename an existing project block.";
        public string Usage => "RENAMEPLCBLOCK|<device-reference>|<block-reference>|<new-block-name>";
        public string Example => "RENAMEPLCBLOCK|DemoProject/PLC_1|02_Global/Data_ImportedTest|Data_RenamedTest";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>", "<new-block-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var block = blockResolution.Block;
            var newBlockName = providedArgs[2].Trim();
            if (string.IsNullOrWhiteSpace(newBlockName))
            {
                throw new ArgumentException("New PLC block name cannot be empty.");
            }

            var oldBlockReference = blockResolution.BlockReference;
            var groupReference = string.IsNullOrWhiteSpace(blockResolution.GroupReference) ? "<root>" : blockResolution.GroupReference;
            var blockType = CommandSupport.GetPlcBlockTypeName(block);
            var programmingLanguage = block.ProgrammingLanguage.ToString();

            block.Name = newBlockName;

            var newBlockReference = string.Equals(groupReference, "<root>", StringComparison.OrdinalIgnoreCase)
                ? newBlockName
                : groupReference + "/" + newBlockName;

            return $"Renamed PLC block '{oldBlockReference}' to '{newBlockReference}' in '{resolvedReference}' [Type={blockType}, Language={programmingLanguage}]. Prefer renaming imported or disposable test blocks unless production changes were explicitly intended.";
        }
    }
}
