using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace TiaLocalBridge.Commands
{
    internal class ConnectProfinetCommand : ITiaCommand
    {
        public string Name => "CONNECTPROFINET";
        public string Description => "Connects an IO device (slave) to an IO controller (master). If the master doesn't have an IO system, it creates one using the specified subnet name.";
        public string Usage => "CONNECTPROFINET|<master-device-ref>|<master-interface-name>|<slave-device-ref>|<slave-interface-name>|<subnet-name>";
        public string Example => "CONNECTPROFINET|DemoProject/PLC_1|PROFINET interface_1|DemoProject/ET200SP_1|PROFINET interface_1|PN/IE_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 5) throw new ArgumentException($"Expected five arguments. Usage: {Usage}");
            
            var masterRef = providedArgs[0];
            var masterIntfName = providedArgs[1];
            var slaveRef = providedArgs[2];
            var slaveIntfName = providedArgs[3];
            var subnetName = providedArgs[4];
            
            var masterDevice = CommandSupport.ResolveDeviceByReference(portal, masterRef).Device;
            var slaveDevice = CommandSupport.ResolveDeviceByReference(portal, slaveRef).Device;
            
            var masterIntf = FindInterface(masterDevice, masterIntfName);
            if (masterIntf == null) throw new InvalidOperationException($"Master interface '{masterIntfName}' not found.");
            
            var slaveIntf = FindInterface(slaveDevice, slaveIntfName);
            if (slaveIntf == null) throw new InvalidOperationException($"Slave interface '{slaveIntfName}' not found.");
            
            var ioController = masterIntf.IoControllers.FirstOrDefault();
            if (ioController == null) throw new InvalidOperationException("Master interface does not have an IO Controller feature.");
            
            var ioSystem = ioController.IoSystem;
            if (ioSystem == null)
            {
                ioSystem = ioController.CreateIoSystem(subnetName);
            }
            
            var ioConnector = slaveIntf.IoConnectors.FirstOrDefault();
            if (ioConnector == null) throw new InvalidOperationException("Slave interface does not have an IO Connector feature.");
            
            ioConnector.ConnectToIoSystem(ioSystem);
            
            return $"Connected slave '{slaveRef}' to master '{masterRef}' via IO System '{ioSystem.Name}'.";
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
