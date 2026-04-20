using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class AddHmiTagCommand : ITiaCommand
    {
        public string Name => "ADDHMITAG";
        public string Description => "Adds a Unified HMI tag to the specified Unified HMI tag table. Use '-' for optional connection or address values you want to leave empty.";
        public string Usage => "ADDHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>";
        public string Example => "ADDHMITAG|DemoProject/HMI_1|Default tag table|HmiSpeed|Int|DB10.DBW0|PLC_Connection";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<table-reference>", "<tag-name>", "<data-type>", "<address>", "<connection>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var table = CommandSupport.ResolveUnifiedHmiTagTable(hmiSoftware, providedArgs[1]);
            var tagName = CommandSupport.RequireNewUnifiedHmiObjectName(table.Table.Tags.Select(existingTag => existingTag.Name), "Unified HMI tag", providedArgs[2]);
            var dataType = providedArgs[3].Trim();
            var address = CommandSupport.NormalizeOptionalTextArgument(providedArgs[4]) ?? string.Empty;
            var connection = CommandSupport.NormalizeOptionalTextArgument(providedArgs[5]) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(dataType))
            {
                throw new ArgumentException("Unified HMI tag data type cannot be empty.");
            }

            var tag = table.Table.Tags.Create(tagName);
            tag.DataType = dataType;
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tag, "Address", address);
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tag, "Connection", connection);

            return $"Added Unified HMI tag '{tag.Name}' to '{resolvedReference}' table '{table.TableReference}' [DataType={tag.DataType}, Address={tag.Address ?? "<empty>"}, Connection={tag.Connection ?? "<empty>"}].";
        }
    }
}
