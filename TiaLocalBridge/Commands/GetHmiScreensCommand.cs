using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreensCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENS";
        public string Description => "Lists screens for Unified or classic WinCC targets, including their folder/group path and Unified screen number where available.";
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
                var classicHmi = CommandSupport.TryGetHmiSoftware(resolution.Device);
                if (classicHmi == null)
                {
                    throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
                }

                var classicScreens = CommandSupport.GetClassicHmiScreens(classicHmi)
                    .OrderBy(screen => screen.ObjectReference, StringComparer.OrdinalIgnoreCase)
                    .Select(screen => $"{screen.ObjectReference} [Folder={screen.ParentReference}]")
                    .ToList();
                return classicScreens.Any()
                    ? $"Device '{resolvedReference}' classic HMI screens: {string.Join(", ", classicScreens)}"
                    : $"Device '{resolvedReference}' has no classic HMI screens.";
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
