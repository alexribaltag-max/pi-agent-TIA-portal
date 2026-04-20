using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class ConnectToSubnetCommand : ITiaCommand
    {
        public string Name => "CONNECTTOSUBNET";
        public string Description => "Connects a device interface to a subnet.";
        public string Usage => "CONNECTTOSUBNET|<device-reference>|<interface-name>|<subnet-name>";
        public string Example => "CONNECTTOSUBNET|DemoProject/PLC_1|PROFINET interface_1|PN/IE_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 3) throw new ArgumentException($"Expected three arguments. Usage: {Usage}");
            
            var deviceRef = providedArgs[0];
            var interfaceName = providedArgs[1];
            var subnetName = providedArgs[2];
            
            var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceRef);
            var project = resolution.Project;
            var device = resolution.Device;
            
            var subnet = project.Subnets.FirstOrDefault(s => string.Equals(s.Name, subnetName, StringComparison.OrdinalIgnoreCase));
            if (subnet == null) throw new InvalidOperationException($"Subnet '{subnetName}' not found in project.");
            
            var netIntf = FindInterface(device, interfaceName);
            if (netIntf == null) throw new InvalidOperationException($"Interface '{interfaceName}' not found on device.");
            
            var node = netIntf.Nodes.FirstOrDefault();
            if (node == null) throw new InvalidOperationException($"No network node found on interface '{interfaceName}'.");
            
            node.ConnectToSubnet(subnet);
            return $"Connected interface '{interfaceName}' of '{deviceRef}' to subnet '{subnetName}'.";
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
