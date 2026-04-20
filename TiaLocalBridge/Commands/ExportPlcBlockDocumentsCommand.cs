using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class ExportPlcBlockDocumentsCommand : ITiaCommand
    {
        public string Name => "EXPORTPLCBLOCKDOCS";
        public string Description => "Safely exports one PLC block as documents so you can inspect how TIA represents the block content depending on its type and programming language.";
        public string Usage => "EXPORTPLCBLOCKDOCS|<device-reference>|<block-reference>|<target-directory>|[file-name-without-extension]";
        public string Example => @"EXPORTPLCBLOCKDOCS|DemoProject/PLC_1|Main/FB_Machine|C:\Exports\BlockDocs|FB_Machine";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 3 || providedArgs.Length > 4)
            {
                throw new ArgumentException($"Expected three or four arguments (<device-reference>, <block-reference>, <target-directory>, optional [file-name-without-extension]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var resolution = CommandSupport.ResolveDeviceByReference(portal, providedArgs[0]);
            var plcSoftware = CommandSupport.TryGetPlcSoftware(resolution.Device);
            var resolvedReference = CommandSupport.GetDeviceReference(resolution.Project, resolution.Device);

            if (plcSoftware == null)
            {
                throw new InvalidOperationException($"Device '{resolvedReference}' does not contain PLC software.");
            }

            var blockResolution = CommandSupport.ResolvePlcBlock(plcSoftware, providedArgs[1]);
            var targetDirectory = new DirectoryInfo(providedArgs[2]);
            if (!targetDirectory.Exists)
            {
                targetDirectory.Create();
            }

            var fileNameWithoutExtension = providedArgs.Length == 4
                ? CommandSupport.SanitizeFileName(providedArgs[3])
                : CommandSupport.SanitizeFileName(blockResolution.Block.Name);

            var exportResult = blockResolution.Block.ExportAsDocuments(targetDirectory, fileNameWithoutExtension);
            var exportedFiles = exportResult.ExportedDocuments
                .Select(file => file.FullName)
                .ToList();
            var messages = exportResult.Messages
                .Select(message => message.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            var result = $"Exported PLC block documents for '{blockResolution.BlockReference}' from '{resolvedReference}' to '{targetDirectory.FullName}' with state '{exportResult.State}'.";
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
    }
}
