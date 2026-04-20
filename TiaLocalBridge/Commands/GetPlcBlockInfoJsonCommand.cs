using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlockInfoJsonCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKINFOJSON";
        public string Description => "Returns detailed JSON information for one PLC block so agents can inspect metadata, timestamps, consistency, and instance DB relationships without parsing text output.";
        public string Usage => "GETPLCBLOCKINFOJSON|<device-reference>|<block-reference>";
        public string Example => "GETPLCBLOCKINFOJSON|DemoProject/PLC_1|Main/FB_Machine";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new System.InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var block = blockResolution.Block;
            var group = string.IsNullOrWhiteSpace(blockResolution.GroupReference) ? "<root>" : blockResolution.GroupReference;
            var json = new StringBuilder();

            json.Append("{\"project\":\"")
                .Append(CommandSupport.EscapeJson(resolution.Project.Name))
                .Append("\",\"device\":{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(resolvedReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(resolution.Device.Name))
                .Append("\"},\"block\":{")
                .Append("\"reference\":\"")
                .Append(CommandSupport.EscapeJson(blockResolution.BlockReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(block.Name))
                .Append("\",\"type\":\"")
                .Append(CommandSupport.EscapeJson(CommandSupport.GetPlcBlockTypeName(block)))
                .Append("\",\"number\":")
                .Append(block.Number)
                .Append(",\"programmingLanguage\":\"")
                .Append(CommandSupport.EscapeJson(block.ProgrammingLanguage.ToString()))
                .Append("\",\"group\":\"")
                .Append(CommandSupport.EscapeJson(group))
                .Append("\",\"namespace\":\"")
                .Append(CommandSupport.EscapeJson(block.Namespace))
                .Append("\",\"headerName\":\"")
                .Append(CommandSupport.EscapeJson(block.HeaderName))
                .Append("\",\"headerAuthor\":\"")
                .Append(CommandSupport.EscapeJson(block.HeaderAuthor))
                .Append("\",\"headerFamily\":\"")
                .Append(CommandSupport.EscapeJson(block.HeaderFamily))
                .Append("\",\"headerVersion\":\"")
                .Append(CommandSupport.EscapeJson(block.HeaderVersion.ToString()))
                .Append("\",\"autoNumber\":")
                .Append(block.AutoNumber ? "true" : "false")
                .Append(",\"memoryLayout\":\"")
                .Append(CommandSupport.EscapeJson(block.MemoryLayout.ToString()))
                .Append("\",\"isKnowHowProtected\":")
                .Append(block.IsKnowHowProtected ? "true" : "false")
                .Append(",\"isConsistent\":")
                .Append(block.IsConsistent ? "true" : "false")
                .Append(",\"creationDate\":\"")
                .Append(CommandSupport.EscapeJson(block.CreationDate.ToString("O")))
                .Append("\",\"modifiedDate\":\"")
                .Append(CommandSupport.EscapeJson(block.ModifiedDate.ToString("O")))
                .Append("\",\"compileDate\":\"")
                .Append(CommandSupport.EscapeJson(block.CompileDate.ToString("O")))
                .Append("\",\"interfaceModifiedDate\":\"")
                .Append(CommandSupport.EscapeJson(block.InterfaceModifiedDate.ToString("O")))
                .Append("\",\"codeModifiedDate\":\"")
                .Append(CommandSupport.EscapeJson(block.CodeModifiedDate.ToString("O")))
                .Append("\",\"structureModified\":\"")
                .Append(CommandSupport.EscapeJson(block.StructureModified.ToString("O")))
                .Append("\",\"parameterModified\":\"")
                .Append(CommandSupport.EscapeJson(block.ParameterModified.ToString("O")))
                .Append("\"");

            if (block is InstanceDB instanceDb)
            {
                json.Append(",\"instanceOf\":\"")
                    .Append(CommandSupport.EscapeJson(instanceDb.InstanceOfName))
                    .Append("\",\"dataBlockType\":\"InstanceDB\"");
            }
            else if (block is DataBlock)
            {
                json.Append(",\"dataBlockType\":\"GlobalDB\"");
            }
            else if (block is CodeBlock)
            {
                json.Append(",\"contentCategory\":\"CodeBlock\"");
            }

            json.Append("}}");
            return json.ToString();
        }
    }
}
