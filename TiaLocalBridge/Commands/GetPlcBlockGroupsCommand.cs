using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetPlcBlockGroupsCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKGROUPS";
        public string Description => "Lists all PLC block groups for the specified PLC device reference so you can understand the PLC block structure before exporting or editing blocks.";
        public string Usage => "GETPLCBLOCKGROUPS|<device-reference>";
        public string Example => "GETPLCBLOCKGROUPS|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var groups = CommandSupport.GetAllPlcBlockGroups(plcSoftware);

            return groups.Any()
                ? $"Device '{resolvedReference}' PLC block groups: {string.Join(", ", groups)}"
                : $"Device '{resolvedReference}' has no PLC block groups.";
        }
    }
}
