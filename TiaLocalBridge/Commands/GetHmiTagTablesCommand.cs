using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiTagTablesCommand : ITiaCommand
    {
        public string Name => "GETHMITAGTABLES";
        public string Description => "Lists tag tables for Unified or classic WinCC targets. Classic Comfort table references include nested tag-folder paths.";
        public string Usage => "GETHMITAGTABLES|<device-reference>";
        public string Example => "GETHMITAGTABLES|DemoProject/HMI_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                var classicHmi = CommandSupport.TryGetHmiSoftware(resolution.Device);
                if (classicHmi == null)
                {
                    throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
                }

                var classicTables = CommandSupport.GetClassicHmiTagTables(classicHmi)
                    .OrderBy(table => table.ObjectReference, StringComparer.OrdinalIgnoreCase)
                    .Select(table =>
                    {
                        var tagsProperty = table.Item.GetType().GetProperty("Tags");
                        var tags = tagsProperty?.GetValue(table.Item, null) as System.Collections.IEnumerable;
                        var count = tags == null ? 0 : tags.Cast<object>().Count();
                        return $"{table.ObjectReference} [TagCount={count}]";
                    })
                    .ToList();

                return classicTables.Any()
                    ? $"Device '{resolvedReference}' classic HMI tag tables: {string.Join(", ", classicTables)}"
                    : $"Device '{resolvedReference}' has no classic HMI tag tables.";
            }

            var tables = CommandSupport.GetAllUnifiedHmiTagTables(hmiSoftware)
                .OrderBy(table => table.TableReference, StringComparer.OrdinalIgnoreCase)
                .Select(table => string.Format("{0} [TagCount={1}]", table.TableReference, table.Table.Tags.Count))
                .ToList();

            return tables.Any()
                ? $"Device '{resolvedReference}' Unified HMI tag tables: {string.Join(", ", tables)}"
                : $"Device '{resolvedReference}' has no Unified HMI tag tables.";
        }
    }
}
