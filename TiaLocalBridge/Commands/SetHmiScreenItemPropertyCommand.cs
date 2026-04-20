using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetHmiScreenItemPropertyCommand : ITiaCommand
    {
        public string Name => "SETHMISCREENITEMPROPERTY";
        public string Description => "Sets one writable Unified HMI screen item property. Use GETHMISCREENITEMPROPERTIES first to inspect the available properties for the selected item. For multilingual text properties such as Text, plain text is normalized automatically.";
        public string Usage => "SETHMISCREENITEMPROPERTY|<device-reference>|<screen-reference>|<item-name>|<property-name>|<value>";
        public string Example => "SETHMISCREENITEMPROPERTY|DemoProject/HMI_1|Config/Overview|Lbl_Title|Text|Device configuration";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>", "<item-name>", "<property-name>", "<value>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var item = CommandSupport.ResolveUnifiedHmiScreenItem(screen.Screen, providedArgs[2]);
            return CommandSupport.SetNamedPropertyOrAttributeValue(
                item,
                providedArgs[3],
                providedArgs[4],
                $"Unified HMI screen item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}'");
        }
    }
}
