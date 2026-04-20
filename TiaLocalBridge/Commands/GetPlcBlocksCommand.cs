using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlocksCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKS";
        public string Description => "Lists PLC blocks for the specified PLC device reference, including block type, language, number, and group reference, before working with block content.";
        public string Usage => "GETPLCBLOCKS|<device-reference>";
        public string Example => "GETPLCBLOCKS|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blocks = CommandSupport.GetAllPlcBlocks(plcSoftware)
                .Select(FormatBlock)
                .ToList();

            return blocks.Any()
                ? $"Device '{resolvedReference}' PLC blocks: {string.Join(", ", blocks)}"
                : $"Device '{resolvedReference}' has no PLC blocks.";
        }

        private static string FormatBlock(PlcBlockResolution resolution)
        {
            var block = resolution.Block;
            var blockType = CommandSupport.GetPlcBlockTypeName(block);
            var group = string.IsNullOrWhiteSpace(resolution.GroupReference) ? "<root>" : resolution.GroupReference;
            var details = string.Format("Reference={0}, Type={1}, Number={2}, Language={3}, Group={4}, KnowHowProtected={5}, Consistent={6}",
                resolution.BlockReference,
                blockType,
                block.Number,
                block.ProgrammingLanguage,
                group,
                block.IsKnowHowProtected,
                block.IsConsistent);

            if (block is InstanceDB instanceDb)
            {
                details += string.Format(", InstanceOf={0}", instanceDb.InstanceOfName);
            }

            return string.Format("{0} [{1}]", block.Name, details);
        }
    }
}
