using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenItemsCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENITEMS";
        public string Description => "Lists the items on one Unified HMI screen, including item type and basic geometry.";
        public string Usage => "GETHMISCREENITEMS|<device-reference>|<screen-reference>";
        public string Example => "GETHMISCREENITEMS|DemoProject/HMI_1|Config/Overview";
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
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var items = screen.Screen.ScreenItems
                .Select(item => CommandSupport.FormatUnifiedHmiScreenItemSummary(item, screen.ScreenReference))
                .ToList();

            return items.Any()
                ? $"Device '{resolvedReference}' Unified HMI screen '{screen.ScreenReference}' items: {string.Join(", ", items)}"
                : $"Device '{resolvedReference}' Unified HMI screen '{screen.ScreenReference}' has no items.";
        }
    }
}
