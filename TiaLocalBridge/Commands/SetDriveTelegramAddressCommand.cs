using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetDriveTelegramAddressCommand : ITiaCommand
    {
        public string Name => "SETDRIVETELEGRAMADDRESS";
        public string Description => "Sets the start address of a specific IO direction on a drive telegram. Use GETDRIVETELEGRAMS first to inspect the current telegram and available addresses.";
        public string Usage => "SETDRIVETELEGRAMADDRESS|<device-reference>|<device-item-reference>|<telegram-type>|<io-type>|<start-address>|[drive-object-number]";
        public string Example => "SETDRIVETELEGRAMADDRESS|dano/Drive_U50_1|C18-VASP1|MainTelegram|Input|1000|1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 5 || providedArgs.Length > 6)
            {
                throw new ArgumentException($"Expected 5 or 6 arguments. {Description} Usage: {Usage}. Example: {Example}");
            }

            int startAddress;
            if (!int.TryParse(providedArgs[4], out startAddress) || startAddress < 0)
            {
                throw new ArgumentException($"Invalid <start-address> '{providedArgs[4]}'. It must be a non-negative integer.");
            }

            var deviceResolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var driveResolution = DriveCommandSupport.ResolveDriveObject(deviceResolution.Device, providedArgs[1], providedArgs.Length > 5 ? providedArgs[5] : null);
            var telegram = DriveCommandSupport.ResolveDriveTelegram(driveResolution.DriveObject, providedArgs[2]);
            var address = DriveCommandSupport.ResolveDriveTelegramAddress(telegram, providedArgs[3]);
            var previousValue = address.StartAddress;

            try
            {
                address.SetAttribute("StartAddress", startAddress);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set drive telegram address for IO type '{address.IoType}' on telegram type '{telegram.Type}'. Details: {ex.Message}");
            }

            return string.Format(
                "Drive telegram address updated on device '{0}', item '{1}', drive object '{2}' [TelegramType={3}, TelegramNumber={4}, IoType={5}, OldStartAddress={6}, NewStartAddress={7}, Length={8}].",
                CommandSupport.GetDeviceReference(deviceResolution.Project, deviceResolution.Device),
                driveResolution.ItemResolution.ItemReference,
                driveResolution.DriveObject.DriveObjectNumber,
                telegram.Type,
                telegram.TelegramNumber,
                address.IoType,
                previousValue,
                address.StartAddress,
                address.Length);
        }
    }
}
