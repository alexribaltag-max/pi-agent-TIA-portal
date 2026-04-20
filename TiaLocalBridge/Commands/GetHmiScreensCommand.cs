using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreensCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENS";
        public string Description => "Lists all Unified HMI screens for the specified HMI device reference, including the screen group path and screen number.";
        public string Usage => "GETHMISCREENS|<device-reference>";
        public string Example => "GETHMISCREENS|DemoProject/HMI_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screens = CommandSupport.GetAllUnifiedHmiScreens(hmiSoftware)
                .OrderBy(screen => screen.ScreenReference, StringComparer.OrdinalIgnoreCase)
                .Select(screen => string.Format(
                    "{0} [Group={1}, ScreenNumber={2}]",
                    screen.ScreenReference,
                    string.IsNullOrWhiteSpace(screen.GroupReference) ? "<root>" : screen.GroupReference,
                    screen.Screen.ScreenNumber))
                .ToList();

            return screens.Any()
                ? $"Device '{resolvedReference}' Unified HMI screens: {string.Join(", ", screens)}"
                : $"Device '{resolvedReference}' has no Unified HMI screens.";
        }
    }
}
