using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetDriveTelegramNumberCommand : ITiaCommand
    {
        public string Name => "SETDRIVETELEGRAMNUMBER";
        public string Description => "Sets or inserts a drive telegram number for the specified telegram type on a drive object. Missing telegram insertion is currently supported for MainTelegram and SafetyTelegram.";
        public string Usage => "SETDRIVETELEGRAMNUMBER|<device-reference>|<device-item-reference>|<telegram-type>|<telegram-number>|[drive-object-number]";
        public string Example => "SETDRIVETELEGRAMNUMBER|dano/Drive_U50_1|C18-VASP1|MainTelegram|352|1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 4 || providedArgs.Length > 5)
            {
                throw new ArgumentException($"Expected 4 or 5 arguments. {Description} Usage: {Usage}. Example: {Example}");
            }

            int telegramNumber;
            if (!int.TryParse(providedArgs[3], out telegramNumber) || telegramNumber < 0)
            {
                throw new ArgumentException($"Invalid <telegram-number> '{providedArgs[3]}'. It must be a non-negative integer.");
            }

            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var driveResolution = DriveCommandSupport.ResolveDriveObject(deviceResolution.Device, providedArgs[1], providedArgs.Length > 4 ? providedArgs[4] : null);
            var telegramType = DriveCommandSupport.ParseTelegramType(providedArgs[2]);
            var existingTelegram = driveResolution.DriveObject.Telegrams.Find(telegramType);
            var oldNumberText = existingTelegram != null ? existingTelegram.TelegramNumber.ToString() : "<missing>";
            string action;
            var updatedTelegram = DriveCommandSupport.EnsureDriveTelegramNumber(driveResolution.DriveObject, providedArgs[2], telegramNumber, out action);

            return string.Format(
                "Drive telegram updated on device '{0}', item '{1}', drive object '{2}' [TelegramType={3}, Action={4}, OldNumber={5}, NewNumber={6}].",
                CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device),
                driveResolution.ItemResolution.ItemReference,
                driveResolution.DriveObject.DriveObjectNumber,
                telegramType,
                action,
                oldNumberText,
                updatedTelegram.TelegramNumber);
        }
    }
}
