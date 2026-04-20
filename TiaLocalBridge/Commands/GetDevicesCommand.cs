using System;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;

namespace TiaLocalBridge.Commands
{
    internal class GetDevicesCommand : ITiaCommand
    {
        public string Name => "GETDEVICES";
        public string Description => "Lists all devices in the selected open project and returns the device reference to use with GETDEVICEITEMS for PLC modules. If only one project is open, the project name argument is optional.";
        public string Usage => "GETDEVICES|[project-name]";
        public string Example => "GETDEVICES|DemoProject";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length > 1)
            {
                throw new ArgumentException($"Too many arguments. Expected zero or one optional '<project-name>' argument. {Description} Usage: {Usage}. Example: {Example}");
            }

            var openProjects = portal.Projects.ToList();
            if (!openProjects.Any())
            {
                throw new InvalidOperationException("No open projects. Open or create a project first.");
            }

            Project project;
            if (providedArgs.Length == 1)
            {
                var projectName = providedArgs[0];
                project = openProjects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));

                if (project == null)
                {
                    throw new InvalidOperationException($"Project '{projectName}' is not open. Open projects: {string.Join(", ", openProjects.Select(p => p.Name))}");
                }
            }
            else
            {
                if (openProjects.Count > 1)
                {
                    throw new InvalidOperationException($"Multiple projects are open. Please specify '<project-name>'. Open projects: {string.Join(", ", openProjects.Select(p => p.Name))}. Usage: {Usage}. Example: {Example}");
                }

                project = openProjects[0];
            }

            var devices = project.Devices
                .Select(device => FormatDevice(project, device))
                .ToList();

            return devices.Any()
                ? $"Project '{project.Name}' devices: {string.Join(", ", devices)}"
                : $"Project '{project.Name}' has no devices.";
        }

        private static string FormatDevice(Project project, Device device)
        {
            return $"{device.Name} [Reference={CommandSupport.GetDeviceReference(project, device)}, Type={device.TypeIdentifier}]";
        }
    }
}
