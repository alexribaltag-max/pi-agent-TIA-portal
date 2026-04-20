using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class ImportPlcBlockSmartJsonCommand : ITiaCommand
    {
        public string Name => "IMPORTPLCBLOCKSMARTJSON";
        public string Description => "Imports one PLC block from XML or document export sources using the smart strategy and returns JSON with detected format, target group, imported block metadata, and compile/update follow-up warnings.";
        public string Usage => "IMPORTPLCBLOCKSMARTJSON|<device-reference>|<source-path>|[target-group-reference]";
        public string Example => @"IMPORTPLCBLOCKSMARTJSON|DemoProject/PLC_1|C:\Exports\Main.xml|01_CentralFunctions";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 2 || providedArgs.Length > 3)
            {
                throw new ArgumentException($"Expected two or three arguments (<device-reference>, <source-path>, optional [target-group-reference]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var sourcePath = providedArgs[1].Trim();
            var targetGroup = providedArgs.Length == 3
                ? CommandSupport.ResolvePlcBlockGroup(plcSoftware, providedArgs[2])
                : CommandSupport.ResolvePlcBlockGroup(plcSoftware, "<root>");

            if (TryResolveDocumentImportSource(sourcePath, out FileInfo s7dclFile))
            {
                var importResult = targetGroup.Blocks.ImportFromDocuments(
                    s7dclFile.Directory,
                    Path.GetFileNameWithoutExtension(s7dclFile.Name),
                    ImportDocumentOptions.Override);

                return BuildJson(
                    resolution.Project.Name,
                    resolvedReference,
                    resolution.Device.Name,
                    sourcePath,
                    s7dclFile.FullName,
                    "Documents",
                    targetGroup,
                    importResult.ImportedPlcBlocks.OfType<PlcBlock>().ToList());
            }

            if (TryResolveXmlImportSource(sourcePath, out FileInfo xmlFile))
            {
                var importedObjects = targetGroup.Blocks.Import(
                    xmlFile,
                    ImportOptions.Override,
                    SWImportOptions.None);

                return BuildJson(
                    resolution.Project.Name,
                    resolvedReference,
                    resolution.Device.Name,
                    sourcePath,
                    xmlFile.FullName,
                    "Xml",
                    targetGroup,
                    importedObjects.OfType<PlcBlock>().ToList());
            }

            throw new InvalidOperationException("Unsupported PLC block import source. Provide either an .xml file, an .s7dcl file, or a directory containing exactly one .s7dcl file.");
        }

        private static string BuildJson(
            string projectName,
            string deviceReference,
            string deviceName,
            string requestedSourcePath,
            string resolvedSourcePath,
            string sourceFormat,
            PlcBlockGroupResolution targetGroup,
            List<PlcBlock> importedBlocks)
        {
            var json = new StringBuilder();
            json.Append("{\"project\":\"")
                .Append(CommandSupport.EscapeJson(projectName))
                .Append("\",\"device\":{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(deviceReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(deviceName))
                .Append("\"},\"import\":{\"requestedSourcePath\":\"")
                .Append(CommandSupport.EscapeJson(requestedSourcePath))
                .Append("\",\"resolvedSourcePath\":\"")
                .Append(CommandSupport.EscapeJson(resolvedSourcePath))
                .Append("\",\"sourceFormat\":\"")
                .Append(CommandSupport.EscapeJson(sourceFormat))
                .Append("\",\"targetGroup\":\"")
                .Append(CommandSupport.EscapeJson(targetGroup.GroupReference))
                .Append("\",\"overrideExisting\":true,\"importedBlockCount\":")
                .Append(importedBlocks.Count)
                .Append(",\"importedBlocks\":[");

            for (int i = 0; i < importedBlocks.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                AppendImportedBlockJson(json, importedBlocks[i], targetGroup.GroupReference);
            }

            json.Append("],\"warnings\":[\"Imported blocks may still require COMPILEPLCBLOCK, COMPILEPLC, or UPDATEPROGRAM afterward.\"]}}");
            return json.ToString();
        }

        private static void AppendImportedBlockJson(StringBuilder json, PlcBlock block, string targetGroupReference)
        {
            var blockReference = string.IsNullOrWhiteSpace(targetGroupReference) || string.Equals(targetGroupReference, "<root>", StringComparison.OrdinalIgnoreCase)
                ? block.Name
                : targetGroupReference + "/" + block.Name;

            json.Append("{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(blockReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(block.Name))
                .Append("\",\"type\":\"")
                .Append(CommandSupport.EscapeJson(CommandSupport.GetPlcBlockTypeName(block)))
                .Append("\",\"number\":")
                .Append(block.Number)
                .Append(",\"programmingLanguage\":\"")
                .Append(CommandSupport.EscapeJson(block.ProgrammingLanguage.ToString()))
                .Append("\",\"isConsistent\":")
                .Append(block.IsConsistent ? "true" : "false")
                .Append(",\"compileDate\":\"")
                .Append(CommandSupport.EscapeJson(block.CompileDate.ToString("O")))
                .Append("\"");

            if (block is InstanceDB instanceDb)
            {
                json.Append(",\"instanceOf\":\"")
                    .Append(CommandSupport.EscapeJson(instanceDb.InstanceOfName))
                    .Append("\"");
            }

            json.Append('}');
        }

        private static bool TryResolveDocumentImportSource(string sourcePath, out FileInfo s7dclFile)
        {
            s7dclFile = null;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            if (Directory.Exists(sourcePath))
            {
                var directory = new DirectoryInfo(sourcePath);
                var matchingFiles = directory.GetFiles("*.s7dcl");
                if (matchingFiles.Length == 1)
                {
                    s7dclFile = matchingFiles[0];
                    return true;
                }

                if (matchingFiles.Length > 1)
                {
                    throw new InvalidOperationException($"Directory '{directory.FullName}' contains multiple .s7dcl files. Specify the exact .s7dcl file to import.");
                }

                return false;
            }

            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var file = new FileInfo(sourcePath);
            if (string.Equals(file.Extension, ".s7dcl", StringComparison.OrdinalIgnoreCase))
            {
                s7dclFile = file;
                return true;
            }

            return false;
        }

        private static bool TryResolveXmlImportSource(string sourcePath, out FileInfo xmlFile)
        {
            xmlFile = null;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return false;
            }

            var file = new FileInfo(sourcePath);
            if (string.Equals(file.Extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlFile = file;
                return true;
            }

            return false;
        }
    }
}
