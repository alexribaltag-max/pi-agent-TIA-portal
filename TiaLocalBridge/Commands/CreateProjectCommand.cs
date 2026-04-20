using System.IO;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class CreateProjectCommand : ITiaCommand
    {
        public string Name => "CREATE";
        public string Description => "Creates a new TIA Portal project in the target directory with the provided project name.";
        public string Usage => "CREATE|<target-directory>|<project-name>";
        public string Example => @"CREATE|C:\Projects|DemoProject";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<target-directory>", "<project-name>");
            var dir = new DirectoryInfo(providedArgs[0]);
            var name = providedArgs[1];
            var project = portal.Projects.Create(dir, name);
            return $"Project '{project.Name}' created at {dir.FullName}.";
        }
    }
}
