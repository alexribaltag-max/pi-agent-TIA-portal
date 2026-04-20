using System.IO;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class OpenProjectCommand : ITiaCommand
    {
        public string Name => "OPEN";
        public string Description => "Opens an existing TIA Portal project from a project file path.";
        public string Usage => "OPEN|<project-file-path>";
        public string Example => @"OPEN|C:\Projects\DemoProject.ap20";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<project-file-path>");
            var path = new FileInfo(providedArgs[0]);
            var project = portal.Projects.Open(path);
            return $"Project '{project.Name}' opened.";
        }
    }
}
