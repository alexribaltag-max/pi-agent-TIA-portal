using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Siemens.Engineering;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    [DataContract]
    internal sealed class PlcBlockReadResult
    {
        [DataMember] public int schemaVersion { get; set; }
        [DataMember] public string project { get; set; }
        [DataMember] public string deviceRef { get; set; }
        [DataMember] public string blockRef { get; set; }
        [DataMember] public string blockType { get; set; }
        [DataMember] public string language { get; set; }
        [DataMember] public string locale { get; set; }
        [DataMember] public string representation { get; set; }
        [DataMember] public string fidelity { get; set; }
        [DataMember] public bool completeForSelection { get; set; }
        [DataMember] public bool incompleteUnit { get; set; }
        [DataMember] public bool hasMore { get; set; }
        [DataMember] public string nextCursor { get; set; }
        [DataMember] public int startLine { get; set; }
        [DataMember] public int endLine { get; set; }
        [DataMember] public string reason { get; set; }
        [DataMember] public string warning { get; set; }
        [DataMember] public string content { get; set; }
        [DataMember] public string artifactId { get; set; }
        [DataMember] public string artifactDirectory { get; set; }
        [DataMember] public string nativeHash { get; set; }
        [DataMember] public string sourceMap { get; set; }
        [DataMember] public string capturedAt { get; set; }
        [DataMember] public bool analysisOnly { get; set; }
    }

    internal sealed class GetPlcBlockCommand : ITiaCommand
    {
        public string Name => "GETPLCBLOCK";
        public string Description => "Read one block into a bounded native-document or supported SCL XML analysis view; unsupported content is metadata-only. Creates a local analysis-only snapshot, never imports.";
        public string Usage => "GETPLCBLOCK|<device-reference>|<block-reference>|[locale=<culture>]";
        public string Example => "GETPLCBLOCK|PackagingMachine/S7-1500/ET200MP station_1|02_Global/Data";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var locale = BlockReadOptions.ParseLocale(args);
            var device = CommandSupport.ResolveDeviceByReference(portal, args[0]);
            var software = CommandSupport.TryGetPlcSoftware(device.Device);
            if (software == null) throw new InvalidOperationException("Device does not contain PLC software.");
            var resolved = CommandSupport.ResolvePlcBlock(software, args[1]);
            var block = resolved.Block;
            string id;
            var directory = BlockSnapshot.CreateDirectory(out id);
            var result = new PlcBlockReadResult { schemaVersion = 1, project = device.Project.Name,
                deviceRef = CommandSupport.GetDeviceReference(device.Project, device.Device), blockRef = resolved.BlockReference,
                blockType = CommandSupport.GetPlcBlockTypeName(block), language = block.ProgrammingLanguage.ToString(), locale = locale,
                artifactId = id, artifactDirectory = directory, analysisOnly = true };
            try
            {
                BlockExportResult export = null;
                string mapFile = null;
                string boundaryFile = null;
                string full = "";
                string warning = null;
                if (block.IsKnowHowProtected)
                {
                    result.representation = "none";
                    result.fidelity = "metadata-only";
                    result.reason = "PROTECTED";
                    warning = "TIA reports know-how protection; no content export was attempted.";
                }
                else
                {
                    export = BlockExportService.ExportForRead(block, new DirectoryInfo(directory));
                }
                if (export != null && export.ActualMode == "Documents")
                {
                    bool resourceComplete;
                    full = BlockSnapshot.ReadDocument(export.Files, locale, out warning, out resourceComplete);
                    result.representation = "native-document";
                    result.fidelity = resourceComplete ? "native-text" : "partial";
                    if (!resourceComplete) result.reason = "RESOURCES_UNRESOLVED";
                }
                else if (export != null)
                {
                    result.representation = "xml-artifact";
                    result.fidelity = "metadata-only";
                    result.reason = "FORMAT_UNSUPPORTED";
                    warning = "XML-only export retained; no supported analysis adapter for this block.";
                    if (string.Equals(result.language, "SCL", StringComparison.OrdinalIgnoreCase) && export.Files.Count == 1)
                    {
                        try
                        {
                            var parsed = StructuredTextReader.Read(export.Files[0], locale);
                            full = parsed.Text;
                            var mapDir = Path.Combine(directory, "view");
                            Directory.CreateDirectory(mapDir);
                            mapFile = Path.Combine(mapDir, "source-map.json");
                            File.WriteAllText(mapFile, InventoryJson.Serialize(parsed.SourceMap), new UTF8Encoding(false));
                            boundaryFile = Path.Combine(mapDir, "boundaries.json");
                            File.WriteAllText(boundaryFile, InventoryJson.Serialize(parsed.SafeBoundaryLines), new UTF8Encoding(false));
                            result.sourceMap = "view/source-map.json";
                            result.representation = "scl-xml-analysis";
                            result.fidelity = "reconstructed";
                            result.reason = null;
                            warning = "SCL reconstruction is analysis-only, not an importable source; only the reviewed XML schema and token subset are supported.";
                        }
                        catch (FormatException ex)
                        {
                            warning = "Unsupported SCL XML construct/schema: " + ex.Message + "; native XML retained. No executable code was claimed.";
                        }
                    }
                    else if ((string.Equals(result.language, "LAD", StringComparison.OrdinalIgnoreCase) || string.Equals(result.language, "FBD", StringComparison.OrdinalIgnoreCase)) && export.Files.Count == 1)
                    {
                        try
                        {
                            var parsed = FlgNetReader.Read(export.Files[0], locale);
                            full = parsed.Text;
                            result.representation = "flgnet-xml-analysis";
                            result.fidelity = parsed.IsPartial ? "partial" : "reconstructed";
                            result.reason = parsed.IsPartial ? "GRAPHICAL_UNSUPPORTED" : null;
                            warning = "LAD/FBD reconstruction is an analysis-only structural outline; some constructs may be omitted.";
                        }
                        catch (FormatException ex)
                        {
                            warning = "Unsupported LAD/FBD XML construct/schema: " + ex.Message + "; native XML retained. No graphical logic was claimed.";
                        }
                    }
                }
                if (export != null && export.UsedFallback) warning = (warning == null ? "" : warning + " ") + "Document export failed: " + export.FallbackReason;
                result.warning = warning != null && warning.Length > 1024 ? warning.Substring(0, 1024) : warning;
                var manifest = BlockSnapshot.Save(directory, new SnapshotManifest { artifactId = id, project = result.project,
                    deviceRef = result.deviceRef, blockRef = result.blockRef, blockType = result.blockType,
                    language = result.language, locale = result.locale, exportMode = export == null ? "none" : export.ActualMode,
                    exportState = export == null ? "Protected" : export.State,
                    fallbackReason = export == null ? null : export.FallbackReason, fidelity = result.fidelity,
                    reason = result.reason, warning = result.warning },
                    export == null ? Enumerable.Empty<string>() : export.Files.AsEnumerable(), full,
                    mapFile == null ? null : new[] { mapFile, boundaryFile });
                result.capturedAt = manifest.capturedAt;
                result.nativeHash = string.Join(",", manifest.files.Where(file => file.path.StartsWith("native/", StringComparison.OrdinalIgnoreCase)).Select(file => file.sha256));
                foreach (var budget in new[] { 8192, 4096, 2048, 1024, 512 })
                {
                    var page = BlockSnapshotPager.Read(id, null, budget);
                    result.content = page.content;
                    result.startLine = page.startLine;
                    result.endLine = page.endLine;
                    result.hasMore = page.hasMore;
                    result.nextCursor = page.nextCursor;
                    result.incompleteUnit = page.incompleteUnit;
                    result.completeForSelection = page.completeForSelection;
                    result.warning = page.warning;
                    if (result.warning != null && result.warning.Length > 1200) result.warning = result.warning.Substring(0, 1200);
                    var json = InventoryJson.Serialize(result);
                    if (Encoding.UTF8.GetByteCount(json) <= 16384) return json;
                }
                throw new InvalidOperationException("Block response metadata exceeds 16 KiB. Inspect the local snapshot instead.");
            }
            catch (Exception ex)
            {
                // Leave failed exports for diagnostics, but do not pretend this is a successful code read.
                throw new InvalidOperationException("GETPLCBLOCK failed (snapshot " + id + "): " + ex.Message, ex);
            }
        }
    }
}
