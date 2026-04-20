using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class GetIpAddressCommand : ITiaCommand
    {
        public string Name => "GETIPADDRESS";
        public string Description => "Gets the IP address and subnet mask of a network interface.";
        public string Usage => "GETIPADDRESS|<device-reference>|<interface-name>";
        public string Example => "GETIPADDRESS|DemoProject/PLC_1|PROFINET interface_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 2) throw new ArgumentException($"Expected two arguments. Usage: {Usage}");
            
            var deviceRef = providedArgs[0];
            var interfaceName = providedArgs[1];
            
            var device = CommandSupport.ResolveDeviceByReference(portal, deviceRef).Device;
            
            var netIntf = FindInterface(device, interfaceName);
            if (netIntf == null) throw new InvalidOperationException($"Interface '{interfaceName}' not found on device.");
            
            var node = netIntf.Nodes.FirstOrDefault();
            if (node == null) throw new InvalidOperationException($"No network node found on interface '{interfaceName}'.");
            
            var address = CommandSupport.TryGetAttributeValue(node, "Address", out string addressError);
            var subnetMask = CommandSupport.TryGetAttributeValue(node, "SubnetMask", out string subnetError);
            
            var addressStr = string.IsNullOrWhiteSpace(addressError) ? CommandSupport.FormatEngineeringValue(address) : $"<read-error:{addressError}>";
            var subnetStr = string.IsNullOrWhiteSpace(subnetError) ? CommandSupport.FormatEngineeringValue(subnetMask) : $"<read-error:{subnetError}>";
            
            return $"Interface '{interfaceName}' on '{deviceRef}' has IP Address '{addressStr}' and Subnet Mask '{subnetStr}'.";
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
