using System;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class HelpCommand : ITiaCommand
    {
        private readonly Func<string> _helpTextFactory;

        public HelpCommand(Func<string> helpTextFactory)
        {
            _helpTextFactory = helpTextFactory;
        }

        public string Name => "HELP";
        public string Description => "Displays all available commands with their description, usage, and example, including that GETDEVICEITEMS lists PLC modules from a device reference returned by GETDEVICES.";
        public string Usage => "HELP";
        public string Example => "HELP";
        public bool RequiresPortal => false;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            CommandSupport.RequireNoArguments(args, this);
            return _helpTextFactory();
        }
    }
}
