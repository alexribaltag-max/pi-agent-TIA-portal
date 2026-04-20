using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetDriveTelegramsCommand : ITiaCommand
    {
        public string Name => "GETDRIVETELEGRAMS";
        public string Description => "Lists the telegrams exposed by a drive object on a drive-capable device item. The optional drive object number is only needed when the item exposes multiple drive objects.";
        public string Usage => "GETDRIVETELEGRAMS|<device-reference>|<device-item-reference>|[drive-object-number]";
        public string Example => "GETDRIVETELEGRAMS|dano/Drive_U50_1|C18-VASP1|1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 2 || providedArgs.Length > 3)
            {
                throw new ArgumentException($"Expected 2 or 3 arguments. {Description} Usage: {Usage}. Example: {Example}");
            }

            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var driveResolution = DriveCommandSupport.ResolveDriveObject(deviceResolution.Device, providedArgs[1], providedArgs.Length > 2 ? providedArgs[2] : null);
            var telegramSummaries = driveResolution.DriveObject.Telegrams
                .Select(DriveCommandSupport.FormatDriveTelegramSummary)
                .ToList();

            return telegramSummaries.Any()
                ? $"Drive telegrams for device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}', item '{driveResolution.ItemResolution.ItemReference}', drive object '{driveResolution.DriveObject.DriveObjectNumber}': {string.Join(" || ", telegramSummaries)}"
                : $"No drive telegrams were exposed for device '{CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device)}', item '{driveResolution.ItemResolution.ItemReference}', drive object '{driveResolution.DriveObject.DriveObjectNumber}'.";
        }
    }
}
