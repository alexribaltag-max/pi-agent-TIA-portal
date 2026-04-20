using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class UpdateHmiTagCommand : ITiaCommand
    {
        public string Name => "UPDATEHMITAG";
        public string Description => "Updates the data type, address, and connection of an existing Unified HMI tag. Use '-' for address or connection if you want to clear them.";
        public string Usage => "UPDATEHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>";
        public string Example => "UPDATEHMITAG|DemoProject/HMI_1|Default tag table|HmiSpeed|Int|DB10.DBW2|PLC_Connection";
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

            var tagResolution = CommandSupport.ResolveUnifiedHmiTag(hmiSoftware, providedArgs[1], providedArgs[2]);
            var dataType = providedArgs[3].Trim();
            var address = CommandSupport.NormalizeOptionalTextArgument(providedArgs[4]) ?? string.Empty;
            var connection = CommandSupport.NormalizeOptionalTextArgument(providedArgs[5]) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(dataType))
            {
                throw new ArgumentException("Unified HMI tag data type cannot be empty.");
            }

            tagResolution.Tag.DataType = dataType;
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tagResolution.Tag, "Address", address);
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tagResolution.Tag, "Connection", connection);

            return $"Updated Unified HMI tag '{tagResolution.Tag.Name}' in '{resolvedReference}' table '{tagResolution.Table.TableReference}' [DataType={tagResolution.Tag.DataType}, Address={tagResolution.Tag.Address ?? "<empty>"}, Connection={tagResolution.Tag.Connection ?? "<empty>"}].";
        }
    }
}
