using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class AddHmiConnectionCommand : ITiaCommand
    {
        public string Name => "ADDHMICONNECTION";
        public string Description => "Adds a Unified HMI connection and sets the writable connection fields exposed directly by Openness. Use '-' for optional fields you want to leave at the TIA default value.";
        public string Usage => "ADDHMICONNECTION|<device-reference>|<connection-name>|<communication-driver>|<initial-address>|<disabled-at-startup>|<comment>";
        public string Example => "ADDHMICONNECTION|DemoProject/HMI_1|PLC_Connection|SIMATIC S7 1200/1500|-|false|-";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<connection-name>", "<communication-driver>", "<initial-address>", "<disabled-at-startup>", "<comment>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var connectionName = CommandSupport.RequireNewUnifiedHmiObjectName(hmiSoftware.Connections.Select(existingConnection => existingConnection.Name), "Unified HMI connection", providedArgs[1]);
            var communicationDriver = CommandSupport.NormalizeOptionalTextArgument(providedArgs[2]);
            var initialAddress = CommandSupport.NormalizeOptionalTextArgument(providedArgs[3]);
            var disabledAtStartupText = CommandSupport.NormalizeOptionalTextArgument(providedArgs[4]);
            var comment = CommandSupport.NormalizeOptionalTextArgument(providedArgs[5]);
            var connection = hmiSoftware.Connections.Create(connectionName);

            if (!string.IsNullOrWhiteSpace(communicationDriver))
            {
                connection.CommunicationDriver = communicationDriver;
            }

            if (!string.IsNullOrWhiteSpace(initialAddress))
            {
                connection.InitialAddress = initialAddress;
            }

            if (!string.IsNullOrWhiteSpace(disabledAtStartupText))
            {
                if (!bool.TryParse(disabledAtStartupText, out bool disabledAtStartup))
                {
                    throw new ArgumentException($"Invalid disabled-at-startup value '{disabledAtStartupText}'. Use true, false, or '-'.");
                }

                connection.DisabledAtStartup = disabledAtStartup;
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                connection.Comment = comment;
            }

            return string.Format(
                "Added Unified HMI connection '{0}' to '{1}' [Driver={2}, Station={3}, Partner={4}, Node={5}, InitialAddress={6}, DisabledAtStartup={7}].",
                connection.Name,
                resolvedReference,
                connection.CommunicationDriver ?? "<empty>",
                connection.Station ?? "<empty>",
                connection.Partner ?? "<empty>",
                connection.Node ?? "<empty>",
                connection.InitialAddress ?? "<empty>",
                connection.DisabledAtStartup ? "true" : "false");
        }
    }
}
