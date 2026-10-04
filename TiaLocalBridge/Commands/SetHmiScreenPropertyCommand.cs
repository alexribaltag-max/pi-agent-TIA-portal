using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SetHmiScreenPropertyCommand : ITiaCommand
    {
        public string Name => "SETHMISCREENPROPERTY";
        public string Description => "Sets a writable Unified screen property or classic WinCC screen engineering attribute. Use GETHMISCREENPROPERTIES first to inspect available settings.";
        public string Usage => "SETHMISCREENPROPERTY|<device-reference>|<screen-reference>|<property-name>|<value>";
        public string Example => "SETHMISCREENPROPERTY|DemoProject/HMI_1|Config/Overview|BackColor|#F5F5F5";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>", "<property-name>", "<value>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                var classicHmi = CommandSupport.TryGetHmiSoftware(resolution.Device);
                if (classicHmi == null)
                {
                    throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
                }

                var classicScreen = CommandSupport.ResolveClassicHmiScreen(classicHmi, providedArgs[1]);
                return CommandSupport.SetNamedPropertyOrAttributeValue(
                    classicScreen.Item,
                    providedArgs[2],
                    providedArgs[3],
                    $"classic HMI screen '{classicScreen.ObjectReference}' on device '{resolvedReference}'");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            return CommandSupport.SetNamedPropertyOrAttributeValue(
                screen.Screen,
                providedArgs[2],
                providedArgs[3],
                $"Unified HMI screen '{screen.ScreenReference}' on device '{resolvedReference}'");
        }
    }
}
