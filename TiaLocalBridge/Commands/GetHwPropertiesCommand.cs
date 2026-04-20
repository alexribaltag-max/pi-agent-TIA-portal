using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHwPropertiesCommand : ITiaCommand
    {
        public string Name => "GETHWPROPERTIES";
        public string Description => "Lists the readable hardware properties exposed by a device or device item. Use target reference DEVICE for device-level properties, or a device item reference returned by GETDEVICEITEMS for module-level properties.";
        public string Usage => "GETHWPROPERTIES|<device-reference>|<target-reference>";
        public string Example => "GETHWPROPERTIES|DemoProject/PLC_1|DEVICE";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-reference>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var targetResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var attributeSummaries = CommandSupport.GetWritableAndReadableAttributeInfos(targetResolution.EngineeringObject)
                .Select(info =>
                {
                    var currentValue = CommandSupport.TryGetAttributeValue(targetResolution.EngineeringObject, info.Name, out string readError);
                    var valueText = string.IsNullOrWhiteSpace(readError)
                        ? CommandSupport.FormatEngineeringValue(currentValue)
                        : $"<read-error:{readError}>";

                    return string.Format(
                        "{0} [Access={1}, Value={2}, SupportedTypes={3}]",
                        info.Name,
                        info.AccessMode,
                        valueText,
                        CommandSupport.DescribeSupportedTypes(info.SupportedTypes));
                })
                .ToList();

            return attributeSummaries.Any()
                ? $"Hardware properties for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}': {string.Join(" || ", attributeSummaries)}"
                : $"No hardware properties were exposed for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}'.";
        }
    }
}
