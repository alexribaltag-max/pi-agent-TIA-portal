using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class CreateHmiScreenGroupCommand : ITiaCommand
    {
        public string Name => "CREATEHMISCREENGROUP";
        public string Description => "Creates a Unified HMI screen group either at the root or inside another screen group. Use '<root>' for the root level.";
        public string Usage => "CREATEHMISCREENGROUP|<device-reference>|<parent-group-reference>|<group-name>";
        public string Example => "CREATEHMISCREENGROUP|DemoProject/HMI_1|<root>|Config";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<parent-group-reference>", "<group-name>");
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
                ? hmiSoftware.ScreenGroups.Select(group => group.Name)
                : parentGroup.Group.Groups.Select(group => group.Name);
            var groupName = CommandSupport.RequireNewUnifiedHmiObjectName(existingNames, "Unified HMI screen group", providedArgs[2]);
            var createdGroup = parentGroup == null
                ? hmiSoftware.ScreenGroups.Create(groupName)
                : parentGroup.Group.Groups.Create(groupName);
            var createdReference = string.Equals(parentGroupReference, "<root>", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(parentGroupReference)
                ? createdGroup.Name
                : parentGroup.GroupReference + "/" + createdGroup.Name;

            return $"Created Unified HMI screen group '{createdReference}' in device '{resolvedReference}'.";
        }
    }
}
