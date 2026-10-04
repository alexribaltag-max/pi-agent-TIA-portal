using System;
using System.Linq;
using System.Runtime.Serialization;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    [DataContract]
    internal sealed class ProjectOverviewResult
    {
        [DataMember] public string name { get; set; }
        [DataMember] public string path { get; set; }
        [DataMember] public bool isPrimary { get; set; }
        [DataMember] public int deviceCount { get; set; }
        [DataMember] public DeviceOverviewInfo[] devices { get; set; }
    }

    [DataContract]
    internal sealed class DeviceOverviewInfo
    {
        [DataMember] public string reference { get; set; }
        [DataMember] public string name { get; set; }
        [DataMember] public string typeIdentifier { get; set; }
        [DataMember] public string containerKind { get; set; }
        [DataMember] public string groupPath { get; set; }
        [DataMember] public string[] capabilities { get; set; }
    }

    internal sealed class GetProjectOverviewCommand : ITiaCommand
    {
        public string Name => "GETPROJECTOVERVIEW";
        public string Description => "Get a project overview including basic device capabilities in one call.";
        public string Usage => "GETPROJECTOVERVIEW|<project-name-or-empty-for-primary>";
        public string Example => "GETPROJECTOVERVIEW|PackagingMachine";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var projectName = args.Length > 0 ? args[0].Trim() : "";
            var project = string.IsNullOrEmpty(projectName)
                ? portal.Projects.FirstOrDefault()
                : portal.Projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project == null)
            {
                throw new ArgumentException(string.IsNullOrEmpty(projectName) ? "No open project." : $"Project '{projectName}' not found.");
            }

            var inventory = OpennessDeviceInventory.Get(project);

            var page = DeviceInventoryPaging.Read(inventory, new DeviceInventoryQuery { ProjectName = project.Name, Scope = "all", Limit = 1000, Capabilities = true, Fields = new[] { "reference", "name", "typeIdentifier", "containerKind", "groupPath", "capabilities" } }, d => new[] { d.TypeIdentifier });

            var result = new ProjectOverviewResult
            {
                name = project.Name,
                path = project.Path.FullName,
                isPrimary = project == portal.Projects.FirstOrDefault(),
                deviceCount = page.Total,
                devices = page.Devices.Select(d => new DeviceOverviewInfo
                {
                    reference = d.Reference,
                    name = d.Name,
                    typeIdentifier = d.TypeIdentifier,
                    containerKind = d.ContainerKind,
                    groupPath = d.GroupPath != null ? string.Join("/", d.GroupPath) : "",
                    capabilities = d.Capabilities
                }).ToArray()
            };

            return InventoryJson.Serialize(result);
        }
    }
}