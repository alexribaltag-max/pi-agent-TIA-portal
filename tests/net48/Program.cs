using System;
using System.Linq;
using System.IO;
using System.Text;
using TiaLocalBridge.Services;

internal static class Program
{
    private static void Assert(bool condition) { if (!condition) throw new Exception("net48 smoke assertion failed"); }

    private static int Main()
    {
        var reference = new DeviceReference("Próyecto", "userGroup", new[] { "A/B", "ñ\t\n🔧" }, "D|\\\"/%");
        Assert(DeviceReference.Decode(reference.Encode()).Encode() == reference.Encode());
        Assert(new DeviceReference("P", "root", new string[0], "D").Encode() == "tia-device:v1:WyJQIiwicm9vdCIsW10sIkQiXQ");
        Console.WriteLine("PASS net48 production codec with Unicode and controls");

        var source = new InventorySource
        {
            ProjectName = "P", Project = new object(),
            RootDevices = () => new[] {
                new InventoryDevice { Handle = new object(), Identity = new DeviceReference("P", "root", new string[0], "A"), TypeIdentifier = "type" },
                new InventoryDevice { Handle = new object(), Identity = new DeviceReference("P", "root", new string[0], "B"), TypeIdentifier = "type" }
            },
            UngroupedDevices = () => new InventoryDevice[0],
            Groups = () => new[] { new InventoryGroup { Name = "Empty", Handle = new object(), Devices = () => new InventoryDevice[0], Groups = () => new InventoryGroup[0] } }
        };
        var inventory = DeviceInventoryService.Collect(source);
        Assert(inventory.Complete && inventory.Devices.Count == 2);
        Assert(DeviceReferenceResolver.Resolve(new[] { inventory }, "P/A").Device == inventory.Devices[0]);
        Console.WriteLine("PASS net48 production traversal/resolution");

        var page = DeviceInventoryPaging.Read(inventory, DeviceInventoryQuery.Parse(new[] { "P", "limit=1" }));
        var json = InventoryJson.Serialize(page);
        Assert(page.HasMore && Encoding.UTF8.GetByteCount(json) < 16384);
        var decoded = InventoryJson.Deserialize<InventoryPage>(json);
        Assert(decoded.Devices.Single().Reference == inventory.Devices[0].CanonicalReference && decoded.NextCursor == page.NextCursor);
        var next = DeviceInventoryPaging.Read(inventory, DeviceInventoryQuery.Parse(new[] { "P", "cursor=" + page.NextCursor }));
        Assert(!next.HasMore && next.Devices.Single().Name == "B");
        Console.WriteLine("PASS net48 production JSON DTO and signed paging");

        var root = Path.Combine(Path.GetTempPath(), "tia-net48-page-" + Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, id);
        try
        {
            Directory.CreateDirectory(directory);
            BlockSnapshot.Save(directory, new SnapshotManifest { artifactId = id, fidelity = "reconstructed", blockRef = "B" },
                new string[0], "INTERFACE Input\n" + new string('é', 12000) + "\n");
            var first = BlockSnapshotPager.ReadAt(root, id);
            Assert(first.hasMore && first.incompleteUnit && first.nextCursor != null);
            var second = BlockSnapshotPager.ReadAt(root, id, first.nextCursor);
            Assert(second.content.Length > 0 && InventoryJson.Serialize(second).Contains("incompleteUnit"));
            Console.WriteLine("PASS net48 production block snapshot and signed Unicode continuation");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine("RESULT: 4 smoke checks passed; no Siemens assemblies referenced or loaded.");
        return 0;
    }
}
