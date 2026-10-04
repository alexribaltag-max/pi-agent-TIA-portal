using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using TiaLocalBridge.Services;

namespace TiaLocalBridge.Commands
{
    [DataContract]
    internal sealed class GuardedImportResult
    {
        [DataMember] public bool dryRun { get; set; }
        [DataMember] public string targetDevice { get; set; }
        [DataMember] public string targetGroup { get; set; }
        [DataMember] public string sourcePath { get; set; }
        [DataMember] public bool targetExists { get; set; }
        [DataMember] public string existingHash { get; set; }
        [DataMember] public string backupArtifactId { get; set; }
        [DataMember] public bool overwriteApproved { get; set; }
        [DataMember] public bool hashMatched { get; set; }
        [DataMember] public bool importExecuted { get; set; }
        [DataMember] public List<string> importedBlocks { get; set; }
        [DataMember] public bool compileRequested { get; set; }
        [DataMember] public string compileStatus { get; set; }
        [DataMember] public string compileErrors { get; set; }
        [DataMember] public string reReadArtifactId { get; set; }
        [DataMember] public string warning { get; set; }
        [DataMember] public string error { get; set; }
    }

    internal class ImportPlcBlockGuardedCommand : ITiaCommand
    {
        public string Name => "IMPORTPLCBLOCKGUARDED";
        public string Description => "Safely import a PLC block with dry-run capabilities, overwrite approval, current-hash verification, and automatic backup.";
        public string Usage => "IMPORTPLCBLOCKGUARDED|<device-reference>|<target-group-reference>|<source-path>|[key=value options...]";
        public string Example => "IMPORTPLCBLOCKGUARDED|DemoProject/PLC_1|01_CentralFunctions|C:\\Exports\\Main.xml|dryRun=false|overwrite=true|compile=true|expectedHash=abc123def";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            if (args.Length < 3) throw new ArgumentException("GETPLCBLOCKGUARDED requires <device>, <group>, and <source-path>.");

            var deviceRef = args[0].Trim();
            var targetGroupRef = args[1].Trim();
            var sourcePath = args[2].Trim();
            
            bool dryRun = true;
            bool overwrite = false;
            bool compile = false;
            string expectedHash = null;

            foreach (var opt in args.Skip(3))
            {
                var kv = opt.Split(new[] { '=' }, 2);
                if (kv.Length == 2)
                {
                    var k = kv[0].Trim();
                    var v = kv[1].Trim();
                    if (k == "dryRun") dryRun = v.ToLowerInvariant() == "true";
                    else if (k == "overwrite") overwrite = v.ToLowerInvariant() == "true";
                    else if (k == "compile") compile = v.ToLowerInvariant() == "true";
                    else if (k == "expectedHash") expectedHash = v;
                    else throw new ArgumentException($"Unknown option: {k}");
                }
            }

            var result = new GuardedImportResult
            {
                dryRun = dryRun,
                targetDevice = deviceRef,
                targetGroup = targetGroupRef,
                sourcePath = sourcePath,
                overwriteApproved = overwrite,
                compileRequested = compile,
                importedBlocks = new List<string>()
            };

            try
            {
                var resolution = CommandSupport.ResolveDeviceByReference(portal, deviceRef);
                var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
                if (plcSoftware == null) throw new InvalidOperationException($"Device '{deviceRef}' does not contain PLC software.");
                
                var targetGroup = CommandSupport.ResolvePlcBlockGroup(plcSoftware, targetGroupRef);

                FileInfo s7dclFile = null;
                FileInfo xmlFile = null;
                if (TryResolveDocumentImportSource(sourcePath, out s7dclFile)) { }
                else if (TryResolveXmlImportSource(sourcePath, out xmlFile)) { }
                else throw new InvalidOperationException("Unsupported import source. Provide either an .xml file, an .s7dcl file, or a directory containing exactly one .s7dcl file.");

                // Check if target block exists to compute existing hash
                string blockName = s7dclFile != null ? Path.GetFileNameWithoutExtension(s7dclFile.Name) : Path.GetFileNameWithoutExtension(xmlFile.Name);
                
                // TIA might name the exported XML the same as the block. For S7DCL it is the block name.
                // We must search the target group for a block with this name.
                PlcBlock existingBlock = null;
                foreach (PlcBlock b in targetGroup.Blocks)
                {
                    if (b.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                    {
                        existingBlock = b;
                        break;
                    }
                }

                if (existingBlock != null)
                {
                    result.targetExists = true;
                    // Export to calculate current hash and create backup
                    string backupId;
                    var backupDir = BlockSnapshot.CreateDirectory(out backupId);
                    var exportResult = BlockExportService.ExportForRead(existingBlock, new DirectoryInfo(backupDir));
                    
                    if (exportResult != null && exportResult.Files.Count > 0)
                    {
                        // Calculate hash of native files
                        var hashes = new List<string>();
                        foreach (var f in exportResult.Files)
                        {
                            var bytes = File.ReadAllBytes(f);
                            using (var hash = SHA256.Create())
                            {
                                hashes.Add(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
                            }
                        }
                        result.existingHash = string.Join(",", hashes);
                    }
                    else
                    {
                        result.existingHash = "UNABLE_TO_EXPORT";
                    }

                    result.backupArtifactId = backupId;
                    
                    if (!string.IsNullOrEmpty(expectedHash))
                    {
                        result.hashMatched = (result.existingHash == expectedHash);
                        if (!result.hashMatched)
                        {
                            throw new InvalidOperationException($"Hash mismatch. Expected: {expectedHash}, Actual: {result.existingHash}. Overwrite refused.");
                        }
                    }
                    else
                    {
                        if (overwrite && !dryRun) throw new InvalidOperationException("expectedHash must be provided to overwrite an existing block.");
                    }

                    if (!overwrite && !dryRun)
                    {
                        throw new InvalidOperationException("Target block exists. overwrite=true is required to modify it.");
                    }
                }
                else
                {
                    result.targetExists = false;
                }

                if (dryRun)
                {
                    result.warning = "Dry run completed successfully. No import was performed.";
                    return InventoryJson.Serialize(result);
                }

                // PERFORM IMPORT
                IList<IEngineeringObject> importedObjects;
                if (s7dclFile != null)
                {
                    var importRes = targetGroup.Blocks.ImportFromDocuments(s7dclFile.Directory, Path.GetFileNameWithoutExtension(s7dclFile.Name), ImportDocumentOptions.Override);
                    importedObjects = importRes.ImportedPlcBlocks.Cast<IEngineeringObject>().ToList();
                }
                else
                {
                    importedObjects = targetGroup.Blocks.Import(xmlFile, Siemens.Engineering.ImportOptions.Override, SWImportOptions.None).Cast<IEngineeringObject>().ToList();
                }

                result.importExecuted = true;
                PlcBlock lastImported = null;
                foreach (var obj in importedObjects.OfType<PlcBlock>())
                {
                    result.importedBlocks.Add($"{obj.Name} [Type={CommandSupport.GetPlcBlockTypeName(obj)}, Number={obj.Number}, Language={obj.ProgrammingLanguage}]");
                    lastImported = obj;
                }

                result.warning = "Imported block may still require compile/update actions in TIA Portal.";

                if (compile && lastImported != null)
                {
                    var compileResult = CommandSupport.CompilePlcBlock(lastImported, $"PLC block '{lastImported.Name}'");
                    result.compileStatus = CommandSupport.FormatCompilerResult(compileResult);
                    if (compileResult.ErrorCount > 0)
                    {
                        result.warning += " Compilation failed. See compileStatus.";
                    }
                }

                // Re-read block if imported
                if (lastImported != null)
                {
                    string reReadId;
                    var reReadDir = BlockSnapshot.CreateDirectory(out reReadId);
                    BlockExportService.ExportForRead(lastImported, new DirectoryInfo(reReadDir));
                    result.reReadArtifactId = reReadId;
                }

                return InventoryJson.Serialize(result);
            }
            catch (Exception ex)
            {
                result.error = ex.Message;
                return InventoryJson.Serialize(result);
            }
        }

        private static bool TryResolveDocumentImportSource(string sourcePath, out FileInfo s7dclFile)
        {
            s7dclFile = null;
            if (string.IsNullOrWhiteSpace(sourcePath)) return false;
            if (Directory.Exists(sourcePath))
            {
                var directory = new DirectoryInfo(sourcePath);
                var matchingFiles = directory.GetFiles("*.s7dcl");
                if (matchingFiles.Length == 1) { s7dclFile = matchingFiles[0]; return true; }
                if (matchingFiles.Length > 1) throw new InvalidOperationException($"Directory '{directory.FullName}' contains multiple .s7dcl files.");
                return false;
            }
            if (!File.Exists(sourcePath)) return false;
            var file = new FileInfo(sourcePath);
            if (string.Equals(file.Extension, ".s7dcl", StringComparison.OrdinalIgnoreCase)) { s7dclFile = file; return true; }
            return false;
        }

        private static bool TryResolveXmlImportSource(string sourcePath, out FileInfo xmlFile)
        {
            xmlFile = null;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return false;
            var file = new FileInfo(sourcePath);
            if (string.Equals(file.Extension, ".xml", StringComparison.OrdinalIgnoreCase)) { xmlFile = file; return true; }
            return false;
        }
    }
}