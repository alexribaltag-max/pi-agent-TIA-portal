using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class ExportPlcBlockSmartCommand : ITiaCommand
    {
        public string Name => "EXPORTPLCBLOCKSMART";
        public string Description => "Exports one PLC block using the most suitable strategy based on block type and language: documents for LAD/FBD/DB blocks, XML for SCL blocks, with XML fallback if document export fails.";
        public string Usage => "EXPORTPLCBLOCKSMART|<device-reference>|<block-reference>|<target-directory>";
        public string Example => @"EXPORTPLCBLOCKSMART|DemoProject/PLC_1|Main/FB_Machine|C:\Exports";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.RequireExactArguments(args, this, "<device-reference>", "<block-reference>", "<target-directory>");
            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var block = blockResolution.Block;
            var targetDirectory = new DirectoryInfo(providedArgs[2]);
            if (!targetDirectory.Exists)
            {
                targetDirectory.Create();
            }

            var baseName = CommandSupport.SanitizeFileName(block.Name);
            var preferredMode = GetPreferredExportMode(block);

            if (preferredMode == SmartExportMode.Xml)
            {
                var xmlFile = new FileInfo(Path.Combine(targetDirectory.FullName, baseName + ".xml"));
                block.Export(xmlFile, ExportOptions.None, DocumentInfoOptions.None);
                return $"Smart export used XML for PLC block '{blockResolution.BlockReference}' from '{resolvedReference}' to '{xmlFile.FullName}' [Type={CommandSupport.GetPlcBlockTypeName(block)}, Language={block.ProgrammingLanguage}].";
            }

            var docsDirectory = new DirectoryInfo(Path.Combine(targetDirectory.FullName, baseName + "_docs"));
            if (!docsDirectory.Exists)
            {
                docsDirectory.Create();
            }

            try
            {
                var exportResult = block.ExportAsDocuments(docsDirectory, baseName);
                var exportedFiles = exportResult.ExportedDocuments
                    .Select(file => file.FullName)
                    .ToList();
                var messages = exportResult.Messages
                    .Select(message => message.Message)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToList();

                var result = $"Smart export used documents for PLC block '{blockResolution.BlockReference}' from '{resolvedReference}' to '{docsDirectory.FullName}' with state '{exportResult.State}' [Type={CommandSupport.GetPlcBlockTypeName(block)}, Language={block.ProgrammingLanguage}].";
                if (exportedFiles.Any())
                {
                    result += $" Files: {string.Join(", ", exportedFiles)}.";
                }

                if (messages.Any())
                {
                    result += $" Messages: {string.Join(" | ", messages)}.";
                }

                return result;
            }
            catch (Exception ex)
            {
                var xmlFile = new FileInfo(Path.Combine(targetDirectory.FullName, baseName + ".xml"));
                block.Export(xmlFile, ExportOptions.None, DocumentInfoOptions.None);
                return $"Smart export fell back to XML for PLC block '{blockResolution.BlockReference}' from '{resolvedReference}' because document export failed: {ex.Message} XML file: '{xmlFile.FullName}' [Type={CommandSupport.GetPlcBlockTypeName(block)}, Language={block.ProgrammingLanguage}].";
            }
        }

        private static SmartExportMode GetPreferredExportMode(PlcBlock block)
        {
            if (block.ProgrammingLanguage == ProgrammingLanguage.SCL)
            {
                return SmartExportMode.Xml;
            }

            if (block is DataBlock || block is InstanceDB)
            {
                return SmartExportMode.Documents;
            }

            return SmartExportMode.Documents;
        }

        private enum SmartExportMode
        {
            Xml,
            Documents
        }
    }
}
