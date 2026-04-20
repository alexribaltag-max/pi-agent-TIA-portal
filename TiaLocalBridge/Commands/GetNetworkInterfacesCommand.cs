using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class GetNetworkInterfacesCommand : ITiaCommand
    {
        public string Name => "GETNETWORKINTERFACES";
        public string Description => "Lists the network interfaces available on a device.";
        public string Usage => "GETNETWORKINTERFACES|<device-reference>";
        public string Example => "GETNETWORKINTERFACES|DemoProject/PLC_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 1) throw new ArgumentException($"Expected one argument. Usage: {Usage}");
            
            var deviceRef = providedArgs[0];
            var device = CommandSupport.ResolveDeviceByReference(portal, deviceRef).Device;
            
            var interfaces = new List<string>();
            foreach (DeviceItem item in device.DeviceItems)
            {
                CollectInterfaces(item, interfaces);
            }
            
            if (!interfaces.Any())
            {
                return $"No network interfaces found on device '{deviceRef}'.";
            }
            
            return $"Interfaces on '{deviceRef}': {string.Join(", ", interfaces)}";
        }

        private void CollectInterfaces(DeviceItem item, List<string> list)
        {
            var intf = item.GetService<NetworkInterface>();
            if (intf != null)
            {
                list.Add(item.Name);
            }
            foreach (DeviceItem sub in item.DeviceItems)
            {
                CollectInterfaces(sub, list);
            }
        }
    }
}
