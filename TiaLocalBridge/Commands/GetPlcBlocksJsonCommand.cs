using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlocksJsonCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKSJSON";
        public string Description => "Returns JSON details for PLC blocks in the specified PLC device reference, including block type, language, number, group reference, and consistency state.";
        public string Usage => "GETPLCBLOCKSJSON|<device-reference>";
        public string Example => "GETPLCBLOCKSJSON|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new System.InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blocks = CommandSupport.GetAllPlcBlocks(plcSoftware);
            var json = new StringBuilder();
            json.Append("{\"project\":\"")
                .Append(CommandSupport.EscapeJson(resolution.Project.Name))
                .Append("\",\"device\":{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(resolvedReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(resolution.Device.Name))
                .Append("\"},\"blockCount\":")
                .Append(blocks.Count)
                .Append(",\"blocks\":[");

            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                AppendBlockJson(json, blocks[i]);
            }

            json.Append("]}");
            return json.ToString();
        }

        private static void AppendBlockJson(StringBuilder json, PlcBlockResolution resolution)
        {
            var block = resolution.Block;
            var group = string.IsNullOrWhiteSpace(resolution.GroupReference) ? "<root>" : resolution.GroupReference;

            json.Append("{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(resolution.BlockReference))
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
                .Append("\",\"isKnowHowProtected\":")
                .Append(block.IsKnowHowProtected ? "true" : "false")
                .Append(",\"isConsistent\":")
                .Append(block.IsConsistent ? "true" : "false");

            if (block is InstanceDB instanceDb)
            {
                json.Append(",\"instanceOf\":\"")
                    .Append(CommandSupport.EscapeJson(instanceDb.InstanceOfName))
                    .Append("\"");
            }

            json.Append('}');
        }
    }
}
