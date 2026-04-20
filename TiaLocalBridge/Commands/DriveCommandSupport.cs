using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering.HW;
using Siemens.Engineering.MC.Drives;
using Siemens.Engineering.MC.Drives.Enums;

namespace TiaLocalBridge.Commands
{
    internal sealed class DriveObjectResolution
    {
        public DeviceItemResolution ItemResolution { get; set; }
        public DriveObjectContainer Container { get; set; }
        public DriveObject DriveObject { get; set; }
    }

    internal static class DriveCommandSupport
    {
        public static List<DriveObjectResolution> GetAllDriveObjectResolutions(Device device)
        {
            var result = new List<DriveObjectResolution>();
            foreach (var itemResolution in CommandSupport.GetAllDeviceItemResolutions(device))
            {
                var container = itemResolution.Item.GetService<DriveObjectContainer>();
                if (container == null || container.DriveObjects == null)
                {
                    continue;
                }

                foreach (DriveObject driveObject in container.DriveObjects)
                {
                    if (driveObject == null)
                    {
                        continue;
                    }

                    result.Add(new DriveObjectResolution
                    {
                        ItemResolution = itemResolution,
                        Container = container,
                        DriveObject = driveObject
                    });
                }
            }

            return result;
        }

        public static DriveObjectResolution ResolveDriveObject(Device device, string deviceItemReference, string driveObjectNumberText)
        {
            if (string.IsNullOrWhiteSpace(deviceItemReference))
            {
                throw new ArgumentException("Drive device item reference cannot be empty. Use GETDEVICEITEMS or GETDRIVEOBJECTS to inspect the available drive-capable items.");
            }

            var itemResolution = CommandSupport.ResolveDeviceItem(device, deviceItemReference);
            var container = itemResolution.Item.GetService<DriveObjectContainer>();
            if (container == null)
            {
                throw new InvalidOperationException($"Device item '{itemResolution.ItemReference}' does not expose a DriveObjectContainer service. Use GETDRIVEOBJECTS to inspect which device items are drive-capable.");
            }

            var driveObjects = container.DriveObjects != null
                ? container.DriveObjects.Cast<DriveObject>().Where(candidate => candidate != null).ToList()
                : new List<DriveObject>();

            if (!driveObjects.Any())
            {
                throw new InvalidOperationException($"Device item '{itemResolution.ItemReference}' exposes a DriveObjectContainer, but no drive objects were found.");
            }

            DriveObject driveObject;
            if (string.IsNullOrWhiteSpace(driveObjectNumberText))
            {
                if (driveObjects.Count > 1)
                {
                    throw new InvalidOperationException($"Device item '{itemResolution.ItemReference}' contains multiple drive objects ({string.Join(", ", driveObjects.Select(candidate => candidate.DriveObjectNumber))}). Provide the optional <drive-object-number> argument.");
                }

                driveObject = driveObjects[0];
            }
            else
            {
                int driveObjectNumber;
                if (!int.TryParse(driveObjectNumberText.Trim(), out driveObjectNumber) || driveObjectNumber < 0)
                {
                    throw new ArgumentException($"Invalid <drive-object-number> '{driveObjectNumberText}'. It must be a non-negative integer.");
                }

                driveObject = driveObjects.FirstOrDefault(candidate => candidate.DriveObjectNumber == driveObjectNumber);
                if (driveObject == null)
                {
                    throw new InvalidOperationException($"Drive object number '{driveObjectNumber}' was not found on device item '{itemResolution.ItemReference}'. Available drive object numbers: {string.Join(", ", driveObjects.Select(candidate => candidate.DriveObjectNumber))}");
                }
            }

            return new DriveObjectResolution
            {
                ItemResolution = itemResolution,
                Container = container,
                DriveObject = driveObject
            };
        }

        public static TelegramType ParseTelegramType(string telegramTypeText)
        {
            if (string.IsNullOrWhiteSpace(telegramTypeText))
            {
                throw new ArgumentException("Telegram type cannot be empty. Supported values: MainTelegram, SafetyTelegram, SupplementaryTelegram, AdditionalTelegram, TorqueTelegram, EdgeTelegram.");
            }

            TelegramType telegramType;
            if (!Enum.TryParse(telegramTypeText.Trim(), true, out telegramType))
            {
                throw new ArgumentException($"Invalid telegram type '{telegramTypeText}'. Supported values: {string.Join(", ", Enum.GetNames(typeof(TelegramType)))}");
            }

            return telegramType;
        }

        public static Telegram ResolveDriveTelegram(DriveObject driveObject, string telegramTypeText)
        {
            var telegramType = ParseTelegramType(telegramTypeText);
            var telegram = driveObject.Telegrams.Find(telegramType);
            if (telegram == null)
            {
                throw new InvalidOperationException($"Drive object '{driveObject.DriveObjectNumber}' does not currently expose a telegram of type '{telegramType}'. Use SETDRIVETELEGRAMNUMBER to insert MainTelegram or SafetyTelegram when supported.");
            }

            return telegram;
        }

        public static Telegram EnsureDriveTelegramNumber(DriveObject driveObject, string telegramTypeText, int telegramNumber, out string action)
        {
            if (telegramNumber < 0)
            {
                throw new ArgumentException($"Invalid telegram number '{telegramNumber}'. It must be a non-negative integer.");
            }

            var telegramType = ParseTelegramType(telegramTypeText);
            var telegrams = driveObject.Telegrams;
            var telegram = telegrams.Find(telegramType);

            if (telegram != null)
            {
                if (telegram.TelegramNumber == telegramNumber)
                {
                    action = "unchanged";
                    return telegram;
                }

                if (!telegram.CanChangeTelegram(telegramNumber))
                {
                    throw new InvalidOperationException($"Drive object '{driveObject.DriveObjectNumber}' cannot change telegram type '{telegramType}' to number '{telegramNumber}'.");
                }

                telegram.TelegramNumber = telegramNumber;
                action = "changed";
                return telegrams.Find(telegramType) ?? telegram;
            }

            switch (telegramType)
            {
                case TelegramType.MainTelegram:
                    if (!telegrams.CanInsertMainTelegram(telegramNumber))
                    {
                        throw new InvalidOperationException($"Drive object '{driveObject.DriveObjectNumber}' cannot insert MainTelegram '{telegramNumber}'.");
                    }

                    telegrams.InsertMainTelegram(telegramNumber);
                    action = "inserted";
                    break;

                case TelegramType.SafetyTelegram:
                    if (!telegrams.CanInsertSafetyTelegram(telegramNumber))
                    {
                        throw new InvalidOperationException($"Drive object '{driveObject.DriveObjectNumber}' cannot insert SafetyTelegram '{telegramNumber}'.");
                    }

                    telegrams.InsertSafetyTelegram(telegramNumber);
                    action = "inserted";
                    break;

                default:
                    throw new InvalidOperationException($"Telegram type '{telegramType}' is missing on drive object '{driveObject.DriveObjectNumber}' and cannot be inserted through this command. Only MainTelegram and SafetyTelegram insertion are currently supported.");
            }

            var inserted = telegrams.Find(telegramType);
            if (inserted == null)
            {
                throw new InvalidOperationException($"Telegram type '{telegramType}' was expected after the insert operation, but it was not returned by Openness.");
            }

            return inserted;
        }

        public static Address ResolveDriveTelegramAddress(Telegram telegram, string ioTypeText)
        {
            if (string.IsNullOrWhiteSpace(ioTypeText))
            {
                throw new ArgumentException("Address IO type cannot be empty. Use Input, Output, Substitute, or Diagnosis.");
            }

            AddressIoType ioType;
            if (!Enum.TryParse(ioTypeText.Trim(), true, out ioType))
            {
                throw new ArgumentException($"Invalid IO type '{ioTypeText}'. Supported values: {string.Join(", ", Enum.GetNames(typeof(AddressIoType)))}");
            }

            var addresses = telegram.Addresses != null
                ? telegram.Addresses.Cast<Address>().Where(address => address != null).ToList()
                : new List<Address>();
            var match = addresses.FirstOrDefault(address => address.IoType == ioType);
            if (match == null)
            {
                throw new InvalidOperationException(
                    addresses.Any()
                        ? $"No telegram address with IO type '{ioType}' was found. Available IO types: {string.Join(", ", addresses.Select(address => address.IoType.ToString()).Distinct())}"
                        : "No addresses are exposed for the selected telegram.");
            }

            return match;
        }

        public static string FormatDriveTelegramSummary(Telegram telegram)
        {
            if (telegram == null)
            {
                return "<missing-telegram>";
            }

            var addressSummaries = telegram.Addresses != null
                ? telegram.Addresses.Cast<Address>()
                    .Where(address => address != null)
                    .Select(address => string.Format("{0}:{1}/Len={2}", address.IoType, address.StartAddress, address.Length))
                    .ToList()
                : new List<string>();

            return string.Format(
                "Type={0}, Number={1}, Addresses={2}",
                telegram.Type,
                telegram.TelegramNumber,
                addressSummaries.Any() ? string.Join(", ", addressSummaries) : "<none>");
        }
    }
}
