using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHwAddressesCommand : ITiaCommand
    {
        public string Name => "GETHWADDRESSES";
        public string Description => "Lists hardware addresses exposed by a device item, including IO type, start address, and length. This is useful for IO modules where address information is not exposed through generic hardware properties.";
        public string Usage => "GETHWADDRESSES|<device-reference>|<target-reference>";
        public string Example => "GETHWADDRESSES|DemoProject/PLC_1|0/2";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-reference>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var targetResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var addresses = CommandSupport.GetAddresses(targetResolution.TargetObject)
                .Select(address => string.Format(
                    "IoType={0}, StartAddress={1}, Length={2}",
                    address.IoType,
                    address.StartAddress,
                    address.Length))
                .ToList();

            return addresses.Any()
                ? $"Hardware addresses for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}': {string.Join(" || ", addresses)}"
                : $"No hardware addresses were exposed for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}'.";
        }
    }
}
