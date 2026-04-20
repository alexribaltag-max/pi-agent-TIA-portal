using System;
using System.Globalization;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class AddHmiScreenItemCommand : ITiaCommand
    {
        public string Name => "ADDHMISCREENITEM";
        public string Description => "Adds a Unified HMI screen item to a screen. Supported item types: LABEL, BUTTON, IOFIELD, RECTANGLE, TEXT, LINE, ELLIPSE, TEXTBOX, SYMBOLICIOFIELD, GRAPHICVIEW. When [text] is provided for a multilingual Text property, plain text is normalized automatically.";
        public string Usage => "ADDHMISCREENITEM|<device-reference>|<screen-reference>|<item-type>|<item-name>|<left>|<top>|<width>|<height>|[text]";
        public string Example => "ADDHMISCREENITEM|DemoProject/HMI_1|Config/Overview|TEXT|Txt_Title|20|20|220|40|Device configuration";
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

            var createdItem = CommandSupport.CreateUnifiedHmiScreenItem(screen.Screen, itemType, itemName);
            CommandSupport.ApplyUnifiedHmiScreenItemGeometry(createdItem, left, top, width, height);

            if (!string.IsNullOrWhiteSpace(text))
            {
                CommandSupport.TrySetMultilingualTextProperty(createdItem, "Text", text);
            }

            return $"Added Unified HMI screen item '{createdItem.Name}' to screen '{screen.ScreenReference}' in device '{resolvedReference}' [Type={createdItem.GetType().Name}, Left={left}, Top={top}, Width={width}, Height={height}].";
        }
    }
}
