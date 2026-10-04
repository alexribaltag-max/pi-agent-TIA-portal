using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiTagsCommand : ITiaCommand
    {
        public string Name => "GETHMITAGS";
        public string Description => "Lists HMI tags for Unified or classic WinCC targets. Unified output includes table, data type, address, and connection; classic Comfort output includes available engineering attributes.";
        public string Usage => "GETHMITAGS|<device-reference>";
        public string Example => "GETHMITAGS|DemoProject/HMI_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);
            var unifiedHmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);

            if (unifiedHmiSoftware != null)
            {
                var tags = CommandSupport.GetAllUnifiedHmiTags(unifiedHmiSoftware)
                    .Select(tag => string.Format(
                        "{0} [Table={1}, DataType={2}, Address={3}, Connection={4}]",
                        tag.Tag.Name,
                        tag.Table.TableReference,
                        tag.Tag.DataType ?? "<empty>",
                        string.IsNullOrWhiteSpace(tag.Tag.Address) ? "<empty>" : tag.Tag.Address,
                        string.IsNullOrWhiteSpace(tag.Tag.Connection) ? "<empty>" : tag.Tag.Connection))
                    .ToList();

                return tags.Any()
                    ? $"Device '{resolvedReference}' Unified HMI tags: {string.Join(", ", tags)}"
                    : $"Device '{resolvedReference}' has no Unified HMI tags.";
            }

            var hmiSoftware = CommandSupport.TryGetHmiSoftware(resolution.Device);
            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
            }

            var classicTags = CommandSupport.GetClassicHmiTagTables(hmiSoftware)
                .SelectMany(table =>
                {
                    var tags = table.Item.GetType().GetProperty("Tags")?.GetValue(table.Item, null) as System.Collections.IEnumerable;
                    return (tags == null ? Enumerable.Empty<object>() : tags.Cast<object>())
                        .Select(tag =>
                        {
                            var name = tag.GetType().GetProperty("Name")?.GetValue(tag, null)?.ToString() ?? "<unnamed>";
                            var attributes = tag is Siemens.Engineering.IEngineeringObject engineeringObject
                                ? CommandSupport.GetEngineeringAttributeSummaries(engineeringObject)
                                : new System.Collections.Generic.List<string>();
                            return attributes.Any()
                                ? $"{name} [Table={table.ObjectReference}, {string.Join("; ", attributes)}]"
                                : $"{name} [Table={table.ObjectReference}]";
                        });
                })
                .ToList();
            return classicTags.Any()
                ? $"Device '{resolvedReference}' classic HMI tags: {string.Join(", ", classicTags)}"
                : $"Device '{resolvedReference}' has no classic HMI tags.";
        }
    }
}
