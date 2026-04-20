using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class AddPlcTagCommand : ITiaCommand
    {
        public string Name => "ADDPLCTAG";
        public string Description => "Adds a PLC tag to the specified PLC tag table.";
        public string Usage => "ADDPLCTAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<logical-address>";
        public string Example => "ADDPLCTAG|DemoProject/PLC_1|Default tag table|MyTag|Bool|%M10.0";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<table-reference>", "<tag-name>", "<data-type>", "<logical-address>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var table = CommandSupport.ResolvePlcTagTable(plcSoftware, providedArgs[1]);
            var tagName = providedArgs[2].Trim();
            var dataTypeName = providedArgs[3].Trim();
            var logicalAddress = providedArgs[4].Trim();

            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("PLC tag name cannot be empty.");
            }

            if (table.Table.Tags.Any(tag => string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"PLC tag '{tagName}' already exists in table '{table.TableReference}'. Use UPDATEPLCTAG to modify it.");
            }

            var createdTag = table.Table.Tags.Create(tagName, dataTypeName, logicalAddress);
            return $"Added PLC tag '{createdTag.Name}' to '{resolvedReference}' table '{table.TableReference}' [DataType={createdTag.DataTypeName}, Address={createdTag.LogicalAddress}]";
        }
    }
}
