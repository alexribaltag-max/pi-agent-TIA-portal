using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcTagsCommand : ITiaCommand
    {
        public string Name => "GETPLCTAGS";
        public string Description => "Lists all PLC tags for the specified PLC device reference. Use the [Reference=...] value returned by GETDEVICES, or use either '<device-name>' or '<project-name>/<device-name>'.";
        public string Usage => "GETPLCTAGS|<device-reference>";
        public string Example => "GETPLCTAGS|DemoProject/PLC_1";
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

            var tags = CommandSupport.GetAllPlcTagsWithTables(plcSoftware)
                .Select(tag => string.Format("{0} [Table={1}, DataType={2}, Address={3}]", tag.Tag.Name, tag.Table.TableReference, tag.Tag.DataTypeName, tag.Tag.LogicalAddress))
                .ToList();

            return tags.Any()
                ? $"Device '{resolvedReference}' PLC tags: {string.Join(", ", tags)}"
                : $"Device '{resolvedReference}' has no PLC tags.";
        }
    }
}
