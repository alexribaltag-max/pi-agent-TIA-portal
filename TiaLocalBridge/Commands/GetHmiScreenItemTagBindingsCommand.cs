using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Dynamization;

namespace TiaLocalBridge.Commands
{
    internal class GetHmiScreenItemTagBindingsCommand : ITiaCommand
    {
        public string Name => "GETHMISCREENITEMTAGBINDINGS";
        public string Description => "Lists direct and tag-dynamized bindings for one Unified HMI screen item so an agent can inspect how the item is linked to HMI tags.";
        public string Usage => "GETHMISCREENITEMTAGBINDINGS|<device-reference>|<screen-reference>|<item-name>";
        public string Example => "GETHMISCREENITEMTAGBINDINGS|DemoProject/HMI_1|Config/Overview|Io_Speed";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<screen-reference>", "<item-name>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var hmiSoftware = CommandSupport.TryGetUnifiedHmiSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (hmiSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain Unified HMI software.");
            }

            var screen = CommandSupport.ResolveUnifiedHmiScreen(hmiSoftware, providedArgs[1]);
            var item = CommandSupport.ResolveUnifiedHmiScreenItem(screen.Screen, providedArgs[2]);
            var bindings = CommandSupport.GetUnifiedHmiDynamizations(item)
                .OfType<TagDynamization>()
                .Select(binding => string.Format(
                    "{0} [Mode=TagDynamization, Tag={1}, PlcTag={2}, Address={3}, DataType={4}, ReadOnly={5}, UseIndirectAddressing={6}]",
                    binding.PropertyName,
                    string.IsNullOrWhiteSpace(binding.Tag) ? "<empty>" : binding.Tag,
                    string.IsNullOrWhiteSpace(binding.PlcTag) ? "<empty>" : binding.PlcTag,
                    string.IsNullOrWhiteSpace(binding.Address) ? "<empty>" : binding.Address,
                    string.IsNullOrWhiteSpace(binding.DataType) ? "<empty>" : binding.DataType,
                    binding.ReadOnly ? "true" : "false",
                    binding.UseIndirectAddressing ? "true" : "false"))
                .ToList();

            var processValueProperty = item.GetType().GetProperty("ProcessValue");
            if (processValueProperty != null && processValueProperty.CanRead)
            {
                var processValue = processValueProperty.GetValue(item, null) as string;
                if (!string.IsNullOrWhiteSpace(processValue))
                {
                    bindings.Insert(0, $"ProcessValue [Mode=DirectProperty, Value={processValue}]");
                }
            }

            return bindings.Any()
                ? $"Unified HMI screen item tag bindings for '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}': {string.Join(" || ", bindings)}"
                : $"Unified HMI screen item '{item.Name}' on screen '{screen.ScreenReference}' in device '{resolvedReference}' has no detected tag bindings.";
        }
    }
}
