using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiConnectionsCommand : ITiaCommand
    {
        public string Name => "GETHMICONNECTIONS";
        public string Description => "Lists all Unified HMI connections for the specified HMI device reference, including communication driver and addressing fields so you can inspect device configuration before editing it.";
        public string Usage => "GETHMICONNECTIONS|<device-reference>";
        public string Example => "GETHMICONNECTIONS|DemoProject/HMI_1";
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

            var connections = hmiSoftware.Connections
                .Select(connection => string.Format(
                    "{0} [Driver={1}, Station={2}, Partner={3}, Node={4}, InitialAddress={5}, DisabledAtStartup={6}]",
                    connection.Name,
                    connection.CommunicationDriver ?? "<empty>",
                    connection.Station ?? "<empty>",
                    connection.Partner ?? "<empty>",
                    connection.Node ?? "<empty>",
                    connection.InitialAddress ?? "<empty>",
                    connection.DisabledAtStartup ? "true" : "false"))
                .ToList();

            return connections.Any()
                ? $"Device '{resolvedReference}' Unified HMI connections: {string.Join(", ", connections)}"
                : $"Device '{resolvedReference}' has no Unified HMI connections.";
        }
    }
}
