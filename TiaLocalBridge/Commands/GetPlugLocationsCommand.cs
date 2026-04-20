using System.Linq;
using Siemens.Engineering;

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
            var plugLocations = CommandSupport.GetPlugLocations(targetResolution.TargetObject);
            var occupiedPositions = CommandSupport.GetDirectChildDeviceItems(targetResolution.TargetObject)
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
                ? $"Plug locations for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}': {string.Join(" || ", summaries)}"
                : $"No plug locations were exposed for {targetResolution.TargetKind} '{targetResolution.TargetReference}' on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}'.";
        }
    }
}
