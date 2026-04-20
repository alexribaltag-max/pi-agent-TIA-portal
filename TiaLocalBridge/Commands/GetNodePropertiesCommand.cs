using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class GetNodePropertiesCommand : ITiaCommand
    {
        public string Name => "GETNODEPROPERTIES";
        public string Description => "Lists the available properties and their values for a network node on a given interface.";
        public string Usage => "GETNODEPROPERTIES|<device-reference>|<interface-name>";
        public string Example => "GETNODEPROPERTIES|DemoProject/PLC_1|PROFINET interface_1";
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
            
            var lines = new List<string>
            {
                $"Properties for network node on interface '{interfaceName}' (Device: '{deviceRef}'):"
            };

            var attributeInfos = node.GetAttributeInfos();
            foreach (var info in attributeInfos)
            {
                string valStr;
                if (info.AccessMode == EngineeringAttributeAccessMode.Read || info.AccessMode == EngineeringAttributeAccessMode.ReadWrite)
                {
                    try
                    {
                        var val = node.GetAttribute(info.Name);
                        valStr = CommandSupport.FormatEngineeringValue(val);
                    }
                    catch (Exception ex)
                    {
                        valStr = $"<error: {ex.Message}>";
                    }
                }
                else
                {
                    valStr = "<write-only>";
                }

                lines.Add($"  - {info.Name} [{info.AccessMode}] ({CommandSupport.DescribeSupportedTypes(info.SupportedTypes)}): {valStr}");
            }

            return string.Join(Environment.NewLine, lines);
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
