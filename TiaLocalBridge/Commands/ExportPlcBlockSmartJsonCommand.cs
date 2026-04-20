using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Commands
{
    internal class ExportPlcBlockSmartJsonCommand : ITiaCommand
    {
        public string Name => "EXPORTPLCBLOCKSMARTJSON";
        public string Description => "Exports one PLC block using the smart strategy and returns JSON describing the chosen export mode, generated files, block metadata, and any fallback from document export to XML.";
        public string Usage => "EXPORTPLCBLOCKSMARTJSON|<device-reference>|<block-reference>|<target-directory>";
        public string Example => @"EXPORTPLCBLOCKSMARTJSON|DemoProject/PLC_1|Main/FB_Machine|C:\Exports";
        public bool RequiresPortal => true;
        public bool ProducesJson => true;

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
            var actualMode = preferredMode;
            var usedFallback = false;
            string fallbackReason = null;
            string exportState = null;
            var exportedFiles = new List<string>();
            var messages = new List<string>();

            if (preferredMode == SmartExportMode.Xml)
            {
                var xmlFile = new FileInfo(Path.Combine(targetDirectory.FullName, baseName + ".xml"));
                block.Export(xmlFile, ExportOptions.None, DocumentInfoOptions.None);
                exportedFiles.Add(xmlFile.FullName);
                exportState = "XmlExported";
            }
            else
            {
                var docsDirectory = new DirectoryInfo(Path.Combine(targetDirectory.FullName, baseName + "_docs"));
                if (!docsDirectory.Exists)
                {
                    docsDirectory.Create();
                }

                try
                {
                    var exportResult = block.ExportAsDocuments(docsDirectory, baseName);
                    exportState = exportResult.State.ToString();
                    exportedFiles.AddRange(exportResult.ExportedDocuments.Select(file => file.FullName));
                    messages.AddRange(
                        exportResult.Messages
                            .Select(message => message.Message)
                            .Where(message => !string.IsNullOrWhiteSpace(message)));
                }
                catch (Exception ex)
                {
                    actualMode = SmartExportMode.Xml;
                    usedFallback = true;
                    fallbackReason = ex.Message;

                    var xmlFile = new FileInfo(Path.Combine(targetDirectory.FullName, baseName + ".xml"));
                    block.Export(xmlFile, ExportOptions.None, DocumentInfoOptions.None);
                    exportedFiles.Add(xmlFile.FullName);
                    exportState = "XmlExportedAfterFallback";
                }
            }

            return BuildJson(
                resolution.Project.Name,
                resolvedReference,
                resolution.Device.Name,
                blockResolution,
                preferredMode,
                actualMode,
                targetDirectory.FullName,
                exportState,
                exportedFiles,
                messages,
                usedFallback,
                fallbackReason);
        }

        private static string BuildJson(
            string projectName,
            string deviceReference,
            string deviceName,
            PlcBlockResolution blockResolution,
            SmartExportMode preferredMode,
            SmartExportMode actualMode,
            string targetDirectory,
            string exportState,
            List<string> exportedFiles,
            List<string> messages,
            bool usedFallback,
            string fallbackReason)
        {
            var block = blockResolution.Block;
            var group = string.IsNullOrWhiteSpace(blockResolution.GroupReference) ? "<root>" : blockResolution.GroupReference;
            var json = new StringBuilder();

            json.Append("{\"project\":\"")
                .Append(CommandSupport.EscapeJson(projectName))
                .Append("\",\"device\":{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(deviceReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(deviceName))
                .Append("\"},\"block\":{\"reference\":\"")
                .Append(CommandSupport.EscapeJson(blockResolution.BlockReference))
                .Append("\",\"name\":\"")
                .Append(CommandSupport.EscapeJson(block.Name))
                .Append("\",\"type\":\"")
                .Append(CommandSupport.EscapeJson(CommandSupport.GetPlcBlockTypeName(block)))
                .Append("\",\"number\":")
                .Append(block.Number)
                .Append(",\"programmingLanguage\":\"")
                .Append(CommandSupport.EscapeJson(block.ProgrammingLanguage.ToString()))
                .Append("\",\"group\":\"")
                .Append(CommandSupport.EscapeJson(group))
                .Append("\",\"isKnowHowProtected\":")
                .Append(block.IsKnowHowProtected ? "true" : "false")
                .Append(",\"isConsistent\":")
                .Append(block.IsConsistent ? "true" : "false");

            if (block is InstanceDB instanceDb)
            {
                json.Append(",\"instanceOf\":\"")
                    .Append(CommandSupport.EscapeJson(instanceDb.InstanceOfName))
                    .Append("\"");
            }

            json.Append("},\"export\":{\"preferredMode\":\"")
                .Append(CommandSupport.EscapeJson(preferredMode.ToString()))
                .Append("\",\"actualMode\":\"")
                .Append(CommandSupport.EscapeJson(actualMode.ToString()))
                .Append("\",\"usedFallback\":")
                .Append(usedFallback ? "true" : "false")
                .Append(",\"targetDirectory\":\"")
                .Append(CommandSupport.EscapeJson(targetDirectory))
                .Append("\",\"state\":\"")
                .Append(CommandSupport.EscapeJson(exportState ?? string.Empty))
                .Append("\",\"exportedFiles\":[");

            for (int i = 0; i < exportedFiles.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("\"")
                    .Append(CommandSupport.EscapeJson(exportedFiles[i]))
                    .Append("\"");
            }

            json.Append("],\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("\"")
                    .Append(CommandSupport.EscapeJson(messages[i]))
                    .Append("\"");
            }

            json.Append("]");

            if (usedFallback)
            {
                json.Append(",\"fallbackReason\":\"")
                    .Append(CommandSupport.EscapeJson(fallbackReason ?? string.Empty))
                    .Append("\"");
            }

            json.Append("}}");
            return json.ToString();
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
