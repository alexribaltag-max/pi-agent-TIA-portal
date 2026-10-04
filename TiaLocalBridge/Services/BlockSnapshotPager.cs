using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace TiaLocalBridge.Services
{
    [DataContract]
    internal sealed class BlockPage
    {
        [DataMember] public int schemaVersion { get; set; }
        [DataMember] public string artifactId { get; set; }
        [DataMember] public string blockRef { get; set; }
        [DataMember] public string project { get; set; }
        [DataMember] public string deviceRef { get; set; }
        [DataMember] public string language { get; set; }
        [DataMember] public string locale { get; set; }
        [DataMember] public string sourceMap { get; set; }
        [DataMember] public bool analysisOnly { get; set; }
        [DataMember] public string fidelity { get; set; }
        [DataMember] public string capturedAt { get; set; }
        [DataMember] public string content { get; set; }
        [DataMember] public bool completeForSelection { get; set; }
        [DataMember] public bool incompleteUnit { get; set; }
        [DataMember] public int startLine { get; set; }
        [DataMember] public int endLine { get; set; }
        [DataMember] public bool hasMore { get; set; }
        [DataMember] public string nextCursor { get; set; }
        [DataMember] public string warning { get; set; }
        [DataMember] public string reason { get; set; }
    }

    [DataContract]
    internal sealed class BlockCursor
    {
        [DataMember] public string id { get; set; }
        [DataMember] public string manifestHash { get; set; }
        [DataMember] public int line { get; set; }
        [DataMember] public int column { get; set; }
        [DataMember] public bool incompleteUnit { get; set; }
    }

    // All pages are from a verified local snapshot, never a new Openness export. A process-local
    // signing key prevents edited cursors; bridge restart deliberately invalidates continuation.
    internal static class BlockSnapshotPager
    {
        private static readonly byte[] Secret = NewSecret();
        private static byte[] NewSecret() { var key = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key); return key; }
        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TiaLocalBridge", "block-snapshots");
        public static BlockPage Read(string id, string cursor = null, int maxBytes = 8192, int maxLines = 150) => ReadAt(Root, id, cursor, maxBytes, maxLines);

        // Root injection is for pure offline tests only; the command never accepts a filesystem root.
        internal static BlockPage ReadAt(string root, string id, string cursor = null, int maxBytes = 8192, int maxLines = 150)
        {
            if (maxBytes < 256 || maxBytes > 16384) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (maxLines < 10 || maxLines > 1000) throw new ArgumentOutOfRangeException(nameof(maxLines));
            if (!Guid.TryParseExact(id, "N", out var parsed) || parsed.ToString("N") != id)
                throw new ArgumentException("Invalid block artifact id.");
            var directory = Path.Combine(root, id);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Snapshot directory is a link.");
            var manifestPath = Path.Combine(directory, "manifest.json");
            if ((File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Snapshot manifest is a link.");
            var manifestBytes = File.ReadAllBytes(manifestPath);
            if (manifestBytes.Length > 256 * 1024) throw new InvalidOperationException("Oversized snapshot manifest.");
            var manifestHash = Hash(manifestBytes);
            BlockCursor continuation = null;
            if (cursor != null) continuation = VerifyCursor(cursor, id, manifestHash);
            var manifest = InventoryJson.Deserialize<SnapshotManifest>(new UTF8Encoding(false, true).GetString(manifestBytes));
            if (manifest.schemaVersion != 1 || manifest.artifactId != id || !manifest.analysisOnly || manifest.files == null || manifest.files.Count == 0 || manifest.files.Count > 64)
                throw new InvalidOperationException("Invalid snapshot manifest.");
            var fullRoot = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            byte[] verifiedView = null;
            byte[] verifiedBoundaries = null;
            foreach (var file in manifest.files)
            {
                if (file == null || file.path == null || file.path.Contains("\\") || file.path.StartsWith("/", StringComparison.Ordinal) ||
                    file.path.Split('/').Any(part => part == "" || part == "." || part == ".." || part.IndexOf(':') >= 0) ||
                    !(file.path.StartsWith("native/", StringComparison.Ordinal) || file.path.StartsWith("view/", StringComparison.Ordinal)) ||
                    !names.Add(file.path)) throw new InvalidOperationException("Invalid snapshot file entry.");
                if (file.bytes < 0 || file.bytes > 16 * 1024 * 1024) throw new InvalidOperationException("Snapshot file exceeds paging limit.");
                totalBytes += file.bytes;
                if (totalBytes > 32 * 1024 * 1024) throw new InvalidOperationException("Snapshot exceeds paging limit.");
                var path = Path.GetFullPath(Path.Combine(directory, file.path.Replace('/', Path.DirectorySeparatorChar)));
                var current = directory;
                foreach (var part in file.path.Split('/'))
                {
                    current = Path.Combine(current, part);
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Snapshot path contains a link.");
                }
                if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || new FileInfo(path).Length != file.bytes)
                    throw new InvalidOperationException("Snapshot files changed; continuation refused.");
                var verifiedBytes = File.ReadAllBytes(path);
                if (verifiedBytes.LongLength != file.bytes || !string.Equals(Hash(verifiedBytes), file.sha256, StringComparison.Ordinal))
                    throw new InvalidOperationException("Snapshot files changed; continuation refused.");
                if (file.path == "view/analysis.txt") verifiedView = verifiedBytes;
                if (file.path == "view/boundaries.json") verifiedBoundaries = verifiedBytes;
            }
            if (!names.Contains("view/analysis.txt")) throw new InvalidOperationException("Missing analysis view.");
            var text = new UTF8Encoding(false, true).GetString(verifiedView ?? throw new InvalidOperationException("Missing verified view."));
            var rows = SplitLines(text);
            var safeLines = new HashSet<int>();
            if (verifiedBoundaries != null)
            {
                if (manifest.fidelity != "reconstructed") throw new InvalidOperationException("Unexpected boundaries for non-reconstructed view.");
                var boundaryLines = InventoryJson.Deserialize<List<int>>(new UTF8Encoding(false, true).GetString(verifiedBoundaries));
                if (boundaryLines == null || boundaryLines.Count > 1024 || boundaryLines.Any(n => n < 1 || n > rows.Count) ||
                    !boundaryLines.SequenceEqual(boundaryLines.Distinct().OrderBy(n => n)))
                    throw new InvalidOperationException("Invalid semantic boundaries.");
                safeLines = new HashSet<int>(boundaryLines);
            }
            var index = continuation == null ? 0 : continuation.line;
            var column = continuation == null ? 0 : continuation.column;
            if (index < 0 || index > rows.Count || (index == rows.Count && column != 0) ||
                (index < rows.Count && (column < 0 || column >= rows[index].Length && column != 0)))
                throw new ArgumentException("Cursor position outside snapshot.");
            var page = new BlockPage { schemaVersion = 1, artifactId = id, blockRef = manifest.blockRef,
                project = manifest.project, deviceRef = manifest.deviceRef, language = manifest.language, locale = manifest.locale,
                sourceMap = names.Contains("view/source-map.json") ? "view/source-map.json" : null, analysisOnly = true,
                fidelity = manifest.fidelity, capturedAt = manifest.capturedAt, reason = manifest.reason,
                warning = manifest.warning, startLine = rows.Count == 0 ? 0 : index + 1 };
            var buffer = new StringBuilder();
            var usedBytes = 0;
            var usedLines = 0;
            var splitUnit = column > 0 || (continuation != null && continuation.incompleteUnit);
            while (index < rows.Count && usedLines < maxLines)
            {
                var row = rows[index];
                var remaining = row.Substring(column);
                var length = Encoding.UTF8.GetByteCount(remaining);
                if (usedBytes + length > maxBytes)
                {
                    if (buffer.Length > 0) break;
                    var take = 0;
                    var bytes = 0;
                    while (column + take < row.Length)
                    {
                        var c = row[column + take];
                        var chars = char.IsHighSurrogate(c) && column + take + 1 < row.Length && char.IsLowSurrogate(row[column + take + 1]) ? 2 : 1;
                        var cost = Encoding.UTF8.GetByteCount(row.Substring(column + take, chars));
                        if (bytes + cost > maxBytes) break;
                        bytes += cost;
                        take += chars;
                    }
                    if (take == 0) throw new InvalidOperationException("Cannot page oversized character.");
                    buffer.Append(row, column, take);
                    column += take;
                    usedBytes += bytes;
                    splitUnit = true;
                    break;
                }
                if (usedLines > 0 && safeLines.Contains(index + 1) && usedBytes + length > maxBytes / 2) break;
                buffer.Append(remaining);
                usedBytes += length;
                usedLines++;
                index++;
                column = 0;
                if (usedBytes >= maxBytes) break;
            }
            page.content = buffer.ToString();
            page.endLine = index + (column > 0 ? 1 : 0);
            page.hasMore = index < rows.Count;
            var endInside = page.hasMore && (column > 0 || !safeLines.Contains(index + 1));
            page.incompleteUnit = splitUnit || endInside;
            page.completeForSelection = !page.incompleteUnit && (manifest.fidelity == "native-text" || manifest.fidelity == "reconstructed");
            if (page.hasMore) page.nextCursor = SignCursor(new BlockCursor { id = id, manifestHash = manifestHash, line = index, column = column, incompleteUnit = endInside });
            if (page.incompleteUnit) page.warning = (page.warning == null ? "" : page.warning + " ") + "Page contains an incomplete analysis unit; this slice is not a complete network/section.";
            return page;
        }
        private static List<string> SplitLines(string text)
        {
            if (text.Length == 0) return new List<string>();
            var result = new List<string>();
            var start = 0;
            for (var i = 0; i < text.Length; i++) if (text[i] == '\n') { result.Add(text.Substring(start, i - start + 1)); start = i + 1; }
            if (start < text.Length) result.Add(text.Substring(start));
            return result;
        }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] Decode(string text)
        {
            if (text.Length > 2048 || text.Length == 0 || text.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new ArgumentException("Malformed block cursor.");
            try
            {
                var bytes = Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
                if (B64(bytes) != text) throw new ArgumentException("Noncanonical block cursor.");
                return bytes;
            }
            catch (FormatException) { throw new ArgumentException("Malformed block cursor."); }
        }
        private static string SignCursor(BlockCursor cursor)
        {
            var data = Encoding.UTF8.GetBytes(InventoryJson.Serialize(cursor));
            using (var hmac = new HMACSHA256(Secret)) return B64(data) + "." + B64(hmac.ComputeHash(data));
        }
        private static BlockCursor VerifyCursor(string token, string id, string manifestHash)
        {
            var parts = token.Split('.');
            if (parts.Length != 2) throw new ArgumentException("Malformed block cursor.");
            var payload = Decode(parts[0]);
            var supplied = Decode(parts[1]);
            byte[] signature;
            using (var hmac = new HMACSHA256(Secret)) signature = hmac.ComputeHash(payload);
            if (signature.Length != supplied.Length) throw new ArgumentException("Invalid block cursor signature.");
            var difference = 0;
            for (int i = 0; i < signature.Length; i++) difference |= signature[i] ^ supplied[i];
            if (difference != 0) throw new ArgumentException("Invalid block cursor signature.");
            var decoded = InventoryJson.Deserialize<BlockCursor>(new UTF8Encoding(false, true).GetString(payload));
            if (decoded.id != id || decoded.manifestHash != manifestHash) throw new InvalidOperationException("Stale or mismatched block cursor.");
            return decoded;
        }
    }
}
