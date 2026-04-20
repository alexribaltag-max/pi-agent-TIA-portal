using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiTagTablesCommand : ITiaCommand
    {
        public string Name => "GETHMITAGTABLES";
        public string Description => "Lists all Unified HMI tag tables for the specified HMI device reference so you can target a table when creating, updating, or deleting HMI tags.";
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
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
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
