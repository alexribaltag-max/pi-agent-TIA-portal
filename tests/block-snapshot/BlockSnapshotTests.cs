using System;
using System.IO;
using System.Linq;
using System.Text;
using TiaLocalBridge.Services;

internal static class BlockSnapshotTests
{
    private static int count;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
    private static string Fixture(string suffix) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../TiaLocalBridge/exports-kinds/", suffix));
    private static void Main()
    {
        var db = Fixture("DB_Data_docs/DB_Data.s7dcl");
        var lad = Fixture("LAD_Main_docs/LAD_Main.s7dcl");
        var res = Fixture("LAD_Main_docs/LAD_Main.s7res");
        Check(File.Exists(db) && File.Exists(res), "fixtures missing");
        var body = BlockSnapshot.ReadDocument(new[] { db }, out var warning);
        Check(body.Contains("plcInternal") && body[0] != '\ufeff', "DB content/BOM");
        Check(warning == null, "DB resource warning");
        var instance = BlockSnapshot.ReadDocument(new[] { Fixture("InstanceDB_InstCallGeneral_docs/InstanceDB_InstCallGeneral.s7dcl") }, out warning);
        Check(instance.Contains("InstCallGeneral") && warning == null, "instance DB native document");
        var logic = BlockSnapshot.ReadDocument(new[] { lad, res }, out warning);
        Check(logic.Contains("Contact( #Initial_Call )") && logic.Contains("MLC_37U") && logic.Contains("// MLC_37U [en-US]: First Cycle") && warning == null, "LAD native structure and resolved title");
        var fallback = ResourceTextReader.Annotate("S7_NetworkTitle := \"MLC_37U\";", res, "de-DE");
        Check(!fallback.Complete && fallback.Warning.Contains("fallback") && fallback.Content.Contains("[en-US]: First Cycle"), "explicit locale fallback");
        Check(!ResourceTextReader.Annotate("MLC_missing", res, "en-US").Complete, "missing keys visible");
        Check(BlockReadOptions.ParseLocale(new[] { "PLC", "Block" }) == "en-US" && BlockReadOptions.ParseLocale(new[] { "PLC", "Block", "locale=de-DE" }) == "de-DE", "locale parser");
        bool rejectedLocale = false;
        try { BlockReadOptions.ParseLocale(new[] { "PLC", "Block", "view=raw" }); } catch (ArgumentException) { rejectedLocale = true; }
        Check(rejectedLocale, "unknown option rejected");
        var root = Path.Combine(Path.GetTempPath(), "tia-phase2-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "native"));
            var bad = Path.Combine(root, "native", "bad.s7res");
            File.WriteAllText(bad, "MultiLingualTexts:\n  - id: MLC_37U\n    en-US: one\n    en-US: two\n");
            Check(!ResourceTextReader.Annotate("MLC_37U", bad, "en-US").Complete, "duplicate culture fails closed");
            File.WriteAllText(bad, "MultiLingualTexts:\n  - id: MLC_37U\n    en-US: |\n      multiline\n");
            Check(!ResourceTextReader.Annotate("MLC_37U", bad, "en-US").Complete, "unsupported multiline fails closed");
            var file = Path.Combine(root, "native", "block.s7dcl");
            File.Copy(db, file);
            BlockSnapshot.EnsureCapacity(root);
            Check(true, "under snapshot quota");
            var map = Path.Combine(root, "view", "source-map.json");
            Directory.CreateDirectory(Path.GetDirectoryName(map));
            File.WriteAllText(map, "[]");
            var manifest = BlockSnapshot.Save(root, new SnapshotManifest { artifactId = "test", fidelity = "native-text" }, new[] { file }, body, new[] { map });
            Check(manifest.analysisOnly && manifest.files.Count == 3 && manifest.files.All(f => f.sha256.Length == 64), "hash manifest including source map");
            Check(File.ReadAllText(Path.Combine(root, "manifest.json")).Contains("analysisOnly"), "serialized manifest");
            bool rejected = false;
            try { BlockSnapshot.Save(root, new SnapshotManifest(), new[] { db }, body); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "escape rejected");
            for (int i = 0; i < 100; i++) Directory.CreateDirectory(Path.Combine(root, "test-" + i));
            bool atLimit = false;
            try { BlockSnapshot.EnsureCapacity(root); } catch (IOException) { atLimit = true; }
            Check(atLimit, "quota rejects instead of deleting existing snapshots");
            var exportDir = new DirectoryInfo(Path.Combine(root, "export"));
            int xmlCalls = 0;
            Action<FileInfo> writeXml = f => { xmlCalls++; File.WriteAllText(f.FullName, "<xml />"); };
            Func<DirectoryInfo, string, DocumentExportAttempt> writeDocs = (d, name) =>
            {
                var doc = Path.Combine(d.FullName, name + ".s7dcl");
                File.WriteAllText(doc, "DATA_BLOCK Db END_DATA_BLOCK");
                return new DocumentExportAttempt { State = "Success", Files = new System.Collections.Generic.List<string> { doc } };
            };
            var read = BlockExportEngine.Export(false, false, exportDir, "block", writeDocs, writeXml);
            Check(read.ActualMode == "Documents" && read.Files.Count == 1 && xmlCalls == 0, "reader validates successful document without XML retry");
            bool failClosed = false;
            try { BlockExportEngine.Export(false, false, exportDir, "broken", (d, n) => throw new IOException("disk failure"), writeXml); }
            catch (InvalidOperationException) { failClosed = true; }
            Check(failClosed && xmlCalls == 0, "reader does not mask document IO errors");
            failClosed = false;
            try { BlockExportEngine.Export(false, false, exportDir, "failed", (d, n) => new DocumentExportAttempt { State = "Failed" }, writeXml); }
            catch (InvalidOperationException) { failClosed = true; }
            Check(failClosed && xmlCalls == 0, "failed state cannot be treated as complete");
            failClosed = false;
            try { BlockExportEngine.Export(false, false, exportDir, "outside", (d, n) => new DocumentExportAttempt { State = "Success", Files = new System.Collections.Generic.List<string> { db } }, writeXml); }
            catch (InvalidOperationException) { failClosed = true; }
            Check(failClosed && xmlCalls == 0, "out-of-snapshot document rejected");
            var legacy = BlockExportEngine.Export(false, true, exportDir, "legacy", (d, n) => throw new InvalidOperationException("document unavailable"), writeXml);
            Check(legacy.UsedFallback && legacy.FallbackReason == "document unavailable" && legacy.State == "XmlExportedAfterFallback" && xmlCalls == 1, "legacy fallback maintained");
            failClosed = false;
            try { BlockExportEngine.Export(false, true, exportDir, "twice", (d, n) => throw new InvalidOperationException("document unavailable"), f => throw new IOException("XML disk failure")); }
            catch (InvalidOperationException ex) { failClosed = ex.Message.Contains("document unavailable") && ex.Message.Contains("XML disk failure"); }
            Check(failClosed, "both legacy failures retained");
            var scl = BlockExportEngine.Export(true, false, exportDir, "scl", (d, n) => throw new Exception("docs must not run"), writeXml);
            Check(scl.ActualMode == "Xml" && scl.Files[0].Contains("native") && xmlCalls == 2, "SCL preferred XML under snapshot native directory");
            var snapshots = Path.Combine(root, "snapshots");
            var id = Guid.NewGuid().ToString("N");
            var captured = Path.Combine(snapshots, id);
            Directory.CreateDirectory(Path.Combine(captured, "native"));
            var native = Path.Combine(captured, "native", "block.xml");
            File.WriteAllText(native, "<block />");
            var lines = new StringBuilder("INTERFACE Input\n");
            for (int i = 0; i < 100; i++) lines.Append("  member").Append(i).Append("_long_identifier_for_network_boundary : Bool;\n");
            lines.Append("// CompileUnit ID=7\n");
            for (int i = 0; i < 100; i++) lines.Append("  Contact( #a );\n");
            var boundaries = Path.Combine(captured, "view", "boundaries.json");
            Directory.CreateDirectory(Path.GetDirectoryName(boundaries));
            File.WriteAllText(boundaries, "[1,102]");
            BlockSnapshot.Save(captured, new SnapshotManifest { artifactId = id, project = "P", blockRef = "B", fidelity = "reconstructed", warning = "Analysis-only XML reconstruction.", reason = "ADAPTER_SUBSET" }, new[] { native }, lines.ToString(), new[] { boundaries });
            var first = BlockSnapshotPager.ReadAt(snapshots, id);
            Check(first.hasMore && first.nextCursor != null && !first.incompleteUnit && first.completeForSelection && Encoding.UTF8.GetByteCount(first.content) <= 8192 && first.content.Split('\n').Length <= 151, "bounded first semantic page");
            var second = BlockSnapshotPager.ReadAt(snapshots, id, first.nextCursor);
            Check(second.startLine > 1 && second.content.Contains("// CompileUnit ID=7") && !second.hasMore && second.completeForSelection, "continuation resumes at safe compile-unit boundary");
            Check(first.warning.Contains("Analysis-only") && second.warning.Contains("Analysis-only") && second.reason == "ADAPTER_SUBSET", "fidelity warnings and reasons persist on each page");
            Check(first.content + second.content == lines.ToString(), "pages cover exact view without omission or duplication");
            File.WriteAllText(boundaries, "[1,999]");
            bool badBoundaries = false;
            try { BlockSnapshotPager.ReadAt(snapshots, id, first.nextCursor); } catch (InvalidOperationException) { badBoundaries = true; }
            Check(badBoundaries, "edited semantic boundary index invalidates continuation");
            File.WriteAllText(boundaries, "[1,102]");
            var nativeId = Guid.NewGuid().ToString("N");
            var nativeDir = Path.Combine(snapshots, nativeId);
            Directory.CreateDirectory(nativeDir);
            BlockSnapshot.Save(nativeDir, new SnapshotManifest { artifactId = nativeId, fidelity = "native-text" }, new string[0], lines.ToString());
            var nativePage = BlockSnapshotPager.ReadAt(snapshots, nativeId);
            Check(nativePage.hasMore && nativePage.incompleteUnit && !nativePage.completeForSelection, "native text split conservatively marked incomplete");
            var spoofId = Guid.NewGuid().ToString("N");
            var spoofDir = Path.Combine(snapshots, spoofId);
            Directory.CreateDirectory(spoofDir);
            BlockSnapshot.Save(spoofDir, new SnapshotManifest { artifactId = spoofId, fidelity = "reconstructed" }, new string[0], lines.ToString());
            Check(BlockSnapshotPager.ReadAt(snapshots, spoofId).incompleteUnit, "source-looking header alone cannot certify a semantic boundary");
            var smallId = Guid.NewGuid().ToString("N");
            var smallDir = Path.Combine(snapshots, smallId);
            Directory.CreateDirectory(smallDir);
            BlockSnapshot.Save(smallDir, new SnapshotManifest { artifactId = smallId, fidelity = "native-text" }, new string[0], body);
            var small = BlockSnapshotPager.ReadAt(snapshots, smallId);
            Check(small.content == body && small.completeForSelection && !small.hasMore && small.nextCursor == null, "small block fully available in first read");
            Check(Encoding.UTF8.GetByteCount(InventoryJson.Serialize(small)) < 16384, "first page net48-compatible JSON stays within response budget");
            var protectedId = Guid.NewGuid().ToString("N");
            var protectedDir = Path.Combine(snapshots, protectedId);
            Directory.CreateDirectory(protectedDir);
            BlockSnapshot.Save(protectedDir, new SnapshotManifest { artifactId = protectedId, fidelity = "metadata-only" }, new string[0], "");
            var protectedPage = BlockSnapshotPager.ReadAt(snapshots, protectedId);
            Check(!protectedPage.completeForSelection && !protectedPage.hasMore && protectedPage.content == "", "metadata-only never masquerades as code");
            bool invalidCursor = false;
            var edited = first.nextCursor.Substring(0, first.nextCursor.Length - 1) + (first.nextCursor.EndsWith("A") ? "B" : "A");
            try { BlockSnapshotPager.ReadAt(snapshots, id, edited); } catch (ArgumentException) { invalidCursor = true; }
            Check(invalidCursor, "edited signed cursor rejected");
            bool invalidId = false;
            try { BlockSnapshotPager.ReadAt(snapshots, "../../other", first.nextCursor); } catch (ArgumentException) { invalidId = true; }
            Check(invalidId, "artifact id cannot be a filesystem path");
            File.AppendAllText(native, "edit");
            bool changed = false;
            try { BlockSnapshotPager.ReadAt(snapshots, id, first.nextCursor); } catch (InvalidOperationException) { changed = true; }
            Check(changed, "native edit invalidates existing snapshot");
            File.WriteAllText(native, "<block />");
            var manifestFile = Path.Combine(captured, "manifest.json");
            File.AppendAllText(manifestFile, " ");
            changed = false;
            try { BlockSnapshotPager.ReadAt(snapshots, id, first.nextCursor); } catch (InvalidOperationException) { changed = true; }
            Check(changed, "manifest change invalidates cursor");
            var badManifest = InventoryJson.Deserialize<SnapshotManifest>(File.ReadAllText(manifestFile));
            badManifest.files.Single(entry => entry.path.StartsWith("native/", StringComparison.Ordinal)).path = "native/../file.xml";
            File.WriteAllText(manifestFile, InventoryJson.Serialize(badManifest));
            bool escapedPath = false;
            try { BlockSnapshotPager.ReadAt(snapshots, id); } catch (InvalidOperationException) { escapedPath = true; }
            Check(escapedPath, "manifest cannot redirect snapshot reads outside native path");
            var hugeId = Guid.NewGuid().ToString("N");
            var huge = Path.Combine(snapshots, hugeId);
            Directory.CreateDirectory(huge);
            BlockSnapshot.Save(huge, new SnapshotManifest { artifactId = hugeId, fidelity = "reconstructed" }, new string[0], "INTERFACE Input\n" + new string('é', 12000) + "\nEND_NETWORK\n");
            var slice = BlockSnapshotPager.ReadAt(snapshots, hugeId);
            Check(slice.incompleteUnit && !slice.completeForSelection && slice.hasMore && slice.nextCursor != null, "oversized unit is explicitly incomplete");
            var following = BlockSnapshotPager.ReadAt(snapshots, hugeId, slice.nextCursor);
            Check(following.startLine > 1 || following.incompleteUnit, "oversized unit has progress across pages");
            var all = new StringBuilder(slice.content);
            var part = following;
            var safety = 0;
            while (true)
            {
                all.Append(part.content);
                if (!part.hasMore) break;
                if (++safety > 10) throw new Exception("paging failed to advance");
                part = BlockSnapshotPager.ReadAt(snapshots, hugeId, part.nextCursor);
            }
            Check(all.ToString() == "INTERFACE Input\n" + new string('é', 12000) + "\nEND_NETWORK\n", "oversized Unicode unit is covered exactly by slices");
            var tighter = BlockSnapshotPager.ReadAt(snapshots, hugeId, null, 512);
            Check(Encoding.UTF8.GetByteCount(tighter.content) <= 512 && tighter.nextCursor != null, "page budget can shrink without changing snapshot");
            var escapedId = Guid.NewGuid().ToString("N");
            var escapedDir = Path.Combine(snapshots, escapedId);
            Directory.CreateDirectory(escapedDir);
            BlockSnapshot.Save(escapedDir, new SnapshotManifest { artifactId = escapedId, fidelity = "reconstructed" }, new string[0], "INTERFACE Input\n" + new string('\u0001', 6000));
            var escapedPage = BlockSnapshotPager.ReadAt(snapshots, escapedId);
            Check(Encoding.UTF8.GetByteCount(InventoryJson.Serialize(escapedPage)) > 16384, "JSON escaping can exceed raw byte budget");
            escapedPage = BlockSnapshotPager.ReadAt(snapshots, escapedId, null, 1024);
            Check(Encoding.UTF8.GetByteCount(InventoryJson.Serialize(escapedPage)) < 16384 && escapedPage.hasMore, "bounded retry preserves continuation for heavily escaped content");
            bool wrongSnapshot = false;
            try { BlockSnapshotPager.ReadAt(snapshots, hugeId, first.nextCursor); } catch (InvalidOperationException) { wrongSnapshot = true; }
            Check(wrongSnapshot, "cursor bound to snapshot identity");
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"Block snapshot production checks: {count} passed");
    }
}
