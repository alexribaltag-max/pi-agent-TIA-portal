using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcTagTablesCommand : ITiaCommand
    {
        public string Name => "GETPLCTAGTABLES";
        public string Description => "Lists all PLC tag tables for the specified PLC device reference so you can target a table when adding, updating, or deleting PLC tags.";
        public string Usage => "GETPLCTAGTABLES|<device-reference>";
        public string Example => "GETPLCTAGTABLES|DemoProject/PLC_1";
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

            var tables = CommandSupport.GetAllPlcTagTables(plcSoftware)
                .Select(table => string.Format("{0} [TagCount={1}]", table.TableReference, table.Table.Tags.Count))
                .ToList();

            return tables.Any()
                ? $"Device '{resolvedReference}' PLC tag tables: {string.Join(", ", tables)}"
                : $"Device '{resolvedReference}' has no PLC tag tables.";
        }
    }
}
