using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetDriveObjectsCommand : ITiaCommand
    {
        public string Name => "GETDRIVEOBJECTS";
        public string Description => "Lists the drive-capable device items on a device together with their drive object numbers and current telegram summary.";
        public string Usage => "GETDRIVEOBJECTS|<device-reference>";
        public string Example => "GETDRIVEOBJECTS|dano/Drive_U50_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var driveObjects = DriveCommandSupport.GetAllDriveObjectResolutions(deviceResolution.Device)
                .Select(resolution =>
                {
                    var telegramSummaries = resolution.DriveObject.Telegrams
                        .Select(DriveCommandSupport.FormatDriveTelegramSummary)
                        .ToList();

                    return string.Format(
                        "ItemReference={0}, ItemName={1}, DriveObjectNumber={2}, Telegrams={3}",
                        resolution.ItemResolution.ItemReference,
                        resolution.ItemResolution.Item.Name,
                        resolution.DriveObject.DriveObjectNumber,
                        telegramSummaries.Any() ? string.Join(" | ", telegramSummaries) : "<none>");
                })
                .ToList();

            return driveObjects.Any()
                ? $"Drive objects on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}': {string.Join(" || ", driveObjects)}"
                : $"No drive objects were found on device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}'.";
        }
    }
}
