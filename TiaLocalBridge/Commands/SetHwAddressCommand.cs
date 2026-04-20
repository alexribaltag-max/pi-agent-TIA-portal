using System;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Commands
{
    internal class SetHwAddressCommand : ITiaCommand
    {
        public string Name => "SETHWADDRESS";
        public string Description => "Sets the start address of a hardware address entry on a device item. Use GETHWADDRESSES first to discover the available IO types and current addresses.";
        public string Usage => "SETHWADDRESS|<device-reference>|<target-reference>|<io-type>|<start-address>";
        public string Example => "SETHWADDRESS|DemoProject/PLC_1|0/2|Input|64";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-reference>", "<io-type>", "<start-address>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var targetResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var address = CommandSupport.ResolveAddress(targetResolution.TargetObject, providedArgs[2]);

            if (!int.TryParse(providedArgs[3], out int startAddress) || startAddress < 0)
            {
                throw new ArgumentException($"Invalid <start-address> '{providedArgs[3]}'. It must be a non-negative integer.");
            }

            var previousValue = address.StartAddress;

            try
            {
                address.SetAttribute("StartAddress", startAddress);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set start address for IO type '{address.IoType}' on {targetResolution.TargetKind} '{targetResolution.TargetReference}'. Details: {ex.Message}");
            }

            return $"Updated hardware address on {targetResolution.TargetKind} '{targetResolution.TargetReference}' for device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}' [IoType={address.IoType}, OldStartAddress={previousValue}, NewStartAddress={address.StartAddress}, Length={address.Length}].";
        }
    }
}
