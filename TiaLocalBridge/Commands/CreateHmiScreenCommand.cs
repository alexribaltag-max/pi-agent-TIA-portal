using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class CreateHmiScreenCommand : ITiaCommand
    {
        public string Name => "CREATEHMISCREEN";
        public string Description => "Creates a Unified HMI screen either at the root or inside a Unified HMI screen group. Use '<root>' for the root level.";
        public string Usage => "CREATEHMISCREEN|<device-reference>|<parent-group-reference>|<screen-name>";
        public string Example => "CREATEHMISCREEN|DemoProject/HMI_1|Config|Overview";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<parent-group-reference>", "<screen-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var parentGroupReference = providedArgs[1];
            var parentGroup = CommandSupport.ResolveUnifiedHmiScreenGroup(hmiSoftware, parentGroupReference);
            var existingNames = parentGroup == null
                ? hmiSoftware.Screens.Select(screen => screen.Name)
                : parentGroup.Group.Screens.Select(screen => screen.Name);
            var screenName = CommandSupport.RequireNewUnifiedHmiObjectName(existingNames, "Unified HMI screen", providedArgs[2]);
            var createdScreen = parentGroup == null
                ? hmiSoftware.Screens.Create(screenName)
                : parentGroup.Group.Screens.Create(screenName);
            var createdReference = string.Equals(parentGroupReference, "<root>", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(parentGroupReference)
                ? createdScreen.Name
                : parentGroup.GroupReference + "/" + createdScreen.Name;

            return $"Created Unified HMI screen '{createdReference}' in device '{resolvedReference}' [ScreenNumber={createdScreen.ScreenNumber}].";
        }
    }
}
