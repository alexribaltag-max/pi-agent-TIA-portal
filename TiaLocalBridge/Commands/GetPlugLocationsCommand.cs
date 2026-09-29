using System;
using System.Globalization;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Commands
{
    internal class GetPlugLocationsCommand : ITiaCommand
    {
        public string Name => "GETPLUGLOCATIONS";
        public string Description => "Lists the available plug locations for a device or device item so you can identify valid slots before inserting hardware modules. Use target reference DEVICE for the device root, or a device item reference returned by GETDEVICEITEMS.";
        public string Usage => "GETPLUGLOCATIONS|<device-reference>|<target-reference>";
        public string Example => "GETPLUGLOCATIONS|DemoProject/PLC_1|DEVICE";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<target-reference>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var targetResolution = CommandSupport.ResolveHardwareObject(deviceResolution.Device, providedArgs[1]);
            var plugLocationsTarget = targetResolution.TargetObject;
            var targetReference = targetResolution.TargetReference;
            var targetKind = targetResolution.TargetKind;

            // For rack-based stations, device-level plug locations describe rack slots, but
            // occupancy is represented by children of the rack DeviceItem rather than Device.Items.
            if (plugLocationsTarget is Device device)
            {
                var rootRacks = CommandSupport.GetDirectChildDeviceItems(device)
                    .Where(item => item.TypeIdentifier != null
                        && item.TypeIdentifier.ToString().StartsWith("System:Rack", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (rootRacks.Count == 1)
                {
                    var rack = rootRacks[0];
                    plugLocationsTarget = rack;
                    targetReference = rack.PositionNumber.ToString(CultureInfo.InvariantCulture);
                    targetKind = "Rack DeviceItem";
                }
            }

            var plugLocations = CommandSupport.GetPlugLocations(plugLocationsTarget);
            var occupiedPositions = CommandSupport.GetDirectChildDeviceItems(plugLocationsTarget)
                .GroupBy(item => item.PositionNumber)
                .ToDictionary(group => group.Key, group => group.First().Name);

            var summaries = plugLocations
                .OrderBy(location => location.PositionNumber)
                .Select(location => string.Format(
                    "Position={0}, Label={1}, Occupied={2}, Occupant={3}",
                    location.PositionNumber,
                    string.IsNullOrWhiteSpace(location.Label) ? "<no-label>" : location.Label,
                    occupiedPositions.ContainsKey(location.PositionNumber) ? "true" : "false",
                    occupiedPositions.TryGetValue(location.PositionNumber, out string occupantName) ? occupantName : "<empty>"))
                .ToList();

            return summaries.Any()
                ? $"Plug locations for {targetKind} '{targetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}': {string.Join(" || ", summaries)}"
                : $"No plug locations were exposed for {targetKind} '{targetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}'.";
        }
    }
}
