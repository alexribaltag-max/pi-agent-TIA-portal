using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization;

namespace TiaLocalBridge.Services
{
    [DataContract]
    internal sealed class SnapshotFile
    {
        [DataMember] public string path { get; set; }
        [DataMember] public string sha256 { get; set; }
        [DataMember] public long bytes { get; set; }
    }

    [DataContract]
    internal sealed class SnapshotManifest
    {
        [DataMember] public int schemaVersion { get; set; }
        [DataMember] public string artifactId { get; set; }
        [DataMember] public string capturedAt { get; set; }
        [DataMember] public string project { get; set; }
        [DataMember] public string deviceRef { get; set; }
        [DataMember] public string blockRef { get; set; }
        [DataMember] public string blockType { get; set; }
        [DataMember] public string language { get; set; }
        [DataMember] public string locale { get; set; }
        [DataMember] public string exportMode { get; set; }
        [DataMember] public string exportState { get; set; }
        [DataMember] public string fallbackReason { get; set; }
        [DataMember] public string fidelity { get; set; }
        [DataMember] public string reason { get; set; }
        [DataMember] public string warning { get; set; }
        [DataMember] public bool analysisOnly { get; set; }
        [DataMember] public List<SnapshotFile> files { get; set; }
    }

    internal static class BlockSnapshot
    {
        // Local-only, unique per read. No user-supplied paths or names are used as directory components.
        public static string CreateDirectory(out string id)
        {
            id = Guid.NewGuid().ToString("N");
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TiaLocalBridge", "block-snapshots");
            EnsureCapacity(root);
            var path = Path.Combine(root, id);
            Directory.CreateDirectory(path);
            return path;
        }

        // Never silently delete engineering exports. Count orphaned/failed captures too.
        public static void EnsureCapacity(string root)
        {
            if (!Directory.Exists(root)) return;
            long bytes = 0;
            var directories = Directory.GetDirectories(root);
            if (directories.Length >= 100) throw new IOException("Block snapshot limit (100) reached; manual retention review required.");
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                bytes += new FileInfo(file).Length;
                if (bytes >= 512L * 1024 * 1024)
                    throw new IOException("Block snapshot disk limit (512 MiB) reached; manual retention review required.");
            }
        }

        public static SnapshotManifest Save(string directory, SnapshotManifest manifest, IEnumerable<string> nativeFiles, string content, IEnumerable<string> analysisFiles = null)
        {
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var view = Path.Combine(directory, "view", "analysis.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(view));
            File.WriteAllText(view, content ?? "", new UTF8Encoding(false));
            manifest.schemaVersion = 1;
            manifest.analysisOnly = true;
            manifest.capturedAt = DateTime.UtcNow.ToString("o");
            manifest.files = nativeFiles.Concat(analysisFiles ?? Enumerable.Empty<string>()).Concat(new[] { view }).Select(path =>
            {
                var full = Path.GetFullPath(path);
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Export file escaped snapshot directory.");
                var bytes = File.ReadAllBytes(full);
                using (var hash = SHA256.Create())
                {
                    return new SnapshotFile { path = full.Substring(root.Length).Replace('\\', '/'), bytes = bytes.LongLength,
                        sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() };
                }
            }).ToList();
            File.WriteAllText(Path.Combine(directory, "manifest.json"), InventoryJson.Serialize(manifest), new UTF8Encoding(false));
            return manifest;
        }

        public static string ReadDocument(IEnumerable<string> files, out string warning)
        {
            bool complete;
            return ReadDocument(files, "en-US", out warning, out complete);
        }

        public static string ReadDocument(IEnumerable<string> files, string culture, out string warning, out bool complete)
        {
            warning = null;
            complete = true;
            var documents = files.Where(path => string.Equals(Path.GetExtension(path), ".s7dcl", StringComparison.OrdinalIgnoreCase)).ToList();
            if (documents.Count != 1) throw new InvalidOperationException("Expected exactly one .s7dcl document.");
            var document = documents[0];
            if (new FileInfo(document).Length > 4 * 1024 * 1024) throw new InvalidOperationException("Document exceeds the 4 MiB reader limit.");
            // Strict decoder; never guess a replacement character or silently lose code.
            var bytes = File.ReadAllBytes(document);
            var text = new UTF8Encoding(false, true).GetString(bytes);
            if (text.Length > 0 && text[0] == '\ufeff') text = text.Substring(1);
            var resources = files.Where(path => string.Equals(Path.GetExtension(path), ".s7res", StringComparison.OrdinalIgnoreCase)).ToList();
            if (resources.Count > 1) throw new InvalidOperationException("Multiple .s7res resources cannot be selected safely.");
            var resolved = ResourceTextReader.Annotate(text, resources.Count == 0 ? null : resources[0], culture);
            warning = resolved.Warning;
            complete = resolved.Complete;
            return resolved.Content;
        }

    }
}
