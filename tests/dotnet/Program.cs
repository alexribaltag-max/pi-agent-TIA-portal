using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TiaLocalBridge.Services;

internal static class Program
{
    private static int passed;
    private static int failed;

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static Exception Throws(Action action, string contains = null)
    {
        try { action(); }
        catch (Exception ex) { if (contains != null) Assert(ex.Message.Contains(contains), ex.Message); return ex; }
        throw new Exception("Expected failure");
    }
    private static InventoryDevice Device(string name, string kind = "root", string[] path = null, string project = "P", object handle = null)
    {
        return new InventoryDevice { Handle = handle ?? new object(), Identity = new DeviceReference(project, kind, path ?? new string[0], name), TypeIdentifier = "test:type" };
    }
    private static InventoryGroup Group(string name, InventoryDevice[] devices = null, InventoryGroup[] children = null)
    {
        return new InventoryGroup { Name = name, Handle = new object(), Devices = () => devices ?? new InventoryDevice[0], Groups = () => children ?? new InventoryGroup[0] };
    }
    private static InventorySource Source(InventoryDevice[] root = null, InventoryDevice[] ungrouped = null, InventoryGroup[] groups = null, string name = "P")
    {
        return new InventorySource { ProjectName = name, Project = new object(), RootDevices = () => root ?? new InventoryDevice[0], UngroupedDevices = () => ungrouped ?? new InventoryDevice[0], Groups = () => groups ?? new InventoryGroup[0] };
    }
    private static InventoryDevice Resolve(DeviceInventory inventory, string value) { return DeviceReferenceResolver.Resolve(new[] { inventory }, value).Device; }
    private static DeviceInventoryQuery Query(params string[] options) { return DeviceInventoryQuery.Parse(new[] { "P" }.Concat(options).ToArray()); }
    private static IEnumerable<InventoryDevice> Interrupted(InventoryDevice device)
    {
        yield return device;
        throw new InvalidOperationException("enumeration interrupted");
    }

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--encode-vectors")
        {
            using (var vectors = JsonDocument.Parse(File.ReadAllText(args[1])))
            {
                foreach (var vector in vectors.RootElement.GetProperty("valid").EnumerateArray())
                {
                    var p = vector.GetProperty("parts");
                    Console.WriteLine(new DeviceReference(p[0].GetString(), p[1].GetString(), p[2].EnumerateArray().Select(x => x.GetString()).ToArray(), p[3].GetString()).Encode());
                }
            }
            return 0;
        }

        Test("production C# codec round-trips all Phase 0 vectors", () =>
        {
            using (var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "device-references.json"))))
            {
                foreach (var vector in vectors.RootElement.GetProperty("valid").EnumerateArray())
                {
                    var p = vector.GetProperty("parts");
                    var reference = new DeviceReference(p[0].GetString(), p[1].GetString(), p[2].EnumerateArray().Select(x => x.GetString()).ToArray(), p[3].GetString());
                    var encoded = reference.Encode();
                    var decoded = DeviceReference.Decode(encoded);
                    Assert(decoded.Encode() == encoded);
                    Assert(decoded.Name == p[3].GetString());
                    Assert(decoded.GroupPath.SequenceEqual(reference.GroupPath));
                    Assert(!encoded.Any(c => "|/\r\n\t".Contains(c)));
                }
                foreach (var p in vectors.RootElement.GetProperty("invalidParts").EnumerateArray())
                {
                    var encoded = "tia-device:v1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(p.GetRawText())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    Throws(() => DeviceReference.Decode(encoded));
                }
            }
            Assert(new DeviceReference("P", "root", new string[0], "D").Encode() == "tia-device:v1:WyJQIiwicm9vdCIsW10sIkQiXQ");
        });
        Test("codec rejects malformed, noncanonical, unknown versions and invalid Unicode", () =>
        {
            var valid = Device("D").CanonicalReference;
            foreach (var bad in new[] { "", "P/D", valid + "=", valid + "|", valid.Replace(":v1:", ":v2:"), "tia-device:v1:_w", "tia-device:v1:A", "TIA-DEVICE:v1:AAAA" })
                Throws(() => DeviceReference.Decode(bad));
            Throws(() => new DeviceReference("P", "root", new string[0], "\ud800"));
            Throws(() => Device(new string('x', 9000)).CanonicalReference.ToString());
            var spaced = "tia-device:v1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("[ \"P\",\"root\",[],\"D\"]")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Throws(() => DeviceReference.Decode(spaced));
        });
        Test("complete root, ungrouped and nested traversal retains non-PLC devices", () =>
        {
            var root = Device("Root");
            var io = Device("IO", "ungrouped");
            var hmi = Device("HMI", "userGroup", new[] { "Line" });
            var drive = Device("Drive", "userGroup", new[] { "Line", "Nested" });
            var inventory = DeviceInventoryService.Collect(Source(new[] { root }, new[] { io }, new[] { Group("Line", new[] { hmi }, new[] { Group("Nested", new[] { drive }) }) }));
            Assert(inventory.Complete && inventory.Devices.SequenceEqual(new[] { root, io, hmi, drive }));
            foreach (var device in inventory.Devices)
            {
                Assert(ReferenceEquals(Resolve(inventory, device.CanonicalReference).Handle, device.Handle));
                Assert(ReferenceEquals(Resolve(inventory, device.LegacyReference).Handle, device.Handle));
                Assert(ReferenceEquals(Resolve(inventory, device.Identity.Name).Handle, device.Handle));
            }
        });
        Test("same handle duplicate exposure appears once using owner location", () =>
        {
            var item = Device("IO", "ungrouped");
            var inventory = DeviceInventoryService.Collect(Source(new[] { item }, new[] { item }));
            Assert(inventory.Complete && inventory.Devices.Count == 1);
            Assert(inventory.Devices[0].Identity.ContainerKind == "ungrouped");
        });
        Test("different wrappers at one location fail closed rather than merge by name", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("D"), Device("D") }));
            Assert(!inventory.Complete && inventory.Devices.Count == 2);
            Assert(inventory.Warnings.Any(w => w.Contains("IDENTITY_UNVERIFIED")));
            Throws(() => Resolve(inventory, inventory.Devices[0].CanonicalReference), "INVENTORY_INCOMPLETE");
        });
        Test("same handle with changed metadata is incomplete", () =>
        {
            var handle = new object();
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("A", handle: handle), Device("B", handle: handle) }));
            Assert(!inventory.Complete);
        });
        Test("duplicate names across groups have canonical identities and no legacy aliases", () =>
        {
            var a = Device("PLC", "userGroup", new[] { "A" });
            var b = Device("PLC", "userGroup", new[] { "B" });
            var inventory = DeviceInventoryService.Collect(Source(groups: new[] { Group("B", new[] { b }), Group("A", new[] { a }) }));
            Assert(inventory.Complete && inventory.Devices.SequenceEqual(new[] { a, b }));
            Assert(a.LegacyReference == null && b.LegacyReference == null);
            Assert(Resolve(inventory, a.CanonicalReference) == a && Resolve(inventory, b.CanonicalReference) == b);
            Throws(() => Resolve(inventory, "PLC"), "AMBIGUOUS_DEVICE");
            Throws(() => Resolve(inventory, "P/PLC"), "AMBIGUOUS_DEVICE");
        });
        Test("slash names, accented names and legacy case-insensitivity survive", () =>
        {
            var slash = Device("S7-1500/ET200MP station_1", project: "PackagingMachine");
            var unicode = Device("Estación ñ", project: "PackagingMachine");
            var inventory = DeviceInventoryService.Collect(Source(new[] { slash, unicode }, name: "PackagingMachine"));
            Assert(Resolve(inventory, "PackagingMachine/S7-1500/ET200MP station_1") == slash);
            Assert(Resolve(inventory, "S7-1500/ET200MP station_1") == slash);
            Assert(Resolve(inventory, "packagingmachine/ESTACIÓN Ñ") == unicode);
            Throws(() => Resolve(inventory, "PackagingMachine/Estacion n"), "DEVICE_NOT_FOUND");
        });
        Test("canonical names are ordinal and preserve Unicode normalization", () =>
        {
            var a = Device("é"); var b = Device("e\u0301");
            var inventory = DeviceInventoryService.Collect(Source(new[] { a, b }));
            Assert(inventory.Complete && a.CanonicalReference != b.CanonicalReference);
            Assert(Resolve(inventory, a.CanonicalReference) == a);
            Assert(Resolve(inventory, b.CanonicalReference) == b);
        });
        Test("reserved canonical-prefix failures never fall back to a legacy name", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("tia-device:v9:bogus") }));
            Throws(() => Resolve(inventory, "tia-device:v9:bogus"), "Invalid canonical");
        });
        Test("cross-project ambiguity is explicit; qualified and canonical resolution work", () =>
        {
            var a = DeviceInventoryService.Collect(Source(new[] { Device("PLC") }));
            var b = DeviceInventoryService.Collect(Source(new[] { Device("PLC", project: "Q") }, name: "Q"));
            Throws(() => DeviceReferenceResolver.Resolve(new[] { a, b }, "PLC"), "AMBIGUOUS_DEVICE");
            Assert(DeviceReferenceResolver.Resolve(new[] { a, b }, "Q/PLC").Inventory == b);
            Assert(DeviceReferenceResolver.Resolve(new[] { a, b }, b.Devices[0].CanonicalReference).Inventory == b);
            Throws(() => DeviceReferenceResolver.Resolve(new[] { a, a }, a.Devices[0].CanonicalReference), "ambiguous");
        });
        Test("missing projects and duplicate project identities cannot resolve", () =>
        {
            Throws(() => DeviceReferenceResolver.Resolve(new DeviceInventory[0], "D"), "No open projects");
            var a = DeviceInventoryService.Collect(Source(new[] { Device("OnlyHere") }));
            var b = DeviceInventoryService.Collect(Source());
            Throws(() => DeviceReferenceResolver.Resolve(new[] { a, b }, "OnlyHere"), "ambiguous");
        });
        Test("project-prefix alias collisions are suppressed without merging canonical identities", () =>
        {
            var a = DeviceInventoryService.Collect(Source(new[] { Device("G/D") }));
            var b = DeviceInventoryService.Collect(Source(new[] { Device("D", project: "P/G") }, name: "P/G"));
            DeviceReferenceResolver.SuppressAmbiguousLegacyAliases(a, new[] { "P/G" });
            Assert(a.Devices[0].LegacyReference == null);
            Throws(() => DeviceReferenceResolver.Resolve(new[] { a, b }, "P/G/D"), "AMBIGUOUS_DEVICE");
            Assert(DeviceReferenceResolver.Resolve(new[] { a, b }, a.Devices[0].CanonicalReference).Inventory == a);
            Assert(b.Devices[0].LegacyReference == null);
        });
        Test("incorrect owner project marks inventory incomplete", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("D", project: "Wrong") }));
            Assert(!inventory.Complete && inventory.Devices.Count == 0);
        });
        Test("empty project is distinguishable from scope failure", () =>
        {
            var empty = DeviceInventoryService.Collect(Source());
            Assert(empty.Complete && empty.Devices.Count == 0 && empty.WarningCount == 0);
            var source = Source(); source.UngroupedDevices = () => throw new InvalidOperationException("denied");
            var partial = DeviceInventoryService.Collect(source);
            Assert(!partial.Complete && partial.Devices.Count == 0 && partial.Warnings[0].Contains("ungrouped"));
        });
        Test("partial iteration retains collected entries and visits other scopes", () =>
        {
            var source = Source(ungrouped: new[] { Device("IO", "ungrouped") });
            source.RootDevices = () => Interrupted(Device("Root"));
            var inventory = DeviceInventoryService.Collect(source);
            Assert(!inventory.Complete && inventory.Devices.Count == 2);
            Assert(inventory.Devices.All(d => d.LegacyReference == null));
            Throws(() => Resolve(inventory, "Root"), "INVENTORY_INCOMPLETE");
        });
        Test("broken group does not prevent sibling discovery", () =>
        {
            var bad = Group("Bad"); bad.Devices = () => throw new InvalidOperationException("broken group");
            var good = Group("Good", new[] { Device("PLC", "userGroup", new[] { "Good" }) });
            var inventory = DeviceInventoryService.Collect(Source(groups: new[] { bad, good }));
            Assert(!inventory.Complete && inventory.Devices.Count == 1);
            Assert(inventory.Warnings.Any(w => w.Contains("groups/Bad")));
        });
        Test("group cycle is bounded and explicit", () =>
        {
            var group = Group("Cycle"); group.Groups = () => new[] { group };
            var inventory = DeviceInventoryService.Collect(Source(groups: new[] { group }));
            Assert(!inventory.Complete && inventory.Warnings.Any(w => w.Contains("cyclic")));
        });
        Test("group depth guard is explicit", () =>
        {
            var group = Group("64");
            for (int n = 63; n >= 0; n--) group = Group(n.ToString(), children: new[] { group });
            var inventory = DeviceInventoryService.Collect(Source(groups: new[] { group }));
            Assert(!inventory.Complete && inventory.Warnings.Any(w => w.Contains("depth")));
        });
        Test("exactly 64 nested groups with no deeper children remains complete", () =>
        {
            var group = Group("63");
            for (int n = 62; n >= 0; n--) group = Group(n.ToString(), children: new[] { group });
            Assert(DeviceInventoryService.Collect(Source(groups: new[] { group })).Complete);
        });
        Test("unrelated partial project does not block a qualified target", () =>
        {
            var a = DeviceInventoryService.Collect(Source(new[] { Device("PLC") }));
            var bad = Source(name: "Q"); bad.Groups = () => throw new Exception("error");
            var b = DeviceInventoryService.Collect(bad);
            Assert(DeviceReferenceResolver.Resolve(new[] { a, b }, "P/PLC").Inventory == a);
            Throws(() => DeviceReferenceResolver.Resolve(new[] { a, b }, "PLC"), "INVENTORY_INCOMPLETE");
        });
        Test("ADDDEVICE preflight covers every scope and refuses partial inventory", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("Root") }, new[] { Device("IO", "ungrouped") }, new[] { Group("G", new[] { Device("PLC", "userGroup", new[] { "G" }) }) }));
            foreach (var name in new[] { "root", "io", "plc" }) Throws(() => DeviceReferenceResolver.EnsureNameAvailable(inventory, name), "already exists");
            DeviceReferenceResolver.EnsureNameAvailable(inventory, "New");
            inventory.Warn("test", "incomplete");
            Throws(() => DeviceReferenceResolver.EnsureNameAvailable(inventory, "New"), "INVENTORY_INCOMPLETE");
        });
        Test("error candidates have count and character budgets", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(Enumerable.Range(0, 1000).Select(n => Device("D" + n)).ToArray()));
            var message = Throws(() => Resolve(inventory, "missing"), "omitted").Message;
            Assert(message.Length < 3500 && message.Contains("GETDEVICEINVENTORY"));
        });
        Test("warnings are capped and total failures remain visible", () =>
        {
            var groups = Enumerable.Range(0, 30).Select(n => { var g = Group(n.ToString()); g.Devices = () => throw new Exception(new string('x', 1000)); return g; }).ToArray();
            var inventory = DeviceInventoryService.Collect(Source(groups: groups));
            Assert(inventory.Warnings.Count == 20 && inventory.WarningCount == 30 && !inventory.Complete);
            Assert(inventory.Warnings.All(w => w.Length < 430));
            var page = DeviceInventoryPaging.Read(inventory, Query());
            Assert(!page.Complete && page.WarningCount == 30 && page.Warnings.Length == 20);
        });
        Test("new options reject blanks, unknowns, duplicates and invalid ranges", () =>
        {
            foreach (var options in new[] { new[] { "" }, new[] { "unknown=x" }, new[] { "scope=none" }, new[] { "scope=all", "scope=root" }, new[] { "limit=0" }, new[] { "limit=501" }, new[] { "limit=-1" }, new[] { "limit=1.5" }, new[] { "fields=name,name" }, new[] { "fields=name,bogus" }, new[] { "fields=capabilities" }, new[] { "cursor=" }, new[] { "capabilities=1" }, new[] { "limit" } })
                Throws(() => Query(options));
            Throws(() => DeviceInventoryQuery.Parse(new[] { "" }));
            Throws(() => Query("cursor=" + new string('x', 257)));
        });
        Test("scope filtering and field projection retain mandatory canonical reference", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("Root") }, new[] { Device("IO", "ungrouped") }));
            var page = DeviceInventoryPaging.Read(inventory, Query("scope=ungrouped", "fields=name"));
            Assert(page.Total == 1 && page.Devices[0].Name == "IO" && page.Devices[0].Reference != null);
            using (var json = JsonDocument.Parse(InventoryJson.Serialize(page)))
            {
                var row = json.RootElement.GetProperty("devices")[0];
                Assert(!row.TryGetProperty("typeIdentifier", out _));
                Assert(row.EnumerateObject().Count() == 2);
            }
        });
        Test("inventory pages continue deterministically without missing or duplicate rows", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(Enumerable.Range(0, 25).Select(n => Device("PLC" + n.ToString("D2"))).Reverse().ToArray()));
            var seen = new List<string>(); string cursor = null;
            do
            {
                var query = Query("limit=7"); query.Cursor = cursor;
                var page = DeviceInventoryPaging.Read(inventory, query);
                Assert(page.Complete && page.Total == 25 && page.Devices.Count <= 7);
                seen.AddRange(page.Devices.Select(d => d.Reference)); cursor = page.NextCursor;
                Assert(page.HasMore == (cursor != null));
            } while (cursor != null);
            Assert(seen.SequenceEqual(inventory.Devices.Select(d => d.CanonicalReference)) && seen.Distinct().Count() == 25);
        });
        Test("cursor rejects tampering, wrong projection, scope, and inventory changes", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("A"), Device("B") }));
            var cursor = DeviceInventoryPaging.Read(inventory, Query("limit=1")).NextCursor;
            foreach (var options in new[] { new[] { "cursor=" + cursor.Replace("1.1.", "1.0.") }, new[] { "cursor=" + cursor, "fields=name" }, new[] { "cursor=" + cursor, "scope=root" } })
                Throws(() => DeviceInventoryPaging.Read(inventory, Query(options)), "cursor");
            inventory.Devices[0].TypeIdentifier = "changed";
            Throws(() => DeviceInventoryPaging.Read(inventory, Query("cursor=" + cursor)), "cursor");
        });
        Test("16 KiB result ceiling pages before line-level truncation", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(Enumerable.Range(0, 100).Select(n => Device(new string('ñ', 120) + n)).ToArray()));
            var page = DeviceInventoryPaging.Read(inventory, Query("limit=500"));
            Assert(Encoding.UTF8.GetByteCount(InventoryJson.Serialize(page)) <= 16384);
            Assert(page.HasMore && page.NextCursor != null && page.Devices.Count < 100 && page.Devices.Count > 0);
        });
        Test("oversized single row fails explicitly and field projection can recover", () =>
        {
            var device = Device("D"); device.TypeIdentifier = new string('x', 17000);
            var inventory = DeviceInventoryService.Collect(Source(new[] { device }));
            Throws(() => DeviceInventoryPaging.Read(inventory, Query()), "16 KiB");
            var page = DeviceInventoryPaging.Read(inventory, Query("fields=name"));
            Assert(page.Devices.Count == 1 && !page.HasMore);
        });
        Test("group scope filtering and a changed page limit preserve continuation", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("R") }, groups: new[] { Group("G", new[] { Device("A", "userGroup", new[] { "G" }), Device("B", "userGroup", new[] { "G" }) }) }));
            var first = DeviceInventoryPaging.Read(inventory, Query("scope=groups", "limit=1"));
            Assert(first.Total == 2 && first.Devices.Single().Name == "A");
            var next = DeviceInventoryPaging.Read(inventory, Query("scope=groups", "limit=10", "cursor=" + first.NextCursor));
            Assert(!next.HasMore && next.Devices.Single().Name == "B");
        });
        Test("capability inspection is opt-in and only for candidate page rows", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("A"), Device("B") }));
            int calls = 0;
            Func<InventoryDevice, string[]> inspect = d => { calls++; return new[] { "PLC" }; };
            DeviceInventoryPaging.Read(inventory, Query(), inspect); Assert(calls == 0);
            var page = DeviceInventoryPaging.Read(inventory, Query("capabilities=true", "limit=1"), inspect);
            Assert(calls == 1 && page.Devices[0].Capabilities.Single() == "PLC");
            Throws(() => DeviceInventoryPaging.Read(inventory, Query("capabilities=true")), "provider unavailable");
        });
        Test("serializer emits parseable control/Unicode values and explicit empty final page", () =>
        {
            var inventory = DeviceInventoryService.Collect(Source(new[] { Device("\"/\\\n\tñ🔧") }));
            var page = DeviceInventoryPaging.Read(inventory, Query());
            using (var json = JsonDocument.Parse(InventoryJson.Serialize(page))) Assert(json.RootElement.GetProperty("devices")[0].GetProperty("name").GetString() == "\"/\\\n\tñ🔧");
            page = DeviceInventoryPaging.Read(DeviceInventoryService.Collect(Source()), Query());
            Assert(page.Complete && page.Total == 0 && !page.HasMore && page.NextCursor == null);
        });
        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed (no Siemens assemblies or TIA connection)");
        return failed == 0 ? 0 : 1;
    }
}
