using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaLocalBridge.Services
{
    internal sealed class BlockExportResult
    {
        public string PreferredMode;
        public string ActualMode;
        public string State;
        public string TargetDirectory;
        public List<string> Files = new List<string>();
        public List<string> Messages = new List<string>();
        public string FallbackReason;
        public bool UsedFallback => FallbackReason != null;
    }

    internal sealed class DocumentExportAttempt
    {
        public string State;
        public List<string> Files = new List<string>();
        public List<string> Messages = new List<string>();
    }

    // Pure, testable boundary: the Openness adapter supplies export delegates. Only the legacy
    // mode retries arbitrary document exceptions. New reads fail closed on unknown failures.
    internal static class BlockExportEngine
    {
        public static BlockExportResult Export(bool preferXml, bool legacy, DirectoryInfo directory, string baseName,
            Func<DirectoryInfo, string, DocumentExportAttempt> documents, Action<FileInfo> xml)
        {
            directory.Create();
            var result = new BlockExportResult { PreferredMode = preferXml ? "Xml" : "Documents", TargetDirectory = directory.FullName };
            if (preferXml)
            {
                ExportXml(xml, legacy ? directory : new DirectoryInfo(Path.Combine(directory.FullName, "native")), baseName, result);
                return result;
            }
            var docs = new DirectoryInfo(Path.Combine(directory.FullName, legacy ? baseName + "_docs" : "native"));
            docs.Create();
            try
            {
                var outcome = documents(docs, baseName) ?? throw new InvalidOperationException("Null document export result.");
                result.State = outcome.State;
                result.ActualMode = "Documents";
                result.Files.AddRange(outcome.Files ?? new List<string>());
                result.Messages.AddRange(outcome.Messages ?? new List<string>());
                if (!legacy) ValidateDocuments(result, docs);
                return result;
            }
            catch (Exception ex)
            {
                if (!legacy)
                    throw new InvalidOperationException("Document export was not verified; XML fallback is disabled for unclassified failures: " + ex.Message, ex);
                result.FallbackReason = ex.Message;
                result.Files.Clear();
                try
                {
                    ExportXml(xml, directory, baseName, result);
                    result.State = "XmlExportedAfterFallback";
                    return result;
                }
                catch (Exception fallback)
                {
                    throw new InvalidOperationException("Document export failed: " + ex.Message + "; XML fallback failed: " + fallback.Message, fallback);
                }
            }
        }

        private static void ExportXml(Action<FileInfo> action, DirectoryInfo directory, string name, BlockExportResult result)
        {
            directory.Create();
            var file = new FileInfo(Path.Combine(directory.FullName, name + ".xml"));
            action(file);
            file.Refresh();
            if (!file.Exists || file.Length == 0) throw new InvalidOperationException("XML export produced no content.");
            result.Files.Add(file.FullName);
            result.ActualMode = "Xml";
            result.State = "XmlExported";
        }

        private static void ValidateDocuments(BlockExportResult result, DirectoryInfo directory)
        {
            // A state spelling other than an observed success must be verified on V21 before it can be accepted.
            if (result.State != "Success" && result.State != "Succeeded")
                throw new InvalidOperationException("Document export returned state '" + result.State + "': " + string.Join(" | ", result.Messages.Take(3)));
            var root = Path.GetFullPath(directory.FullName).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (result.Files.Count == 0 || result.Files.Count(path => string.Equals(Path.GetExtension(path), ".s7dcl", StringComparison.OrdinalIgnoreCase)) != 1)
                throw new InvalidOperationException("Document export must produce exactly one .s7dcl file.");
            foreach (var path in result.Files)
            {
                var full = Path.GetFullPath(path);
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(full) || new FileInfo(full).Length == 0)
                    throw new InvalidOperationException("Document export returned an invalid, empty or out-of-snapshot file.");
            }
        }
    }
}
