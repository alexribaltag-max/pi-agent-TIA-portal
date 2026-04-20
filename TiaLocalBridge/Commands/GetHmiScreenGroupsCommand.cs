using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenGroupsCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENGROUPS";
        public string Description => "Lists all Unified HMI screen groups for the specified HMI device reference.";
        public string Usage => "GETHMISCREENGROUPS|<device-reference>";
        public string Example => "GETHMISCREENGROUPS|DemoProject/HMI_1";
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

            var groups = CommandSupport.GetAllUnifiedHmiScreenGroups(hmiSoftware)
                .Select(group => group.GroupReference)
                .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return groups.Any()
                ? $"Device '{resolvedReference}' Unified HMI screen groups: {string.Join(", ", groups)}"
                : $"Device '{resolvedReference}' has no Unified HMI screen groups.";
        }
    }
}
