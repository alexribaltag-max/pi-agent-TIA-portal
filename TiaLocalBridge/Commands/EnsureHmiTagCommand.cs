using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class EnsureHmiTagCommand : ITiaCommand
    {
        public string Name => "ENSUREHMITAG";
        public string Description => "Creates a Unified HMI tag if it does not exist, or updates the existing tag in place. This makes retries safer for agent workflows, including internal tags that leave address and connection empty.";
        public string Usage => "ENSUREHMITAG|<device-reference>|<table-reference>|<tag-name>|<data-type>|<address>|<connection>";
        public string Example => "ENSUREHMITAG|DemoProject/HMI_1|Default tag table|HmiSpeed|Int|DB10.DBW0|PLC_Connection";
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
            var tagName = (providedArgs[2] ?? string.Empty).Trim();
            var dataType = (providedArgs[3] ?? string.Empty).Trim();
            var address = CommandSupport.NormalizeOptionalTextArgument(providedArgs[4]) ?? string.Empty;
            var connection = CommandSupport.NormalizeOptionalTextArgument(providedArgs[5]) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Unified HMI tag name cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(dataType))
            {
                throw new ArgumentException("Unified HMI tag data type cannot be empty.");
            }

            var existingTag = table.Table.Tags.FirstOrDefault(candidate => string.Equals(candidate.Name, tagName, StringComparison.OrdinalIgnoreCase));
            var created = existingTag == null;
            var tag = existingTag ?? table.Table.Tags.Create(tagName);

            var previousDataType = tag.DataType ?? string.Empty;
            var previousAddress = tag.Address ?? string.Empty;
            var previousConnection = tag.Connection ?? string.Empty;

            tag.DataType = dataType;
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tag, "Address", address);
            CommandSupport.SetOptionalUnifiedHmiTagTextProperty(tag, "Connection", connection);

            var updatedDataType = tag.DataType ?? string.Empty;
            var updatedAddress = tag.Address ?? string.Empty;
            var updatedConnection = tag.Connection ?? string.Empty;

            var unchanged = !created
                && string.Equals(previousDataType, updatedDataType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(previousAddress, updatedAddress, StringComparison.OrdinalIgnoreCase)
                && AreEquivalentUnifiedHmiConnections(previousConnection, updatedConnection, connection);

            var operation = created
                ? "created"
                : unchanged
                    ? "unchanged"
                    : "updated";

            return $"Ensured Unified HMI tag '{tag.Name}' in '{resolvedReference}' table '{table.TableReference}' [Operation={operation}, DataType={tag.DataType}, Address={tag.Address ?? "<empty>"}, Connection={tag.Connection ?? "<empty>"}].";
        }

        private static bool AreEquivalentUnifiedHmiConnections(string previousConnection, string updatedConnection, string requestedConnection)
        {
            var normalizedPrevious = NormalizeConnectionText(previousConnection);
            var normalizedUpdated = NormalizeConnectionText(updatedConnection);
            var normalizedRequested = NormalizeConnectionText(requestedConnection);

            if (string.Equals(normalizedPrevious, normalizedUpdated, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.IsNullOrEmpty(normalizedRequested)
                && string.IsNullOrEmpty(normalizedUpdated)
                && string.IsNullOrEmpty(normalizedPrevious);
        }

        private static string NormalizeConnectionText(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return string.Equals(normalized, "<Internal tag>", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalized;
        }
    }
}
