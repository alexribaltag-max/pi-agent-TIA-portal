using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class GetDeviceItemsCommand : ITiaCommand
    {
        public string Name => "GETDEVICEITEMS";
        public string Description => "Lists all device items (PLC modules) for the specified device reference. Use the [Reference=...] value returned by GETDEVICES, or use either '<device-name>' or '<project-name>/<device-name>'.";
        public string Usage => "GETDEVICEITEMS|<device-reference>";
        public string Example => "GETDEVICEITEMS|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var deviceReference = CommandSupport.RequireSingleArgument(args, this, "<device-reference>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceReference);
            var items = CommandSupport.GetAllDeviceItemResolutions(resolution.Device)
                .Select(item => string.Format("{0} [Reference={1}, Parent={2}, NamePath={3}, Type={4}, Position={5}, Plugged={6}]", item.Item.Name, item.ItemReference, item.ParentReference, item.NamePath, item.Item.TypeIdentifier, item.Item.PositionNumber, item.Item.IsPlugged))
                .ToList();

            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            return items.Any()
                ? $"Device '{resolvedReference}' items: {string.Join(", ", items)}"
                : $"Device '{resolvedReference}' has no device items.";
        }
    }
}
