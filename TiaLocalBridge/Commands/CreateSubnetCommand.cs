using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class CreateSubnetCommand : ITiaCommand
    {
        public string Name => "CREATESUBNET";
        public string Description => "Creates a new subnet in the project.";
        public string Usage => "CREATESUBNET|<project-name>|<subnet-name>";
        public string Example => "CREATESUBNET|DemoProject|PN/IE_1";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length != 2)
            {
                throw new ArgumentException($"Expected two arguments. Usage: {Usage}");
            }
            var projectName = providedArgs[0];
            var subnetName = providedArgs[1];
            
            var project = portal.Projects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            if (project == null) throw new InvalidOperationException($"Project '{projectName}' not found.");
            
            var existing = project.Subnets.FirstOrDefault(s => string.Equals(s.Name, subnetName, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return $"Subnet '{subnetName}' already exists.";
            
            var subnet = project.Subnets.Create("System:Subnet.Ethernet", subnetName);
            return $"Created subnet '{subnet.Name}'.";
        }
    }
}
