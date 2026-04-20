using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class SetNodePropertyCommand : ITiaCommand
    {
        public string Name => "SETNODEPROPERTY";
        public string Description => "Sets a property on a network node (e.g., Address, SubnetMask, ProfinetName).";
        public string Usage => "SETNODEPROPERTY|<device-reference>|<interface-name>|<property-name>|<value>";
        public string Example => "SETNODEPROPERTY|DemoProject/PLC_1|PROFINET interface_1|Address|192.168.0.10";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<interface-name>", "<property-name>", "<value>");
            var deviceRef = providedArgs[0];
            var interfaceName = providedArgs[1];
            var propertyName = providedArgs[2];
            var rawValue = providedArgs[3];
            
            var device = CommandSupport.ResolveDeviceByReference(portal, deviceRef).Device;
            
            var netIntf = FindInterface(device, interfaceName);
            if (netIntf == null) throw new InvalidOperationException($"Interface '{interfaceName}' not found on device.");
            
            var node = netIntf.Nodes.FirstOrDefault();
            if (node == null) throw new InvalidOperationException($"No network node found on interface '{interfaceName}'.");
            
            var attributeInfo = CommandSupport.ResolveAttributeInfo(node, propertyName);

            if (attributeInfo.AccessMode != EngineeringAttributeAccessMode.Write && attributeInfo.AccessMode != EngineeringAttributeAccessMode.ReadWrite)
            {
                throw new InvalidOperationException($"Property '{attributeInfo.Name}' is not writable on node. Access mode is {attributeInfo.AccessMode}.");
            }

            var previousValue = CommandSupport.TryGetAttributeValue(node, attributeInfo.Name, out string previousReadError);
            var convertedValue = CommandSupport.ConvertTextToAttributeValue(node, attributeInfo, rawValue);

            try
            {
                node.SetAttribute(attributeInfo.Name, convertedValue);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set property '{attributeInfo.Name}' on node. Details: {ex.Message}");
            }

            var updatedValue = CommandSupport.TryGetAttributeValue(node, attributeInfo.Name, out string updatedReadError);
            var previousValueText = string.IsNullOrWhiteSpace(previousReadError)
                ? CommandSupport.FormatEngineeringValue(previousValue)
                : $"<read-error:{previousReadError}>";
            var updatedValueText = string.IsNullOrWhiteSpace(updatedReadError)
                ? CommandSupport.FormatEngineeringValue(updatedValue)
                : $"<read-error:{updatedReadError}>";

            return $"Updated node property '{attributeInfo.Name}' on interface '{interfaceName}' for device '{deviceRef}' [OldValue={previousValueText}, NewValue={updatedValueText}].";
        }

        private NetworkInterface FindInterface(Device device, string name)
        {
            foreach (DeviceItem item in device.DeviceItems)
            {
                var intf = FindInterfaceRec(item, name);
                if (intf != null) return intf;
            }
            return null;
        }

        private NetworkInterface FindInterfaceRec(DeviceItem item, string name)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                var intf = item.GetService<NetworkInterface>();
                if (intf != null) return intf;
            }
            foreach (DeviceItem sub in item.DeviceItems)
            {
                var intf = FindInterfaceRec(sub, name);
                if (intf != null) return intf;
            }
            return null;
        }
    }
}
