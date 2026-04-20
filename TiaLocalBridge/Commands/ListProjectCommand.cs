using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class ListProjectsCommand : ITiaCommand
    {
        public string Name => "LIST";
        public string Description => "Lists the names of all currently open TIA Portal projects.";
        public string Usage => "LIST";
        public string Example => "LIST";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            CommandSupport.RequireNoArguments(args, this);

            var names = portal.Projects.Select(p => p.Name);
            return names.Any() ? string.Join(", ", names) : "No open projects";
        }
    }
}
