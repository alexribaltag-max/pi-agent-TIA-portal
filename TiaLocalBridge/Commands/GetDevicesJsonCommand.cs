using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetDevicesJsonCommand : ITiaCommand
    {
        public string Name => "GETDEVICESJSON";
        public string Description => "Returns JSON details for the specified device reference. Use the [Reference=...] value returned by GETDEVICES, or use either '<device-name>' or '<project-name>/<device-name>'.";
        public string Usage => "GETDEVICESJSON|<device-reference>";
        public string Example => "GETDEVICESJSON|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var deviceItems = CommandSupport.GetAllDeviceItems(resolution.Device);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var hmiSoftware = CommandSupport.TryGetHmiSoftware(resolution.Device);

            return string.Format(
                "{{\"project\":\"{0}\",\"device\":{{\"reference\":\"{1}\",\"name\":\"{2}\",\"typeIdentifier\":\"{3}\",\"deviceItemCount\":{4},\"hasPlcSoftware\":{5},\"hasHmiSoftware\":{6}}}}}",
                CommandSupport.EscapeJson(resolution.Project.Name),
                CommandSupport.EscapeJson(CommandSupport.GetDeviceReference(resolution.Project, resolution.Device)),
                CommandSupport.EscapeJson(resolution.Device.Name),
                CommandSupport.EscapeJson(resolution.Device.TypeIdentifier),
                deviceItems.Count,
                plcSoftware != null ? "true" : "false",
                hmiSoftware != null ? "true" : "false");
        }
    }
}
