using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiConnectionPropertiesCommand : ITiaCommand
    {
        public string Name => "GETHMICONNECTIONPROPERTIES";
        public string Description => "Lists the public properties of one Unified HMI connection so you can inspect writable and read-only connection settings before changing them.";
        public string Usage => "GETHMICONNECTIONPROPERTIES|<device-reference>|<connection-name>";
        public string Example => "GETHMICONNECTIONPROPERTIES|DemoProject/HMI_1|PLC_Connection";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<connection-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var connection = CommandSupport.ResolveUnifiedHmiConnection(hmiSoftware, providedArgs[1]);
            var properties = CommandSupport.GetPublicPropertySummaries(connection, "Parent")
                .ToList();

            return properties.Any()
                ? $"Unified HMI connection properties for '{connection.Name}' on device '{resolvedReference}': {string.Join(" || ", properties)}"
                : $"No public Unified HMI connection properties were exposed for '{connection.Name}' on device '{resolvedReference}'.";
        }
    }
}
