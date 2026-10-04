using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;

namespace TiaLocalBridge.Services
{
    // Siemens-dependent adapter; decisions and validation live in the production pure engine.
    internal static class BlockExportService
    {
        public static BlockExportResult Export(PlcBlock block, DirectoryInfo directory, string baseName)
        {
            return Run(block, directory, baseName, true);
        }

        public static BlockExportResult ExportForRead(PlcBlock block, DirectoryInfo directory)
        {
            return Run(block, directory, "block", false);
        }

        private static BlockExportResult Run(PlcBlock block, DirectoryInfo directory, string baseName, bool legacy)
        {
            return BlockExportEngine.Export(block.ProgrammingLanguage == ProgrammingLanguage.SCL, legacy, directory, baseName,
                (docs, name) =>
                {
                    var exported = block.ExportAsDocuments(docs, name);
                    return new DocumentExportAttempt
                    {
                        State = exported.State.ToString(),
                        Files = exported.ExportedDocuments.Select(file => file.FullName).ToList(),
                        Messages = exported.Messages.Select(message => message.Message).Where(message => !string.IsNullOrWhiteSpace(message)).ToList()
                    };
                },
                file => block.Export(file, ExportOptions.None, DocumentInfoOptions.None));
        }
    }
}
