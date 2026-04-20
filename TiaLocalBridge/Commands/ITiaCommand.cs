using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal interface ITiaCommand
    {
        string Name { get; }
        string Description { get; }
        string Usage { get; }
        string Example { get; }
        bool RequiresPortal { get; }
        bool ProducesJson { get; }
        string Execute(string[] args, TiaPortal portal);
    }
}
