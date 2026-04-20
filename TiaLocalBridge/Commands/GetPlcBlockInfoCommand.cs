using System;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlockInfoCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKINFO";
        public string Description => "Returns detailed information for one PLC block so you can decide later how to export or modify its content.";
        public string Usage => "GETPLCBLOCKINFO|<device-reference>|<block-reference>";
        public string Example => "GETPLCBLOCKINFO|DemoProject/PLC_1|Main/FB_Machine";
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
            var group = string.IsNullOrWhiteSpace(blockResolution.GroupReference) ? "<root>" : blockResolution.GroupReference;
            var blockType = CommandSupport.GetPlcBlockTypeName(block);
            var result = string.Format(
                "Device '{0}' PLC block '{1}': Type={2}, Name={3}, Number={4}, ProgrammingLanguage={5}, Group={6}, Namespace={7}, HeaderName={8}, HeaderAuthor={9}, HeaderFamily={10}, HeaderVersion={11}, AutoNumber={12}, MemoryLayout={13}, KnowHowProtected={14}, IsConsistent={15}, CreationDate={16:O}, ModifiedDate={17:O}, CompileDate={18:O}, InterfaceModifiedDate={19:O}, CodeModifiedDate={20:O}, StructureModified={21:O}, ParameterModified={22:O}",
                resolvedReference,
                blockResolution.BlockReference,
                blockType,
                block.Name,
                block.Number,
                block.ProgrammingLanguage,
                group,
                block.Namespace,
                block.HeaderName,
                block.HeaderAuthor,
                block.HeaderFamily,
                block.HeaderVersion,
                block.AutoNumber,
                block.MemoryLayout,
                block.IsKnowHowProtected,
                block.IsConsistent,
                block.CreationDate,
                block.ModifiedDate,
                block.CompileDate,
                block.InterfaceModifiedDate,
                block.CodeModifiedDate,
                block.StructureModified,
                block.ParameterModified);

            if (block is InstanceDB instanceDb)
            {
                result += string.Format(", InstanceOf={0}", instanceDb.InstanceOfName);
            }
            else if (block is DataBlock)
            {
                result += ", DataBlockType=GlobalDB";
            }
            else if (block is CodeBlock)
            {
                result += ", ContentCategory=CodeBlock";
            }

            return result;
        }
    }
}
