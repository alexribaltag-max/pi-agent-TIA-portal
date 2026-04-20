using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenItemPropertiesCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENITEMPROPERTIES";
        public string Description => "Lists the public properties of one Unified HMI screen item so you can inspect geometry, text, visibility, and other editable settings before changing them.";
        public string Usage => "GETHMISCREENITEMPROPERTIES|<device-reference>|<screen-reference>|<item-name>";
        public string Example => "GETHMISCREENITEMPROPERTIES|DemoProject/HMI_1|Config/Overview|Lbl_Title";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>", "<item-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var item = CommandSupport.ResolveUnifiedHmiScreenItem(screen.Screen, providedArgs[2]);
            var properties = CommandSupport.GetPublicPropertySummaries(item, "Parent", "EventHandlers", "PropertyEventHandlers", "Dynamizations")
                .ToList();

            return properties.Any()
                ? $"Unified HMI screen item properties for '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}': {string.Join(" || ", properties)}"
                : $"No public Unified HMI screen item properties were exposed for '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}'.";
        }
    }
}
