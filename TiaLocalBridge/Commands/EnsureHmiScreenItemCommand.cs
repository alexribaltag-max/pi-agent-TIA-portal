using System;
using System.Globalization;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class EnsureHmiScreenItemCommand : ITiaCommand
    {
        public string Name => "ENSUREHMISCREENITEM";
        public string Description => "Creates a Unified HMI screen item if it does not exist, or updates the geometry and optional text of an existing item. This makes retries safer for agent workflows.";
        public string Usage => "ENSUREHMISCREENITEM|<device-reference>|<screen-reference>|<item-type>|<item-name>|<left>|<top>|<width>|<height>|[text]";
        public string Example => "ENSUREHMISCREENITEM|DemoProject/HMI_1|Config/Overview|BUTTON|Btn_Ok|520|50|180|70|OK";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 8 && providedArgs.Length != 9)
            {
                throw new ArgumentException($"Expected exactly 8 or 9 argument(s). {Description} Usage: {Usage}. Example: {Example}");
            }

            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var itemType = providedArgs[2];
            var itemName = providedArgs[3];
            var left = int.Parse(providedArgs[4], CultureInfo.InvariantCulture);
            var top = int.Parse(providedArgs[5], CultureInfo.InvariantCulture);
            var width = uint.Parse(providedArgs[6], CultureInfo.InvariantCulture);
            var height = uint.Parse(providedArgs[7], CultureInfo.InvariantCulture);
            var text = providedArgs.Length >= 9 ? providedArgs[8] : null;

            var supportedTypes = CommandSupport.GetSupportedUnifiedHmiScreenItemTypes();
            if (!supportedTypes.TryGetValue((itemType ?? string.Empty).Trim(), out Type requestedType))
            {
                throw new ArgumentException($"Unsupported Unified HMI screen item type '{itemType}'. Supported types: {string.Join(", ", supportedTypes.Keys)}");
            }

            var existingItem = CommandSupport.FindUnifiedHmiScreenItem(screen.Screen, itemName);
            var created = existingItem == null;
            var item = existingItem ?? CommandSupport.CreateUnifiedHmiScreenItem(screen.Screen, itemType, itemName);

            if (!created && item.GetType() != requestedType)
            {
                throw new InvalidOperationException($"Unified HMI screen item '{item.Name}' already exists on screen '{screen.ScreenReference}' with type '{item.GetType().Name}', not requested type '{requestedType.Name}'.");
            }

            var beforeSummary = created ? null : CommandSupport.FormatUnifiedHmiScreenItemSummary(item, screen.ScreenReference);

            CommandSupport.ApplyUnifiedHmiScreenItemGeometry(item, left, top, width, height);
            if (providedArgs.Length >= 9)
            {
                CommandSupport.TrySetMultilingualTextProperty(item, "Text", text);
            }

            var afterSummary = CommandSupport.FormatUnifiedHmiScreenItemSummary(item, screen.ScreenReference);
            var operation = created
                ? "created"
                : string.Equals(beforeSummary, afterSummary, StringComparison.Ordinal)
                    ? "unchanged"
                    : "updated";

            return $"Ensured Unified HMI screen item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' [Operation={operation}, Type={item.GetType().Name}, Left={left}, Top={top}, Width={width}, Height={height}].";
        }
    }
}
