using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenPropertiesCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENPROPERTIES";
        public string Description => "Lists public properties for a Unified screen or engineering attributes exposed by a classic WinCC screen.";
        public string Usage => "GETHMISCREENPROPERTIES|<device-reference>|<screen-reference>";
        public string Example => "GETHMISCREENPROPERTIES|DemoProject/HMI_1|Config/Overview";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>");
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
                var classicProperties = classicScreen.Item is Siemens.Engineering.IEngineeringObject classicEngineeringObject
                    ? CommandSupport.GetEngineeringAttributeSummaries(classicEngineeringObject)
                    : new System.Collections.Generic.List<string>();
                return classicProperties.Any()
                    ? $"Classic HMI screen properties for '{classicScreen.ObjectReference}' on device '{resolvedReference}': {string.Join(" || ", classicProperties)}"
                    : $"No engineering attributes were exposed for classic HMI screen '{classicScreen.ObjectReference}' on device '{resolvedReference}'.";
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var properties = CommandSupport.GetPublicPropertySummaries(screen.Screen, "Parent", "EventHandlers", "PropertyEventHandlers", "Dynamizations", "ScreenItems")
                .ToList();

            return properties.Any()
                ? $"Unified HMI screen properties for '{screen.ScreenReference}' on device '{resolvedReference}': {string.Join(" || ", properties)}"
                : $"No public Unified HMI screen properties were exposed for '{screen.ScreenReference}' on device '{resolvedReference}'.";
        }
    }
}
