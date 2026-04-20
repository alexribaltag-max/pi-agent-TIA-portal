using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class ImportPlcBlockSmartCommand : ITiaCommand
    {
        public string Name => "IMPORTPLCBLOCKSMART";
        public string Description => "Imports one PLC block from either an XML export or a document export (.s7dcl with optional .s7res), automatically selecting the correct TIA import method and overriding existing blocks when needed.";
        public string Usage => "IMPORTPLCBLOCKSMART|<device-reference>|<source-path>|[target-group-reference]";
        public string Example => @"IMPORTPLCBLOCKSMART|DemoProject/PLC_1|C:\Exports\Main.xml|01_CentralFunctions";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

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
                return ImportDocuments(targetGroup, resolvedReference, s7dclFile);
            }

            if (TryResolveXmlImportSource(sourcePath, out FileInfo xmlFile))
            {
                return ImportXml(targetGroup, resolvedReference, xmlFile);
            }

            throw new InvalidOperationException("Unsupported PLC block import source. Provide either an .xml file, an .s7dcl file, or a directory containing exactly one .s7dcl file.");
        }

        private static string ImportDocuments(PlcBlockGroupResolution targetGroup, string resolvedReference, FileInfo s7dclFile)
        {
            var importResult = targetGroup.Blocks.ImportFromDocuments(
                s7dclFile.Directory,
                Path.GetFileNameWithoutExtension(s7dclFile.Name),
                ImportDocumentOptions.Override);

            var importedBlocks = importResult.ImportedPlcBlocks
                .Select(block => string.Format("{0} [Type={1}, Number={2}, Language={3}, IsConsistent={4}, CompileDate={5:O}]", block.Name, CommandSupport.GetPlcBlockTypeName(block), block.Number, block.ProgrammingLanguage, block.IsConsistent, block.CompileDate))
                .ToList();

            return importedBlocks.Any()
                ? $"Imported PLC block documents from '{s7dclFile.FullName}' into '{resolvedReference}' group '{targetGroup.GroupReference}'. Imported blocks: {string.Join(", ", importedBlocks)}. Imported blocks may still require COMPILEPLCBLOCK, COMPILEPLC, or UPDATEPROGRAM afterward."
                : $"Document import from '{s7dclFile.FullName}' into '{resolvedReference}' group '{targetGroup.GroupReference}' completed, but no imported blocks were reported.";
        }

        private static string ImportXml(PlcBlockGroupResolution targetGroup, string resolvedReference, FileInfo xmlFile)
        {
            var importedObjects = targetGroup.Blocks.Import(
                xmlFile,
                ImportOptions.Override,
                SWImportOptions.None);

            var importedBlocks = importedObjects
                .OfType<PlcBlock>()
                .Select(block => string.Format("{0} [Type={1}, Number={2}, Language={3}, IsConsistent={4}, CompileDate={5:O}]", block.Name, CommandSupport.GetPlcBlockTypeName(block), block.Number, block.ProgrammingLanguage, block.IsConsistent, block.CompileDate))
                .ToList();

            return importedBlocks.Any()
                ? $"Imported PLC block XML from '{xmlFile.FullName}' into '{resolvedReference}' group '{targetGroup.GroupReference}'. Imported blocks: {string.Join(", ", importedBlocks)}. Imported blocks may still require COMPILEPLCBLOCK, COMPILEPLC, or UPDATEPROGRAM afterward."
                : $"XML import from '{xmlFile.FullName}' into '{resolvedReference}' group '{targetGroup.GroupReference}' completed, but no imported blocks were reported.";
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
