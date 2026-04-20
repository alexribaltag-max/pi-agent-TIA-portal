using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetHmiConnectionPropertyCommand : ITiaCommand
    {
        public string Name => "SETHMICONNECTIONPROPERTY";
        public string Description => "Sets one writable Unified HMI connection property. Use GETHMICONNECTIONPROPERTIES first to inspect available connection properties.";
        public string Usage => "SETHMICONNECTIONPROPERTY|<device-reference>|<connection-name>|<property-name>|<value>";
        public string Example => "SETHMICONNECTIONPROPERTY|DemoProject/HMI_1|PLC_Connection|Comment|Created by bridge";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<connection-name>", "<property-name>", "<value>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var connection = CommandSupport.ResolveUnifiedHmiConnection(hmiSoftware, providedArgs[1]);
            return CommandSupport.SetNamedPropertyOrAttributeValue(
                connection,
                providedArgs[2],
                providedArgs[3],
                $"Unified HMI connection '{connection.Name}' on device '{resolvedReference}'");
        }
    }
}
