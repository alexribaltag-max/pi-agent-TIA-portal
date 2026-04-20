using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Drawing;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Extensions;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiConnections;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.ScreenGroup;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Shapes;
using Siemens.Engineering.HmiUnified.UI.Widgets;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

namespace TiaLocalBridge.Commands
{
    internal sealed class DeviceResolution
    {
        public Project Project { get; set; }
        public Device Device { get; set; }
    }

    internal sealed class DeviceItemResolution
    {
        public DeviceItem Item { get; set; }
        public DeviceItem ParentItem { get; set; }
        public string ItemReference { get; set; }
        public string ParentReference { get; set; }
        public string NamePath { get; set; }
    }

    internal sealed class HardwareObjectResolution
    {
        public object TargetObject { get; set; }
        public IEngineeringObject EngineeringObject { get; set; }
        public string TargetReference { get; set; }
        public string DisplayName { get; set; }
        public string TargetKind { get; set; }
    }

    internal sealed class PlcTagTableResolution
    {
        public PlcTagTable Table { get; set; }
        public string TableReference { get; set; }
    }

    internal sealed class PlcTagResolution
    {
        public PlcTagTableResolution Table { get; set; }
        public PlcTag Tag { get; set; }
    }

    internal sealed class PlcBlockResolution
    {
        public PlcBlock Block { get; set; }
        public string GroupReference { get; set; }
        public string BlockReference { get; set; }
    }

    internal sealed class PlcBlockGroupResolution
    {
        public PlcBlockComposition Blocks { get; set; }
        public string GroupReference { get; set; }
    }

    internal sealed class UnifiedHmiScreenGroupResolution
    {
        public HmiScreenGroup Group { get; set; }
        public string GroupReference { get; set; }
    }

    internal sealed class UnifiedHmiScreenResolution
    {
        public HmiScreen Screen { get; set; }
        public string GroupReference { get; set; }
        public string ScreenReference { get; set; }
    }

    internal sealed class UnifiedHmiTagTableResolution
    {
        public HmiTagTable Table { get; set; }
        public string GroupReference { get; set; }
        public string TableReference { get; set; }
    }

    internal sealed class UnifiedHmiTagResolution
    {
        public UnifiedHmiTagTableResolution Table { get; set; }
        public HmiTag Tag { get; set; }
    }

    internal static class CommandSupport
    {
        public static string[] GetProvidedArgs(string[] args)
        {
            return (args ?? Array.Empty<string>())
                .Where(arg => !string.IsNullOrWhiteSpace(arg))
                .Select(arg => arg.Trim())
                .ToArray();
        }

        public static void RequireNoArguments(string[] args, ITiaCommand command)
        {
            var providedArgs = GetProvidedArgs(args);
            if (providedArgs.Length != 0)
            {
                throw new ArgumentException($"This command does not accept arguments. {command.Description} Usage: {command.Usage}. Example: {command.Example}");
            }
        }

        public static string RequireSingleArgument(string[] args, ITiaCommand command, string argumentName)
        {
            return RequireExactArguments(args, command, argumentName)[0];
        }

        public static string[] RequireExactArguments(string[] args, ITiaCommand command, params string[] argumentNames)
        {
            var providedArgs = GetProvidedArgs(args);
            if (providedArgs.Length != argumentNames.Length)
            {
                throw new ArgumentException($"Expected exactly {argumentNames.Length} argument(s) ({string.Join(", ", argumentNames)}). {command.Description} Usage: {command.Usage}. Example: {command.Example}");
            }

            return providedArgs;
        }

        public static DeviceResolution ResolveDeviceByReference(TiaPortal portal, string deviceReference)
        {
            var openProjects = portal.Projects.ToList();
            if (!openProjects.Any())
            {
                throw new InvalidOperationException("No open projects. Open or create a project first.");
            }

            var normalizedReference = NormalizeDeviceReference(deviceReference);

            if (TryResolveProjectQualifiedDevice(openProjects, normalizedReference, out DeviceResolution qualifiedResolution))
            {
                return qualifiedResolution;
            }

            var matches = openProjects
                .SelectMany(project => project.Devices.Select(device => new DeviceResolution
                {
                    Project = project,
                    Device = device
                }))
                .Where(match => string.Equals(match.Device.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!matches.Any())
            {
                var availableDevices = GetAvailableDeviceReferences(openProjects);
                throw new InvalidOperationException(
                    availableDevices.Any()
                        ? $"Device reference '{normalizedReference}' was not found. Use one of these references from GETDEVICES: {string.Join(", ", availableDevices)}"
                        : "There are open projects, but no devices were found.");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple devices named '{normalizedReference}' were found in open projects: {string.Join(", ", matches.Select(m => m.Project.Name))}. Use the project-qualified reference returned by GETDEVICES: <project-name>/<device-name>.");
            }

            return matches[0];
        }

        public static string GetDeviceReference(Project project, Device device)
        {
            return $"{project.Name}/{device.Name}";
        }

        public static List<DeviceItem> GetAllDeviceItems(Device device)
        {
            var result = new List<DeviceItem>();
            foreach (DeviceItem item in device.Items)
            {
                AddDeviceItemRecursive(item, result);
            }
            return result;
        }

        public static List<DeviceItemResolution> GetAllDeviceItemResolutions(Device device)
        {
            var result = new List<DeviceItemResolution>();
            foreach (DeviceItem item in device.Items)
            {
                AddDeviceItemResolutionRecursive(item, null, null, null, result);
            }

            return result;
        }

        public static DeviceItemResolution ResolveDeviceItem(Device device, string itemReference)
        {
            if (string.IsNullOrWhiteSpace(itemReference))
            {
                throw new ArgumentException("Device item reference cannot be empty. Use GETDEVICEITEMS to inspect the available item references.");
            }

            var normalizedReference = itemReference.Trim();
            var allItems = GetAllDeviceItemResolutions(device);

            var exactReferenceMatch = allItems.FirstOrDefault(candidate => string.Equals(candidate.ItemReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactReferenceMatch != null)
            {
                return exactReferenceMatch;
            }

            var exactNamePathMatch = allItems.FirstOrDefault(candidate => string.Equals(candidate.NamePath, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactNamePathMatch != null)
            {
                return exactNamePathMatch;
            }

            var nameMatches = allItems
                .Where(candidate => string.Equals(candidate.Item.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allItems.Any()
                        ? $"Device item reference '{normalizedReference}' was not found. Use one of these item references from GETDEVICEITEMS: {string.Join(", ", allItems.Select(candidate => candidate.ItemReference))}"
                        : "The device has no device items.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple device items named '{normalizedReference}' were found. Use the exact item reference from GETDEVICEITEMS: {string.Join(", ", nameMatches.Select(candidate => candidate.ItemReference))}");
            }

            return nameMatches[0];
        }

        public static HardwareObjectResolution ResolveHardwareObject(Device device, string targetReference)
        {
            if (string.IsNullOrWhiteSpace(targetReference) || string.Equals(targetReference.Trim(), "DEVICE", StringComparison.OrdinalIgnoreCase))
            {
                return new HardwareObjectResolution
                {
                    TargetObject = device,
                    EngineeringObject = (IEngineeringObject)device,
                    TargetReference = "DEVICE",
                    DisplayName = device.Name,
                    TargetKind = "Device"
                };
            }

            var itemResolution = ResolveDeviceItem(device, targetReference);
            return new HardwareObjectResolution
            {
                TargetObject = itemResolution.Item,
                EngineeringObject = (IEngineeringObject)itemResolution.Item,
                TargetReference = itemResolution.ItemReference,
                DisplayName = itemResolution.Item.Name,
                TargetKind = "DeviceItem"
            };
        }

        public static List<PlugLocation> GetPlugLocations(object targetObject)
        {
            if (targetObject is Device device)
            {
                var locations = device.GetPlugLocations();
                return locations != null ? locations.Where(location => location != null).ToList() : new List<PlugLocation>();
            }

            if (targetObject is DeviceItem deviceItem)
            {
                var locations = deviceItem.GetPlugLocations();
                return locations != null ? locations.Where(location => location != null).ToList() : new List<PlugLocation>();
            }

            throw new InvalidOperationException("Plug locations are only available for devices and device items.");
        }

        public static List<DeviceItem> GetDirectChildDeviceItems(object targetObject)
        {
            if (targetObject is Device device)
            {
                return device.Items != null ? device.Items.Cast<DeviceItem>().Where(item => item != null).ToList() : new List<DeviceItem>();
            }

            if (targetObject is DeviceItem deviceItem)
            {
                return deviceItem.Items != null ? deviceItem.Items.Cast<DeviceItem>().Where(item => item != null).ToList() : new List<DeviceItem>();
            }

            throw new InvalidOperationException("Direct child device items are only available for devices and device items.");
        }

        public static List<Address> GetAddresses(object targetObject)
        {
            if (targetObject is Device device)
            {
                return GetAllDeviceItems(device)
                    .SelectMany(item => item.Addresses != null ? item.Addresses.Cast<Address>() : Enumerable.Empty<Address>())
                    .Where(address => address != null)
                    .ToList();
            }

            if (targetObject is DeviceItem deviceItem)
            {
                return deviceItem.Addresses != null
                    ? deviceItem.Addresses.Cast<Address>().Where(address => address != null).ToList()
                    : new List<Address>();
            }

            throw new InvalidOperationException("Addresses are only available for devices and device items.");
        }

        public static Address ResolveAddress(object targetObject, string ioTypeText)
        {
            if (string.IsNullOrWhiteSpace(ioTypeText))
            {
                throw new ArgumentException("Address IO type cannot be empty. Use Input, Output, Substitute, or Diagnosis.");
            }

            if (!Enum.TryParse(ioTypeText.Trim(), true, out AddressIoType ioType))
            {
                throw new ArgumentException($"Invalid IO type '{ioTypeText}'. Supported values: {string.Join(", ", Enum.GetNames(typeof(AddressIoType)))}");
            }

            var addresses = GetAddresses(targetObject);
            var match = addresses.FirstOrDefault(address => address.IoType == ioType);
            if (match == null)
            {
                throw new InvalidOperationException(
                    addresses.Any()
                        ? $"No address with IO type '{ioType}' was found. Available IO types: {string.Join(", ", addresses.Select(address => address.IoType.ToString()).Distinct())}"
                        : "No addresses are exposed for the selected hardware object.");
            }

            return match;
        }

        public static DeviceItem PlugNewModule(object targetObject, string typeIdentifier, string name, int positionNumber)
        {
            if (string.IsNullOrWhiteSpace(typeIdentifier))
            {
                throw new ArgumentException("Type identifier cannot be empty. Use SEARCHHWCATALOG to find the exact catalog type identifier.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Module name cannot be empty.");
            }

            if (positionNumber <= 0)
            {
                throw new ArgumentException("Position number must be greater than zero. Use GETPLUGLOCATIONS to inspect the valid target slots.");
            }

            if (targetObject is Device device)
            {
                if (!device.CanPlugNew(typeIdentifier, name, positionNumber))
                {
                    throw new InvalidOperationException($"The device does not allow plugging type '{typeIdentifier}' at position {positionNumber}. Use GETPLUGLOCATIONS and SEARCHHWCATALOG to inspect compatible slots and module types.");
                }

                return device.PlugNew(typeIdentifier, name, positionNumber);
            }

            if (targetObject is DeviceItem deviceItem)
            {
                if (!deviceItem.CanPlugNew(typeIdentifier, name, positionNumber))
                {
                    throw new InvalidOperationException($"The device item '{deviceItem.Name}' does not allow plugging type '{typeIdentifier}' at position {positionNumber}. Use GETPLUGLOCATIONS and SEARCHHWCATALOG to inspect compatible slots and module types.");
                }

                return deviceItem.PlugNew(typeIdentifier, name, positionNumber);
            }

            throw new InvalidOperationException("Modules can only be plugged into a device or device item.");
        }

        public static IList<EngineeringAttributeInfo> GetWritableAndReadableAttributeInfos(IEngineeringObject engineeringObject)
        {
            return engineeringObject
                .GetAttributeInfos()
                .OfType<EngineeringAttributeInfo>()
                .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static EngineeringAttributeInfo ResolveAttributeInfo(IEngineeringObject engineeringObject, string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new ArgumentException("Attribute name cannot be empty.");
            }

            var normalizedName = attributeName.Trim();
            var attributeInfos = GetWritableAndReadableAttributeInfos(engineeringObject);
            var attributeInfo = attributeInfos.FirstOrDefault(info => string.Equals(info.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (attributeInfo == null)
            {
                throw new InvalidOperationException(
                    attributeInfos.Any()
                        ? $"Hardware property '{normalizedName}' was not found. Available properties: {string.Join(", ", attributeInfos.Select(info => info.Name))}"
                        : "No hardware properties are exposed for the selected target.");
            }

            return attributeInfo;
        }

        public static object TryGetAttributeValue(IEngineeringObject engineeringObject, string attributeName, out string error)
        {
            error = null;

            try
            {
                return engineeringObject.GetAttribute(attributeName);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        public static object ConvertTextToAttributeValue(IEngineeringObject engineeringObject, EngineeringAttributeInfo attributeInfo, string rawValue)
        {
            var candidateTypes = new List<Type>();
            var currentValue = TryGetAttributeValue(engineeringObject, attributeInfo.Name, out _);
            if (currentValue != null)
            {
                candidateTypes.Add(currentValue.GetType());
            }

            if (attributeInfo.SupportedTypes != null)
            {
                candidateTypes.AddRange(attributeInfo.SupportedTypes.Where(type => type != null));
            }

            var orderedCandidateTypes = candidateTypes
                .Distinct()
                .OrderBy(type => type == typeof(string) ? 1 : 0)
                .ToList();

            foreach (var candidateType in orderedCandidateTypes)
            {
                if (TryConvertTextValue(rawValue, candidateType, out object convertedValue))
                {
                    return convertedValue;
                }
            }

            if (TryConvertTextValue(rawValue, typeof(string), out object stringValue))
            {
                return stringValue;
            }

            throw new InvalidOperationException($"Unable to convert '{rawValue}' to a valid value for hardware property '{attributeInfo.Name}'. Supported types: {DescribeSupportedTypes(attributeInfo.SupportedTypes)}");
        }

        public static string DescribeSupportedTypes(IEnumerable<Type> supportedTypes)
        {
            var typeNames = (supportedTypes ?? Enumerable.Empty<Type>())
                .Where(type => type != null)
                .Select(GetFriendlyTypeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return typeNames.Any() ? string.Join("|", typeNames) : "<unknown>";
        }

        public static string FormatEngineeringValue(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            if (value is string text)
            {
                return string.IsNullOrEmpty(text) ? "<empty>" : text;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToString("o", CultureInfo.InvariantCulture);
            }

            if (value is bool boolean)
            {
                return boolean ? "true" : "false";
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        public static PlcSoftware TryGetPlcSoftware(Device device)
        {
            var software = TryGetSoftware(device);
            return software as PlcSoftware;
        }

        public static object TryGetHmiSoftware(Device device)
        {
            var software = TryGetSoftware(device);
            if (software is HmiTarget || software is HmiSoftware)
            {
                return software;
            }

            return null;
        }

        public static HmiSoftware TryGetUnifiedHmiSoftware(Device device)
        {
            return TryGetSoftware(device) as HmiSoftware;
        }

        public static List<UnifiedHmiTagTableResolution> GetAllUnifiedHmiTagTables(HmiSoftware hmiSoftware)
        {
            var result = new List<UnifiedHmiTagTableResolution>();

            foreach (HmiTagTable table in hmiSoftware.TagTables)
            {
                result.Add(new UnifiedHmiTagTableResolution
                {
                    Table = table,
                    GroupReference = "<root>",
                    TableReference = table.Name
                });
            }

            AddUnifiedHmiTagTablesRecursive(hmiSoftware.TagTableGroups, null, result);
            return result;
        }

        public static UnifiedHmiTagTableResolution ResolveUnifiedHmiTagTable(HmiSoftware hmiSoftware, string tableReference)
        {
            if (string.IsNullOrWhiteSpace(tableReference))
            {
                throw new ArgumentException("Unified HMI tag table reference cannot be empty.");
            }

            var normalizedReference = tableReference.Trim();
            var allTables = GetAllUnifiedHmiTagTables(hmiSoftware);

            var exactMatch = allTables.FirstOrDefault(table => string.Equals(table.TableReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var nameMatches = allTables
                .Where(table => string.Equals(table.Table.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allTables.Any()
                        ? $"Unified HMI tag table '{normalizedReference}' was not found. Available tag tables: {string.Join(", ", allTables.Select(table => table.TableReference))}"
                        : "The Unified HMI has no tag tables.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple Unified HMI tag tables named '{normalizedReference}' were found. Use the full table reference: {string.Join(", ", nameMatches.Select(table => table.TableReference))}");
            }

            return nameMatches[0];
        }

        public static List<UnifiedHmiTagResolution> GetAllUnifiedHmiTags(HmiSoftware hmiSoftware)
        {
            return GetAllUnifiedHmiTagTables(hmiSoftware)
                .SelectMany(table => table.Table.Tags.Select(tag => new UnifiedHmiTagResolution
                {
                    Table = table,
                    Tag = tag
                }))
                .ToList();
        }

        public static UnifiedHmiTagResolution ResolveUnifiedHmiTag(HmiSoftware hmiSoftware, string tableReference, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Unified HMI tag name cannot be empty.");
            }

            var table = ResolveUnifiedHmiTagTable(hmiSoftware, tableReference);
            var normalizedTagName = tagName.Trim();
            var tag = table.Table.Tags.FirstOrDefault(candidate => string.Equals(candidate.Name, normalizedTagName, StringComparison.OrdinalIgnoreCase));
            if (tag == null)
            {
                var availableTags = table.Table.Tags.Select(candidate => candidate.Name).ToList();
                throw new InvalidOperationException(
                    availableTags.Any()
                        ? $"Unified HMI tag '{normalizedTagName}' was not found in table '{table.TableReference}'. Available tags: {string.Join(", ", availableTags)}"
                        : $"Unified HMI tag table '{table.TableReference}' does not contain any tags.");
            }

            return new UnifiedHmiTagResolution
            {
                Table = table,
                Tag = tag
            };
        }

        public static UnifiedHmiTagResolution ResolveUnifiedHmiTagByName(HmiSoftware hmiSoftware, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Unified HMI tag name cannot be empty.");
            }

            var normalizedTagName = tagName.Trim();
            var allTags = GetAllUnifiedHmiTags(hmiSoftware);
            var matches = allTags
                .Where(candidate => string.Equals(candidate.Tag.Name, normalizedTagName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!matches.Any())
            {
                var availableTags = allTags
                    .Select(candidate => candidate.Tag.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                throw new InvalidOperationException(
                    availableTags.Any()
                        ? $"Unified HMI tag '{normalizedTagName}' was not found. Available tags: {string.Join(", ", availableTags)}"
                        : "The Unified HMI has no tags.");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple Unified HMI tags named '{normalizedTagName}' were found. Use a unique tag name. Matching tables: {string.Join(", ", matches.Select(candidate => candidate.Table.TableReference))}");
            }

            return matches[0];
        }

        public static List<UnifiedHmiScreenGroupResolution> GetAllUnifiedHmiScreenGroups(HmiSoftware hmiSoftware)
        {
            var result = new List<UnifiedHmiScreenGroupResolution>();
            AddUnifiedHmiScreenGroupsRecursive(hmiSoftware.ScreenGroups, null, result);
            return result;
        }

        public static UnifiedHmiScreenGroupResolution ResolveUnifiedHmiScreenGroup(HmiSoftware hmiSoftware, string groupReference)
        {
            if (string.IsNullOrWhiteSpace(groupReference) || string.Equals(groupReference.Trim(), "<root>", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalizedReference = groupReference.Trim();
            var allGroups = GetAllUnifiedHmiScreenGroups(hmiSoftware);

            var exactMatch = allGroups.FirstOrDefault(group => string.Equals(group.GroupReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var nameMatches = allGroups
                .Where(group => string.Equals(GetLastPathSegment(group.GroupReference), normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allGroups.Any()
                        ? $"Unified HMI screen group '{normalizedReference}' was not found. Available screen groups: {string.Join(", ", allGroups.Select(group => group.GroupReference))}"
                        : "The Unified HMI has no screen groups.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple Unified HMI screen groups named '{normalizedReference}' were found. Use the full group reference: {string.Join(", ", nameMatches.Select(group => group.GroupReference))}");
            }

            return nameMatches[0];
        }

        public static List<UnifiedHmiScreenResolution> GetAllUnifiedHmiScreens(HmiSoftware hmiSoftware)
        {
            var result = new List<UnifiedHmiScreenResolution>();

            foreach (HmiScreen screen in hmiSoftware.Screens)
            {
                result.Add(new UnifiedHmiScreenResolution
                {
                    Screen = screen,
                    GroupReference = "<root>",
                    ScreenReference = screen.Name
                });
            }

            AddUnifiedHmiScreensRecursive(hmiSoftware.ScreenGroups, null, result);
            return result;
        }

        public static UnifiedHmiScreenResolution ResolveUnifiedHmiScreen(HmiSoftware hmiSoftware, string screenReference)
        {
            if (string.IsNullOrWhiteSpace(screenReference))
            {
                throw new ArgumentException("Unified HMI screen reference cannot be empty.");
            }

            var normalizedReference = screenReference.Trim();
            var allScreens = GetAllUnifiedHmiScreens(hmiSoftware);

            var exactMatch = allScreens.FirstOrDefault(screen => string.Equals(screen.ScreenReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var nameMatches = allScreens
                .Where(screen => string.Equals(screen.Screen.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allScreens.Any()
                        ? $"Unified HMI screen '{normalizedReference}' was not found. Available screens: {string.Join(", ", allScreens.Select(screen => screen.ScreenReference))}"
                        : "The Unified HMI has no screens.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple Unified HMI screens named '{normalizedReference}' were found. Use the full screen reference: {string.Join(", ", nameMatches.Select(screen => screen.ScreenReference))}");
            }

            return nameMatches[0];
        }

        public static HmiScreenItemBase FindUnifiedHmiScreenItem(HmiScreen screen, string itemName)
        {
            if (screen == null || string.IsNullOrWhiteSpace(itemName))
            {
                return null;
            }

            var normalizedName = itemName.Trim();
            return screen.ScreenItems
                .ToList()
                .FirstOrDefault(item => string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        public static HmiScreenItemBase ResolveUnifiedHmiScreenItem(HmiScreen screen, string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                throw new ArgumentException("Unified HMI screen item name cannot be empty.");
            }

            var normalizedName = itemName.Trim();
            var items = screen.ScreenItems.ToList();
            var exactMatch = items.FirstOrDefault(item => string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            throw new InvalidOperationException(
                items.Any()
                    ? $"Unified HMI screen item '{normalizedName}' was not found. Available items: {string.Join(", ", items.Select(item => item.Name))}"
                    : "The Unified HMI screen has no items.");
        }

        public static HmiConnection ResolveUnifiedHmiConnection(HmiSoftware hmiSoftware, string connectionName)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
            {
                throw new ArgumentException("Unified HMI connection name cannot be empty.");
            }

            var normalizedName = connectionName.Trim();
            var connections = hmiSoftware.Connections.ToList();
            var exactMatch = connections.FirstOrDefault(connection => string.Equals(connection.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            throw new InvalidOperationException(
                connections.Any()
                    ? $"Unified HMI connection '{normalizedName}' was not found. Available connections: {string.Join(", ", connections.Select(connection => connection.Name))}"
                    : "The Unified HMI has no configured connections.");
        }

        public static string RequireNewUnifiedHmiObjectName(IEnumerable<string> existingNames, string objectKind, string requestedName)
        {
            var normalizedName = (requestedName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new ArgumentException($"{objectKind} name cannot be empty.");
            }

            if (normalizedName.Contains("/"))
            {
                throw new ArgumentException($"{objectKind} name cannot contain '/'. Use only the final object name, not a full reference path.");
            }

            if ((existingNames ?? Enumerable.Empty<string>()).Any(name => string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A {objectKind.ToLowerInvariant()} named '{normalizedName}' already exists. Choose a different name.");
            }

            return normalizedName;
        }

        public static IReadOnlyDictionary<string, Type> GetSupportedUnifiedHmiScreenItemTypes()
        {
            return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "LABEL", typeof(HmiLabel) },
                { "BUTTON", typeof(HmiButton) },
                { "IOFIELD", typeof(HmiIOField) },
                { "RECTANGLE", typeof(HmiRectangle) },
                { "TEXT", typeof(HmiText) },
                { "LINE", typeof(HmiLine) },
                { "ELLIPSE", typeof(HmiEllipse) },
                { "TEXTBOX", typeof(HmiTextBox) },
                { "SYMBOLICIOFIELD", typeof(HmiSymbolicIOField) },
                { "GRAPHICVIEW", typeof(HmiGraphicView) }
            };
        }

        public static HmiScreenItemBase CreateUnifiedHmiScreenItem(HmiScreen screen, string itemTypeText, string itemName)
        {
            var supportedTypes = GetSupportedUnifiedHmiScreenItemTypes();
            if (!supportedTypes.TryGetValue((itemTypeText ?? string.Empty).Trim(), out Type itemType))
            {
                throw new ArgumentException($"Unsupported Unified HMI screen item type '{itemTypeText}'. Supported types: {string.Join(", ", supportedTypes.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}");
            }

            var normalizedItemName = RequireNewUnifiedHmiObjectName(screen.ScreenItems.Select(item => item.Name), "Unified HMI screen item", itemName);
            var createMethod = screen.ScreenItems
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                    string.Equals(method.Name, "Create", StringComparison.OrdinalIgnoreCase)
                    && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));

            if (createMethod == null)
            {
                throw new InvalidOperationException("The Unified HMI screen item creation API is not available in this TIA Portal environment.");
            }

            try
            {
                var genericCreateMethod = createMethod.MakeGenericMethod(itemType);
                return (HmiScreenItemBase)genericCreateMethod.Invoke(screen.ScreenItems, new object[] { normalizedItemName });
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException($"Failed to create Unified HMI screen item '{normalizedItemName}' of type '{itemType.Name}'. Details: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public static string FormatUnifiedHmiScreenItemSummary(HmiScreenItemBase item, string screenReference)
        {
            var itemReference = string.IsNullOrWhiteSpace(screenReference)
                ? item.Name
                : $"{screenReference}/{item.Name}";
            var left = TryGetPublicPropertyText(item, "Left");
            var top = TryGetPublicPropertyText(item, "Top");
            var width = TryGetPublicPropertyText(item, "Width");
            var height = TryGetPublicPropertyText(item, "Height");
            var text = TryGetPublicPropertyText(item, "Text");
            var textSuffix = string.IsNullOrWhiteSpace(text) ? string.Empty : $", Text={text}";

            return $"{item.Name} [Reference={itemReference}, Type={item.GetType().Name}, Left={left}, Top={top}, Width={width}, Height={height}{textSuffix}]";
        }

        public static void SetPublicPropertyValue(object instance, string propertyName, object value)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                throw new InvalidOperationException($"Property '{propertyName}' is not writable on type '{instance.GetType().Name}'.");
            }

            property.SetValue(instance, value, null);
        }

        public static void ApplyUnifiedHmiScreenItemGeometry(HmiScreenItemBase item, int left, int top, uint width, uint height)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var itemType = item.GetType();
            var leftProperty = itemType.GetProperty("Left", BindingFlags.Public | BindingFlags.Instance);
            var topProperty = itemType.GetProperty("Top", BindingFlags.Public | BindingFlags.Instance);
            var widthProperty = itemType.GetProperty("Width", BindingFlags.Public | BindingFlags.Instance);
            var heightProperty = itemType.GetProperty("Height", BindingFlags.Public | BindingFlags.Instance);

            if (leftProperty?.CanWrite == true && topProperty?.CanWrite == true && widthProperty?.CanWrite == true && heightProperty?.CanWrite == true)
            {
                leftProperty.SetValue(item, left, null);
                topProperty.SetValue(item, top, null);
                widthProperty.SetValue(item, width, null);
                heightProperty.SetValue(item, height, null);
                return;
            }

            if (item is HmiEllipse ellipse)
            {
                ellipse.CenterX = left + (int)(width / 2u);
                ellipse.CenterY = top + (int)(height / 2u);
                ellipse.RadiusX = width / 2u;
                ellipse.RadiusY = height / 2u;
                return;
            }

            throw new InvalidOperationException($"The Unified HMI screen item type '{itemType.Name}' does not expose a supported geometry model for Left/Top/Width/Height placement.");
        }

        public static IEnumerable<DynamizationBase> GetUnifiedHmiDynamizations(object instance)
        {
            if (instance == null)
            {
                return Enumerable.Empty<DynamizationBase>();
            }

            var property = instance.GetType().GetProperty("Dynamizations", BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanRead)
            {
                return Enumerable.Empty<DynamizationBase>();
            }

            return EnumerateComposition(property.GetValue(instance, null)).OfType<DynamizationBase>().ToList();
        }

        public static TagDynamization EnsureUnifiedHmiTagDynamization(object instance, string propertyName)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Unified HMI dynamization property name cannot be empty.");
            }

            var normalizedPropertyName = propertyName.Trim();
            var existingDynamization = GetUnifiedHmiDynamizations(instance)
                .FirstOrDefault(dynamization => string.Equals(dynamization.PropertyName, normalizedPropertyName, StringComparison.OrdinalIgnoreCase));

            if (existingDynamization is TagDynamization existingTagDynamization)
            {
                return existingTagDynamization;
            }

            if (existingDynamization != null)
            {
                throw new InvalidOperationException($"Property '{normalizedPropertyName}' on '{instance.GetType().Name}' already has a non-tag dynamization of type '{existingDynamization.DynamizationType}'.");
            }

            var dynamizationsProperty = instance.GetType().GetProperty("Dynamizations", BindingFlags.Public | BindingFlags.Instance);
            if (dynamizationsProperty == null || !dynamizationsProperty.CanRead)
            {
                throw new InvalidOperationException($"Type '{instance.GetType().Name}' does not expose a Dynamizations collection.");
            }

            var dynamizations = dynamizationsProperty.GetValue(instance, null);
            if (dynamizations == null)
            {
                throw new InvalidOperationException($"The Dynamizations collection is not available on type '{instance.GetType().Name}'.");
            }

            var createMethod = dynamizations
                .GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                    string.Equals(method.Name, "Create", StringComparison.OrdinalIgnoreCase)
                    && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));

            if (createMethod == null)
            {
                throw new InvalidOperationException($"The Unified HMI tag dynamization creation API is not available on type '{dynamizations.GetType().Name}'.");
            }

            try
            {
                var genericCreateMethod = createMethod.MakeGenericMethod(typeof(TagDynamization));
                return (TagDynamization)genericCreateMethod.Invoke(dynamizations, new object[] { normalizedPropertyName });
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException($"Failed to create a Unified HMI tag dynamization for property '{normalizedPropertyName}'. Details: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public static string NormalizeUnifiedHmiTextMarkup(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            var normalized = text.Trim();
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            if (normalized.StartsWith("<body", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("<p", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var paragraphs = normalized
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None)
                .Select(line => $"<p>{SecurityElement.Escape(line ?? string.Empty)}</p>");

            return $"<body>{string.Join(string.Empty, paragraphs)}</body>";
        }

        public static void TrySetMultilingualTextProperty(object instance, string propertyName, string text)
        {
            if (instance == null)
            {
                return;
            }

            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanRead)
            {
                return;
            }

            var multilingualText = property.GetValue(instance, null) as MultilingualText;
            if (multilingualText == null)
            {
                return;
            }

            var firstItem = multilingualText.Items.Cast<MultilingualTextItem>().FirstOrDefault();
            if (firstItem != null)
            {
                firstItem.Text = NormalizeUnifiedHmiTextMarkup(text);
            }
        }

        public static string NormalizeOptionalTextArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            return string.Equals(normalized, "-", StringComparison.Ordinal) ? null : normalized;
        }

        public static void SetOptionalUnifiedHmiTagTextProperty(object tag, string propertyName, string value)
        {
            if (tag == null)
            {
                throw new ArgumentNullException(nameof(tag));
            }

            var property = tag.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                throw new InvalidOperationException($"Property '{propertyName}' is not writable on type '{tag.GetType().Name}'.");
            }

            try
            {
                property.SetValue(tag, value ?? string.Empty, null);
            }
            catch (TargetInvocationException ex) when (string.IsNullOrEmpty(value) && (ex.InnerException?.Message?.IndexOf("disabled fields", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message.IndexOf("disabled fields", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return;
            }
            catch (Exception ex) when (string.IsNullOrEmpty(value) && ex.Message.IndexOf("disabled fields", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }
        }

        public static IEnumerable<string> GetPublicPropertySummaries(object instance, params string[] excludedPropertyNames)
        {
            if (instance == null)
            {
                return Enumerable.Empty<string>();
            }

            var excludedNames = new HashSet<string>(excludedPropertyNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            return instance
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead)
                .Where(property => !excludedNames.Contains(property.Name))
                .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .Select(property =>
                {
                    string valueText;
                    try
                    {
                        valueText = FormatHmiPropertyValue(property.GetValue(instance, null));
                    }
                    catch (Exception ex)
                    {
                        valueText = $"<read-error:{ex.Message}>";
                    }

                    return string.Format(
                        "{0} [Access={1}, Value={2}, Type={3}]",
                        property.Name,
                        property.CanWrite ? "ReadWrite" : "Read",
                        valueText,
                        GetFriendlyTypeName(property.PropertyType));
                })
                .ToList();
        }

        public static PropertyInfo ResolvePublicProperty(object instance, string propertyName)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.");
            }

            var normalizedName = propertyName.Trim();
            var properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var exactMatch = properties.FirstOrDefault(property => string.Equals(property.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            throw new InvalidOperationException(
                properties.Any()
                    ? $"Property '{normalizedName}' was not found on type '{instance.GetType().Name}'. Available properties: {string.Join(", ", properties.Select(property => property.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}"
                    : $"Type '{instance.GetType().Name}' does not expose public instance properties.");
        }

        public static object ConvertTextToType(string rawValue, Type targetType, string contextName)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (TryConvertTextValue(rawValue, targetType, out object convertedValue))
            {
                return convertedValue;
            }

            throw new InvalidOperationException($"Unable to convert '{rawValue}' to type '{GetFriendlyTypeName(targetType)}' for {contextName}.");
        }

        public static CrossReferenceFilter ParseCrossReferenceFilter(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
            {
                return CrossReferenceFilter.AllObjects;
            }

            var normalized = filterText.Trim();
            if (Enum.TryParse(normalized, true, out CrossReferenceFilter parsedFilter))
            {
                return parsedFilter;
            }

            throw new ArgumentException($"Unsupported cross reference filter '{filterText}'. Supported values: {string.Join(", ", Enum.GetNames(typeof(CrossReferenceFilter)))}");
        }

        public static List<string> GetCrossReferenceSummaries(IEngineeringServiceProvider serviceProvider, CrossReferenceFilter filter)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var crossReferenceService = serviceProvider.GetService<CrossReferenceService>();
            if (crossReferenceService == null)
            {
                throw new InvalidOperationException($"Cross reference service is not available on type '{serviceProvider.GetType().Name}'.");
            }

            var crossReferenceResult = crossReferenceService.GetCrossReferences(filter);
            var summaries = new List<string>();
            foreach (var source in crossReferenceResult.Sources.OfType<SourceObject>())
            {
                AddCrossReferenceSummariesRecursive(source, summaries);
            }

            return summaries
                .OrderBy(summary => summary, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string SetNamedPropertyOrAttributeValue(object instance, string propertyName, string rawValue, string scopeDescription)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("Property name cannot be empty.");
            }

            var normalizedPropertyName = propertyName.Trim();
            var property = instance
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, normalizedPropertyName, StringComparison.OrdinalIgnoreCase));

            if (property != null)
            {
                var previousValue = property.CanRead ? property.GetValue(instance, null) : null;

                if (property.PropertyType == typeof(MultilingualText))
                {
                    TrySetMultilingualTextProperty(instance, property.Name, rawValue ?? string.Empty);
                    var updatedValue = property.GetValue(instance, null);
                    return $"Updated property '{property.Name}' on {scopeDescription} [OldValue={FormatHmiPropertyValue(previousValue)}, NewValue={FormatHmiPropertyValue(updatedValue)}, ConvertedType=MultilingualText].";
                }

                if (!property.CanWrite)
                {
                    throw new InvalidOperationException($"Property '{property.Name}' is not writable on {scopeDescription}.");
                }

                var convertedValue = ConvertTextToType(rawValue, property.PropertyType, $"property '{property.Name}' on {scopeDescription}");
                property.SetValue(instance, convertedValue, null);
                var updatedPropertyValue = property.CanRead ? property.GetValue(instance, null) : null;
                return $"Updated property '{property.Name}' on {scopeDescription} [OldValue={FormatHmiPropertyValue(previousValue)}, NewValue={FormatHmiPropertyValue(updatedPropertyValue)}, ConvertedType={GetFriendlyTypeName(convertedValue?.GetType() ?? property.PropertyType)}].";
            }

            if (instance is IEngineeringObject engineeringObject)
            {
                var attributeInfo = ResolveAttributeInfo(engineeringObject, normalizedPropertyName);
                if (attributeInfo.AccessMode != EngineeringAttributeAccessMode.Write && attributeInfo.AccessMode != EngineeringAttributeAccessMode.ReadWrite)
                {
                    throw new InvalidOperationException($"Engineering attribute '{attributeInfo.Name}' is not writable on {scopeDescription}. Access mode is {attributeInfo.AccessMode}.");
                }

                var previousValue = TryGetAttributeValue(engineeringObject, attributeInfo.Name, out string previousReadError);
                var convertedValue = ConvertTextToAttributeValue(engineeringObject, attributeInfo, rawValue);
                engineeringObject.SetAttribute(attributeInfo.Name, convertedValue);
                var updatedValue = TryGetAttributeValue(engineeringObject, attributeInfo.Name, out string updatedReadError);
                var previousText = string.IsNullOrWhiteSpace(previousReadError) ? FormatEngineeringValue(previousValue) : $"<read-error:{previousReadError}>";
                var updatedText = string.IsNullOrWhiteSpace(updatedReadError) ? FormatEngineeringValue(updatedValue) : $"<read-error:{updatedReadError}>";
                return $"Updated engineering attribute '{attributeInfo.Name}' on {scopeDescription} [OldValue={previousText}, NewValue={updatedText}, ConvertedType={GetFriendlyTypeName(convertedValue?.GetType() ?? typeof(object))}].";
            }

            throw new InvalidOperationException($"Property or engineering attribute '{normalizedPropertyName}' was not found on {scopeDescription}.");
        }

        public static List<PlcTagTableResolution> GetAllPlcTagTables(PlcSoftware plcSoftware)
        {
            var result = new List<PlcTagTableResolution>();
            AddPlcTagTablesRecursive(plcSoftware.TagTableGroup, null, result);
            return result;
        }

        public static PlcTagTableResolution ResolvePlcTagTable(PlcSoftware plcSoftware, string tableReference)
        {
            var normalizedReference = NormalizePlcTagTableReference(tableReference);
            var allTables = GetAllPlcTagTables(plcSoftware);

            var exactMatch = allTables.FirstOrDefault(table => string.Equals(table.TableReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var accentInsensitiveReference = NormalizeForLooseTextComparison(normalizedReference);

            var nameMatches = allTables
                .Where(table => string.Equals(table.Table.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                nameMatches = allTables
                    .Where(table => string.Equals(NormalizeForLooseTextComparison(table.Table.Name), accentInsensitiveReference, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!nameMatches.Any())
            {
                var referenceMatches = allTables
                    .Where(table => string.Equals(NormalizeForLooseTextComparison(table.TableReference), accentInsensitiveReference, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (referenceMatches.Any())
                {
                    nameMatches = referenceMatches;
                }
            }

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allTables.Any()
                        ? $"PLC tag table '{normalizedReference}' was not found. Available PLC tag tables: {string.Join(", ", allTables.Select(table => table.TableReference))}"
                        : "The PLC has no tag tables.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple PLC tag tables named '{normalizedReference}' were found. Use the full table reference: {string.Join(", ", nameMatches.Select(table => table.TableReference))}");
            }

            return nameMatches[0];
        }

        public static PlcTagResolution ResolvePlcTag(PlcSoftware plcSoftware, string tableReference, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("PLC tag name cannot be empty.");
            }

            var table = ResolvePlcTagTable(plcSoftware, tableReference);
            var tag = table.Table.Tags
                .FirstOrDefault(candidate => string.Equals(candidate.Name, tagName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (tag == null)
            {
                var availableTags = table.Table.Tags.Select(candidate => candidate.Name).ToList();
                throw new InvalidOperationException(
                    availableTags.Any()
                        ? $"PLC tag '{tagName}' was not found in table '{table.TableReference}'. Available tags: {string.Join(", ", availableTags)}"
                        : $"PLC tag table '{table.TableReference}' does not contain any tags.");
            }

            return new PlcTagResolution
            {
                Table = table,
                Tag = tag
            };
        }

        public static IEnumerable<PlcTag> GetAllPlcTags(PlcSoftware plcSoftware)
        {
            return GetAllPlcTagsWithTables(plcSoftware)
                .Select(resolution => resolution.Tag);
        }

        public static List<PlcTagResolution> GetAllPlcTagsWithTables(PlcSoftware plcSoftware)
        {
            return GetAllPlcTagTables(plcSoftware)
                .SelectMany(table => table.Table.Tags.Select(tag => new PlcTagResolution
                {
                    Table = table,
                    Tag = tag
                }))
                .ToList();
        }

        public static List<string> GetAllPlcBlockGroups(PlcSoftware plcSoftware)
        {
            return GetAllPlcBlockGroupTargets(plcSoftware)
                .Select(group => group.GroupReference)
                .Where(group => !string.IsNullOrWhiteSpace(group))
                .Where(group => !string.Equals(group, "<root>", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<PlcBlockGroupResolution> GetAllPlcBlockGroupTargets(PlcSoftware plcSoftware)
        {
            var result = new List<PlcBlockGroupResolution>
            {
                new PlcBlockGroupResolution
                {
                    Blocks = plcSoftware.BlockGroup.Blocks,
                    GroupReference = "<root>"
                }
            };

            AddPlcBlockGroupTargetsRecursive(plcSoftware.BlockGroup, null, result);

            foreach (PlcSystemBlockGroup systemGroup in plcSoftware.BlockGroup.SystemBlockGroups)
            {
                AddPlcSystemBlockGroupTargetsRecursive(systemGroup, systemGroup.Name, result);
            }

            return result;
        }

        public static PlcBlockGroupResolution ResolvePlcBlockGroup(PlcSoftware plcSoftware, string groupReference)
        {
            if (string.IsNullOrWhiteSpace(groupReference) || string.Equals(groupReference.Trim(), "<root>", StringComparison.OrdinalIgnoreCase))
            {
                return new PlcBlockGroupResolution
                {
                    Blocks = plcSoftware.BlockGroup.Blocks,
                    GroupReference = "<root>"
                };
            }

            var normalizedReference = groupReference.Trim();
            var allGroups = GetAllPlcBlockGroupTargets(plcSoftware);

            var exactMatch = allGroups.FirstOrDefault(group => string.Equals(group.GroupReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var nameMatches = allGroups
                .Where(group => string.Equals(GetLastPathSegment(group.GroupReference), normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException($"PLC block group '{normalizedReference}' was not found. Available PLC block groups: {string.Join(", ", allGroups.Select(group => group.GroupReference))}");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple PLC block groups named '{normalizedReference}' were found. Use the full group reference: {string.Join(", ", nameMatches.Select(group => group.GroupReference))}");
            }

            return nameMatches[0];
        }

        public static List<PlcBlockResolution> GetAllPlcBlocks(PlcSoftware plcSoftware)
        {
            var result = new List<PlcBlockResolution>();
            var rootGroup = plcSoftware.BlockGroup;

            AddPlcBlocksRecursive(rootGroup, null, result);

            foreach (PlcSystemBlockGroup systemGroup in rootGroup.SystemBlockGroups)
            {
                AddPlcSystemBlocksRecursive(systemGroup, systemGroup.Name, result);
            }

            return result;
        }

        public static PlcBlockResolution ResolvePlcBlock(PlcSoftware plcSoftware, string blockReference)
        {
            var normalizedReference = NormalizePlcBlockReference(blockReference);
            var allBlocks = GetAllPlcBlocks(plcSoftware);

            var exactMatch = allBlocks.FirstOrDefault(block => string.Equals(block.BlockReference, normalizedReference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch;
            }

            var nameMatches = allBlocks
                .Where(block => string.Equals(block.Block.Name, normalizedReference, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!nameMatches.Any())
            {
                throw new InvalidOperationException(
                    allBlocks.Any()
                        ? $"PLC block '{normalizedReference}' was not found. Available PLC blocks: {string.Join(", ", allBlocks.Select(block => block.BlockReference))}"
                        : "The PLC has no blocks.");
            }

            if (nameMatches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple PLC blocks named '{normalizedReference}' were found. Use the full block reference: {string.Join(", ", nameMatches.Select(block => block.BlockReference))}");
            }

            return nameMatches[0];
        }

        public static string GetPlcBlockTypeName(PlcBlock block)
        {
            if (block is InstanceDB)
            {
                return "InstanceDB";
            }

            if (block is DataBlock)
            {
                return "DB";
            }

            if (block is FB)
            {
                return "FB";
            }

            if (block is FC)
            {
                return "FC";
            }

            if (block is OB)
            {
                return "OB";
            }

            if (block is CodeBlock)
            {
                return "CodeBlock";
            }

            return block.GetType().Name;
        }

        public static int GetNextAvailableFbNumber(PlcSoftware plcSoftware)
        {
            var usedNumbers = new HashSet<int>(
                GetAllPlcBlocks(plcSoftware)
                    .Where(resolution => resolution.Block is FB)
                    .Select(resolution => resolution.Block.Number)
                    .Where(number => number > 0));

            var candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        public static int GetNextAvailableFcNumber(PlcSoftware plcSoftware)
        {
            var usedNumbers = new HashSet<int>(
                GetAllPlcBlocks(plcSoftware)
                    .Where(resolution => resolution.Block is FC)
                    .Select(resolution => resolution.Block.Number)
                    .Where(number => number > 0));

            var candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        public static int GetNextAvailableDbNumber(PlcSoftware plcSoftware)
        {
            var usedNumbers = new HashSet<int>(
                GetAllPlcBlocks(plcSoftware)
                    .Where(resolution => resolution.Block is DataBlock || resolution.Block is InstanceDB)
                    .Select(resolution => resolution.Block.Number)
                    .Where(number => number > 0));

            var candidate = 1;
            while (usedNumbers.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        public static string RequireNewPlcBlockName(PlcSoftware plcSoftware, string blockName)
        {
            var normalizedBlockName = (blockName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedBlockName))
            {
                throw new ArgumentException("PLC block name cannot be empty.");
            }

            if (normalizedBlockName.Contains("/"))
            {
                throw new ArgumentException("PLC block name cannot contain '/'. Use only the block name here, not a block reference path.");
            }

            var existingBlock = GetAllPlcBlocks(plcSoftware)
                .FirstOrDefault(candidate => string.Equals(candidate.Block.Name, normalizedBlockName, StringComparison.OrdinalIgnoreCase));
            if (existingBlock != null)
            {
                throw new InvalidOperationException($"A PLC block named '{normalizedBlockName}' already exists as '{existingBlock.BlockReference}'. Choose a different name.");
            }

            return normalizedBlockName;
        }

        public static TBlock ImportPlcBlockTemplate<TBlock>(
            PlcSoftware plcSoftware,
            PlcBlockGroupResolution targetGroup,
            string blockName,
            string templatePath,
            string commandName,
            string templateNameToken,
            string templateNumberToken,
            int blockNumber,
            string expectedBlockTypeName)
            where TBlock : PlcBlock
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"{commandName} template file was not found: '{templatePath}'. Rebuild the project so Templates/{Path.GetFileName(templatePath)} is copied to the output directory.");
            }

            var targetGroupReference = string.IsNullOrWhiteSpace(targetGroup.GroupReference) ? "<root>" : targetGroup.GroupReference;
            var requestedBlockReference = string.Equals(targetGroupReference, "<root>", StringComparison.OrdinalIgnoreCase)
                ? blockName
                : targetGroupReference + "/" + blockName;

            var templateXml = File.ReadAllText(templatePath);
            var escapedBlockName = SecurityElement.Escape(blockName) ?? blockName;
            var customizedXml = templateXml
                .Replace($"<Name>{templateNameToken}</Name>", $"<Name>{escapedBlockName}</Name>")
                .Replace($"<Number>{templateNumberToken}</Number>", $"<Number>{blockNumber}</Number>");

            if (customizedXml.Contains(templateNameToken) || customizedXml.Contains(templateNumberToken))
            {
                throw new InvalidOperationException($"{commandName} template token replacement failed for '{templatePath}'.");
            }

            var tempDirectoryPath = Path.Combine(Path.GetTempPath(), "TiaLocalBridge", commandName.ToLowerInvariant());
            Directory.CreateDirectory(tempDirectoryPath);
            var tempFilePath = Path.Combine(tempDirectoryPath, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(tempFilePath, customizedXml);

            try
            {
                var importedObjects = targetGroup.Blocks.Import(
                    new FileInfo(tempFilePath),
                    ImportOptions.Override,
                    SWImportOptions.None);

                var importedBlock = importedObjects
                    .OfType<TBlock>()
                    .FirstOrDefault(block => string.Equals(block.Name, blockName, StringComparison.OrdinalIgnoreCase));

                if (importedBlock == null)
                {
                    var resolvedBlock = ResolvePlcBlock(plcSoftware, requestedBlockReference);
                    importedBlock = resolvedBlock.Block as TBlock;
                }

                if (importedBlock == null)
                {
                    throw new InvalidOperationException($"{commandName} import finished, but the created block '{requestedBlockReference}' could not be resolved as a {expectedBlockTypeName}.");
                }

                return importedBlock;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch
                {
                }
            }
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "export";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedChars = value
                .Trim()
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray();

            var sanitized = new string(sanitizedChars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized;
        }

        public static IEnumerable<string> GetAllHmiTagNames(object hmiSoftware)
        {
            if (hmiSoftware is HmiTarget classicHmi)
            {
                return EnumerateClassicHmiTagsFromFolder(classicHmi.TagFolder)
                    .Select(tag => tag.Name);
            }

            if (hmiSoftware is HmiSoftware unifiedHmi)
            {
                return unifiedHmi.Tags.Select(tag => tag.Name);
            }

            return Enumerable.Empty<string>();
        }

        public static string EscapeJson(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        public static CompilerResult CompilePlcSoftware(PlcSoftware plcSoftware, string scopeDescription)
        {
            var compilable = plcSoftware?.GetService<ICompilable>();
            if (compilable == null)
            {
                throw new InvalidOperationException($"Compilation is not available for {scopeDescription}.");
            }

            return compilable.Compile();
        }

        public static CompilerResult CompilePlcBlock(PlcBlock block, string scopeDescription)
        {
            var compilable = block?.GetService<ICompilable>();
            if (compilable == null)
            {
                throw new InvalidOperationException($"Compilation is not available for {scopeDescription}.");
            }

            return compilable.Compile();
        }

        public static string FormatCompilerResult(CompilerResult result)
        {
            if (result == null)
            {
                return "State=<unknown>, Errors=0, Warnings=0";
            }

            var messageSummaries = new List<string>();
            AddCompilerMessageSummaries(result.Messages, messageSummaries);

            var summary = $"State={result.State}, Errors={result.ErrorCount}, Warnings={result.WarningCount}";
            if (messageSummaries.Any())
            {
                summary += $". Messages: {string.Join(" | ", messageSummaries)}";
            }

            return summary;
        }

        private static void AddCompilerMessageSummaries(IEnumerable<CompilerResultMessage> messages, List<string> result)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                if (message == null)
                {
                    continue;
                }

                var path = string.IsNullOrWhiteSpace(message.Path) ? "<no-path>" : message.Path;
                var description = string.IsNullOrWhiteSpace(message.Description) ? "<no-description>" : message.Description;
                result.Add($"State={message.State}, Path={path}, Errors={message.ErrorCount}, Warnings={message.WarningCount}, Description={description}");
                AddCompilerMessageSummaries(message.Messages, result);
            }
        }

        private static string NormalizeDeviceReference(string deviceReference)
        {
            if (string.IsNullOrWhiteSpace(deviceReference))
            {
                throw new ArgumentException("Device reference cannot be empty. Use either '<device-name>' or '<project-name>/<device-name>'.");
            }

            return deviceReference.Trim();
        }

        private static bool TryResolveProjectQualifiedDevice(IEnumerable<Project> openProjects, string deviceReference, out DeviceResolution resolution)
        {
            resolution = null;

            var separatorIndex = deviceReference.IndexOf('/');
            if (separatorIndex <= 0)
            {
                return false;
            }

            var projectName = deviceReference.Substring(0, separatorIndex).Trim();
            var deviceName = deviceReference.Substring(separatorIndex + 1).Trim();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(deviceName))
            {
                throw new ArgumentException("Invalid device reference. Use either '<device-name>' or '<project-name>/<device-name>'.");
            }

            var project = openProjects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            if (project == null)
            {
                return false;
            }

            var device = project.Devices.FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            if (device == null)
            {
                var availableDevicesInProject = project.Devices.Select(d => $"{project.Name}/{d.Name}").ToList();
                throw new InvalidOperationException(
                    availableDevicesInProject.Any()
                        ? $"Device '{deviceName}' was not found in project '{project.Name}'. Available devices: {string.Join(", ", availableDevicesInProject)}"
                        : $"Project '{project.Name}' is open, but no devices were found.");
            }

            resolution = new DeviceResolution
            {
                Project = project,
                Device = device
            };
            return true;
        }

        private static List<string> GetAvailableDeviceReferences(IEnumerable<Project> projects)
        {
            return projects
                .SelectMany(project => project.Devices.Select(device => GetDeviceReference(project, device)))
                .ToList();
        }

        private static string NormalizePlcTagTableReference(string tableReference)
        {
            if (string.IsNullOrWhiteSpace(tableReference))
            {
                throw new ArgumentException("PLC tag table reference cannot be empty.");
            }

            return tableReference.Trim();
        }

        private static string NormalizeForLooseTextComparison(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().Normalize(NormalizationForm.FormD);
            var chars = normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();

            return new string(chars).Normalize(NormalizationForm.FormC);
        }

        private static string NormalizePlcBlockReference(string blockReference)
        {
            if (string.IsNullOrWhiteSpace(blockReference))
            {
                throw new ArgumentException("PLC block reference cannot be empty.");
            }

            return blockReference.Trim();
        }

        private static string GetLastPathSegment(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            var lastSeparatorIndex = path.LastIndexOf('/');
            return lastSeparatorIndex >= 0 ? path.Substring(lastSeparatorIndex + 1) : path;
        }

        private static void AddPlcTagTablesRecursive(PlcTagTableGroup group, string groupPath, List<PlcTagTableResolution> result)
        {
            foreach (PlcTagTable table in group.TagTables)
            {
                result.Add(new PlcTagTableResolution
                {
                    Table = table,
                    TableReference = string.IsNullOrWhiteSpace(groupPath)
                        ? table.Name
                        : $"{groupPath}/{table.Name}"
                });
            }

            foreach (PlcTagTableUserGroup childGroup in group.Groups)
            {
                var childPath = string.IsNullOrWhiteSpace(groupPath)
                    ? childGroup.Name
                    : $"{groupPath}/{childGroup.Name}";

                AddPlcTagTablesRecursive(childGroup, childPath, result);
            }
        }

        private static void AddPlcBlockGroupTargetsRecursive(PlcBlockGroup group, string groupPath, List<PlcBlockGroupResolution> result)
        {
            foreach (PlcBlockUserGroup childGroup in group.Groups)
            {
                var childPath = string.IsNullOrWhiteSpace(groupPath)
                    ? childGroup.Name
                    : $"{groupPath}/{childGroup.Name}";

                result.Add(new PlcBlockGroupResolution
                {
                    Blocks = childGroup.Blocks,
                    GroupReference = childPath
                });

                AddPlcBlockGroupTargetsRecursive(childGroup, childPath, result);
            }
        }

        private static void AddPlcSystemBlockGroupTargetsRecursive(PlcSystemBlockGroup group, string groupPath, List<PlcBlockGroupResolution> result)
        {
            result.Add(new PlcBlockGroupResolution
            {
                Blocks = group.Blocks,
                GroupReference = groupPath
            });

            foreach (PlcSystemBlockGroup childGroup in group.Groups)
            {
                var childPath = string.IsNullOrWhiteSpace(groupPath)
                    ? childGroup.Name
                    : $"{groupPath}/{childGroup.Name}";

                AddPlcSystemBlockGroupTargetsRecursive(childGroup, childPath, result);
            }
        }

        private static void AddPlcBlocksRecursive(PlcBlockGroup group, string groupPath, List<PlcBlockResolution> result)
        {
            foreach (PlcBlock block in group.Blocks)
            {
                result.Add(new PlcBlockResolution
                {
                    Block = block,
                    GroupReference = groupPath,
                    BlockReference = string.IsNullOrWhiteSpace(groupPath)
                        ? block.Name
                        : $"{groupPath}/{block.Name}"
                });
            }

            foreach (PlcBlockUserGroup childGroup in group.Groups)
            {
                var childPath = string.IsNullOrWhiteSpace(groupPath)
                    ? childGroup.Name
                    : $"{groupPath}/{childGroup.Name}";

                AddPlcBlocksRecursive(childGroup, childPath, result);
            }
        }

        private static void AddPlcSystemBlocksRecursive(PlcSystemBlockGroup group, string groupPath, List<PlcBlockResolution> result)
        {
            foreach (PlcBlock block in group.Blocks)
            {
                result.Add(new PlcBlockResolution
                {
                    Block = block,
                    GroupReference = groupPath,
                    BlockReference = string.IsNullOrWhiteSpace(groupPath)
                        ? block.Name
                        : $"{groupPath}/{block.Name}"
                });
            }

            foreach (PlcSystemBlockGroup childGroup in group.Groups)
            {
                var childPath = string.IsNullOrWhiteSpace(groupPath)
                    ? childGroup.Name
                    : $"{groupPath}/{childGroup.Name}";

                AddPlcSystemBlocksRecursive(childGroup, childPath, result);
            }
        }

        private static void AddUnifiedHmiTagTablesRecursive(HmiTagTableGroupComposition groups, string parentPath, List<UnifiedHmiTagTableResolution> result)
        {
            foreach (HmiTagTableGroup group in groups)
            {
                var groupPath = string.IsNullOrWhiteSpace(parentPath)
                    ? group.Name
                    : $"{parentPath}/{group.Name}";

                foreach (HmiTagTable table in group.TagTables)
                {
                    result.Add(new UnifiedHmiTagTableResolution
                    {
                        Table = table,
                        GroupReference = groupPath,
                        TableReference = $"{groupPath}/{table.Name}"
                    });
                }

                AddUnifiedHmiTagTablesRecursive(group.Groups, groupPath, result);
            }
        }

        private static void AddUnifiedHmiScreenGroupsRecursive(HmiScreenGroupComposition groups, string parentPath, List<UnifiedHmiScreenGroupResolution> result)
        {
            foreach (HmiScreenGroup group in groups)
            {
                var groupPath = string.IsNullOrWhiteSpace(parentPath)
                    ? group.Name
                    : $"{parentPath}/{group.Name}";

                result.Add(new UnifiedHmiScreenGroupResolution
                {
                    Group = group,
                    GroupReference = groupPath
                });

                AddUnifiedHmiScreenGroupsRecursive(group.Groups, groupPath, result);
            }
        }

        private static void AddUnifiedHmiScreensRecursive(HmiScreenGroupComposition groups, string parentPath, List<UnifiedHmiScreenResolution> result)
        {
            foreach (HmiScreenGroup group in groups)
            {
                var groupPath = string.IsNullOrWhiteSpace(parentPath)
                    ? group.Name
                    : $"{parentPath}/{group.Name}";

                foreach (HmiScreen screen in group.Screens)
                {
                    result.Add(new UnifiedHmiScreenResolution
                    {
                        Screen = screen,
                        GroupReference = groupPath,
                        ScreenReference = $"{groupPath}/{screen.Name}"
                    });
                }

                AddUnifiedHmiScreensRecursive(group.Groups, groupPath, result);
            }
        }

        private static void AddDeviceItemRecursive(DeviceItem item, List<DeviceItem> result)
        {
            result.Add(item);

            foreach (DeviceItem childItem in item.Items)
            {
                AddDeviceItemRecursive(childItem, result);
            }
        }

        private static void AddDeviceItemResolutionRecursive(DeviceItem item, DeviceItem parentItem, string parentReference, string parentNamePath, List<DeviceItemResolution> result)
        {
            var itemReference = string.IsNullOrWhiteSpace(parentReference)
                ? item.PositionNumber.ToString(CultureInfo.InvariantCulture)
                : $"{parentReference}/{item.PositionNumber.ToString(CultureInfo.InvariantCulture)}";

            var namePath = string.IsNullOrWhiteSpace(parentNamePath)
                ? item.Name
                : $"{parentNamePath}/{item.Name}";

            result.Add(new DeviceItemResolution
            {
                Item = item,
                ParentItem = parentItem,
                ItemReference = itemReference,
                ParentReference = string.IsNullOrWhiteSpace(parentReference) ? "DEVICE" : parentReference,
                NamePath = namePath
            });

            foreach (DeviceItem childItem in item.Items)
            {
                AddDeviceItemResolutionRecursive(childItem, item, itemReference, namePath, result);
            }
        }

        private static string TryGetPublicPropertyText(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return "<n/a>";
            }

            try
            {
                var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanRead)
                {
                    return "<n/a>";
                }

                var value = property.GetValue(instance, null);
                return FormatHmiPropertyValue(value);
            }
            catch (Exception ex)
            {
                return $"<error:{ex.Message}>";
            }
        }

        private static string FormatHmiPropertyValue(object value)
        {
            if (value == null)
            {
                return "<null>";
            }

            if (value is MultilingualText multilingualText)
            {
                var items = multilingualText.Items
                    .Cast<MultilingualTextItem>()
                    .Select(item => string.Format(
                        "{0}={1}",
                        item.Language != null ? item.Language.Culture.Name : "<unknown>",
                        item.Text ?? string.Empty))
                    .ToList();

                return items.Any() ? string.Join("; ", items) : "<empty>";
            }

            return FormatEngineeringValue(value);
        }

        private static bool TryConvertTextValue(string rawValue, Type candidateType, out object convertedValue)
        {
            convertedValue = null;
            if (candidateType == null)
            {
                return false;
            }

            var underlyingType = Nullable.GetUnderlyingType(candidateType) ?? candidateType;
            var normalizedValue = rawValue ?? string.Empty;

            if (underlyingType == typeof(string))
            {
                convertedValue = rawValue ?? string.Empty;
                return true;
            }

            if (underlyingType == typeof(Color) && TryConvertColorValue(normalizedValue, out Color colorValue))
            {
                convertedValue = colorValue;
                return true;
            }

            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                if (candidateType != underlyingType || !underlyingType.IsValueType)
                {
                    convertedValue = null;
                    return true;
                }

                return false;
            }

            if (underlyingType == typeof(bool) && bool.TryParse(normalizedValue, out bool boolValue))
            {
                convertedValue = boolValue;
                return true;
            }

            if (underlyingType == typeof(byte) && byte.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte byteValue))
            {
                convertedValue = byteValue;
                return true;
            }

            if (underlyingType == typeof(short) && short.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out short shortValue))
            {
                convertedValue = shortValue;
                return true;
            }

            if (underlyingType == typeof(ushort) && ushort.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort ushortValue))
            {
                convertedValue = ushortValue;
                return true;
            }

            if (underlyingType == typeof(int) && int.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                convertedValue = intValue;
                return true;
            }

            if (underlyingType == typeof(uint) && uint.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint uintValue))
            {
                convertedValue = uintValue;
                return true;
            }

            if (underlyingType == typeof(long) && long.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                convertedValue = longValue;
                return true;
            }

            if (underlyingType == typeof(ulong) && ulong.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ulongValue))
            {
                convertedValue = ulongValue;
                return true;
            }

            if (underlyingType == typeof(float) && float.TryParse(normalizedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float floatValue))
            {
                convertedValue = floatValue;
                return true;
            }

            if (underlyingType == typeof(double) && double.TryParse(normalizedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
            {
                convertedValue = doubleValue;
                return true;
            }

            if (underlyingType == typeof(decimal) && decimal.TryParse(normalizedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal decimalValue))
            {
                convertedValue = decimalValue;
                return true;
            }

            if (underlyingType == typeof(DateTime) && DateTime.TryParse(normalizedValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateTimeValue))
            {
                convertedValue = dateTimeValue;
                return true;
            }

            if (underlyingType == typeof(TimeSpan) && TimeSpan.TryParse(normalizedValue, CultureInfo.InvariantCulture, out TimeSpan timeSpanValue))
            {
                convertedValue = timeSpanValue;
                return true;
            }

            if (underlyingType == typeof(Guid) && Guid.TryParse(normalizedValue, out Guid guidValue))
            {
                convertedValue = guidValue;
                return true;
            }

            if (underlyingType.IsEnum)
            {
                try
                {
                    convertedValue = Enum.Parse(underlyingType, normalizedValue, true);
                    return true;
                }
                catch
                {
                    var enumUnderlyingType = Enum.GetUnderlyingType(underlyingType);
                    if (TryConvertTextValue(normalizedValue, enumUnderlyingType, out object enumNumericValue))
                    {
                        convertedValue = Enum.ToObject(underlyingType, enumNumericValue);
                        return true;
                    }
                }
            }

            try
            {
                convertedValue = Convert.ChangeType(normalizedValue, underlyingType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertColorValue(string rawValue, out Color colorValue)
        {
            colorValue = default(Color);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var normalizedValue = rawValue.Trim();

            try
            {
                colorValue = ColorTranslator.FromHtml(normalizedValue);
                return true;
            }
            catch
            {
            }

            var namedColor = Color.FromName(normalizedValue);
            if (namedColor.IsKnownColor || namedColor.IsNamedColor || namedColor.A != 0 || namedColor.R != 0 || namedColor.G != 0 || namedColor.B != 0)
            {
                colorValue = namedColor;
                return true;
            }

            var parts = normalizedValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .ToArray();

            if (parts.Length == 3
                && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
            {
                colorValue = Color.FromArgb(r, g, b);
                return true;
            }

            if (parts.Length == 4
                && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte a)
                && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte ar)
                && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte ag)
                && byte.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte ab))
            {
                colorValue = Color.FromArgb(a, ar, ag, ab);
                return true;
            }

            return false;
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
            {
                return "<unknown>";
            }

            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                return $"{GetFriendlyTypeName(underlyingType)}?";
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var genericBaseName = type.Name;
            var tickIndex = genericBaseName.IndexOf('`');
            if (tickIndex >= 0)
            {
                genericBaseName = genericBaseName.Substring(0, tickIndex);
            }

            return $"{genericBaseName}<{string.Join(",", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
        }

        private static object TryGetSoftware(Device device)
        {
            var deviceSoftwareContainer = device.GetService<SoftwareContainer>();
            if (deviceSoftwareContainer != null && deviceSoftwareContainer.Software != null)
            {
                return deviceSoftwareContainer.Software;
            }

            foreach (var item in GetAllDeviceItems(device))
            {
                var softwareContainer = item.GetService<SoftwareContainer>();
                if (softwareContainer != null && softwareContainer.Software != null)
                {
                    return softwareContainer.Software;
                }
            }

            return null;
        }

        private static IEnumerable<PlcTag> EnumeratePlcTagsFromGroup(object group)
        {
            foreach (var table in EnumerateComposition(GetPropertyValue(group, "TagTables")))
            {
                foreach (var tag in EnumerateComposition(GetPropertyValue(table, "Tags")).OfType<PlcTag>())
                {
                    yield return tag;
                }
            }

            foreach (var subgroup in EnumerateComposition(GetPropertyValue(group, "Groups")))
            {
                foreach (var tag in EnumeratePlcTagsFromGroup(subgroup))
                {
                    yield return tag;
                }
            }
        }

        private static IEnumerable<Tag> EnumerateClassicHmiTagsFromFolder(object folder)
        {
            foreach (var table in EnumerateComposition(GetPropertyValue(folder, "TagTables")))
            {
                foreach (var tag in EnumerateComposition(GetPropertyValue(table, "Tags")).OfType<Tag>())
                {
                    yield return tag;
                }
            }

            foreach (var subfolder in EnumerateComposition(GetPropertyValue(folder, "Folders")))
            {
                foreach (var tag in EnumerateClassicHmiTagsFromFolder(subfolder))
                {
                    yield return tag;
                }
            }
        }

        private static void AddCrossReferenceSummariesRecursive(SourceObject source, List<string> summaries)
        {
            if (source == null || summaries == null)
            {
                return;
            }

            var sourceName = string.IsNullOrWhiteSpace(source.Name) ? "<empty>" : source.Name;
            var sourcePath = string.IsNullOrWhiteSpace(source.Path) ? sourceName : source.Path;
            var sourceType = string.IsNullOrWhiteSpace(source.TypeName) ? "<empty>" : source.TypeName;
            var references = source.References.OfType<ReferenceObject>().ToList();
            var children = source.Children.OfType<SourceObject>().ToList();

            if (!references.Any() && !children.Any())
            {
                summaries.Add($"{sourceName} [SourcePath={sourcePath}, SourceType={sourceType}, ReferenceCount=0]");
            }

            foreach (var reference in references)
            {
                var referenceName = string.IsNullOrWhiteSpace(reference.Name) ? "<empty>" : reference.Name;
                var referencePath = string.IsNullOrWhiteSpace(reference.Path) ? referenceName : reference.Path;
                var referenceTypeName = string.IsNullOrWhiteSpace(reference.TypeName) ? "<empty>" : reference.TypeName;
                var referenceDevice = string.IsNullOrWhiteSpace(reference.Device) ? "<empty>" : reference.Device;
                var referenceAddress = string.IsNullOrWhiteSpace(reference.Address) ? "<empty>" : reference.Address;
                var locations = reference.Locations.OfType<Location>().ToList();

                if (!locations.Any())
                {
                    summaries.Add($"{referenceName} [SourcePath={sourcePath}, SourceType={sourceType}, ReferencePath={referencePath}, ReferenceObjectType={referenceTypeName}, Device={referenceDevice}, Address={referenceAddress}, Location=<none>, ReferenceType=<none>, Access=<none>, ReferencedAs=<empty>]");
                    continue;
                }

                foreach (var location in locations)
                {
                    var locationName = string.IsNullOrWhiteSpace(location.Name) ? "<empty>" : location.Name;
                    var referenceLocation = string.IsNullOrWhiteSpace(location.ReferenceLocation) ? locationName : location.ReferenceLocation;
                    var referencedAs = string.IsNullOrWhiteSpace(location.ReferencedAsName) ? "<empty>" : location.ReferencedAsName;

                    summaries.Add(
                        $"{referenceName} [SourcePath={sourcePath}, SourceType={sourceType}, ReferencePath={referencePath}, ReferenceObjectType={referenceTypeName}, Device={referenceDevice}, Address={referenceAddress}, Location={referenceLocation}, ReferenceType={location.ReferenceType}, Access={location.Access}, ReferencedAs={referencedAs}]");
                }
            }

            foreach (var child in children)
            {
                AddCrossReferenceSummariesRecursive(child, summaries);
            }
        }

        private static IEnumerable<object> EnumerateComposition(object composition)
        {
            if (composition is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    yield return item;
                }
            }
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null)
            {
                return null;
            }

            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(instance, null);
        }
    }
}
