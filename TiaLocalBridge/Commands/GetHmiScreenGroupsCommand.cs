using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenGroupsCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENGROUPS";
        public string Description => "Lists Unified HMI screen groups or classic WinCC screen folders for the specified HMI device reference.";
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
                var classicHmi = CommandSupport.TryGetHmiSoftware(resolution.Device);
                if (classicHmi == null)
                {
                    throw new InvalidOperationException($"Device '{resolvedReference}' does not contain HMI software.");
                }

                var classicGroups = CommandSupport.GetClassicHmiScreenGroups(classicHmi)
                    .Select(group => group.ObjectReference)
                    .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return classicGroups.Any()
                    ? $"Device '{resolvedReference}' classic HMI screen folders: {string.Join(", ", classicGroups)}"
                    : $"Device '{resolvedReference}' has no classic HMI screen folders.";
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
