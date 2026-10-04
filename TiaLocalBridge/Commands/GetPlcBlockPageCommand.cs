using System;
using System.Text;
using Siemens.Engineering;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    internal sealed class GetPlcBlockPageCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCKPAGE";
        public string Description => "Read a verified continuation from a local block snapshot; never reconnects to TIA or re-exports the block.";
        public string Usage => "GETPLCBLOCKPAGE|<artifact-id>|<nextCursor>";
        public string Example => "GETPLCBLOCKPAGE|<artifactId-from-GETPLCBLOCK>|<nextCursor-from-response>";
        public bool RequiresPortal => false;
        public bool ProducesJson => true;
        public string Execute(string[] args, TiaPortal portal)
        {
            if (args == null || args.Length != 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("Expected artifact id and exact nextCursor from the previous block page.");
            foreach (var budget in new[] { 8192, 4096, 2048, 1024, 512 })
            {
                var json = InventoryJson.Serialize(BlockSnapshotPager.Read(args[0], args[1], budget));
                if (Encoding.UTF8.GetByteCount(json) <= 16384) return json;
            }
            throw new InvalidOperationException("Block page metadata exceeds the 16 KiB response budget.");
        }
    }
}
