using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaLocalBridge.Services
{
    internal sealed class ResourceReadResult
    {
        public string Content;
        public string Warning;
        public bool Complete;
    }

    // Deliberately limited to the .s7res subset in the reviewed V20 fixture. This is NOT a general YAML parser.
    // Reject all other syntax rather than silently interpreting complex resource structures incorrectly.
    internal static class ResourceTextReader
    {
        private static readonly Regex Entry = new Regex(@"^  - id: (MLC_[A-Za-z0-9]+)$", RegexOptions.CultureInvariant);
        private static readonly Regex Culture = new Regex(@"^    ([A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*): (.*)$", RegexOptions.CultureInvariant);
        private static readonly Regex Key = new Regex(@"\bMLC_[A-Za-z0-9]+\b", RegexOptions.CultureInvariant);

        public static ResourceReadResult Annotate(string document, string path, string requestedCulture)
        {
            var result = new ResourceReadResult { Content = document, Complete = true };
            var keys = Key.Matches(document).Cast<Match>().Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            if (keys.Count == 0) return result;
            if (path == null)
                return Incomplete(result, "Resource keys found without a .s7res file; keys remain unchanged.");
            try
            {
                var values = Parse(path);
                var header = new StringBuilder("// Resource lookup (analysis only; native document below is unchanged):\n");
                var fallbackCount = 0;
                var missing = 0;
                foreach (var key in keys)
                {
                    if (!values.TryGetValue(key, out var locales) || locales.Count == 0)
                    {
                        missing++;
                        continue;
                    }
                    var match = locales.FirstOrDefault(p => string.Equals(p.Key, requestedCulture, StringComparison.OrdinalIgnoreCase));
                    if (match.Key == null)
                    {
                        match = locales.OrderBy(p => p.Key, StringComparer.Ordinal).First();
                        fallbackCount++;
                    }
                    header.Append("// ").Append(key).Append(" [").Append(match.Key).Append("]: ")
                        .Append(match.Value.Replace("\r", "\\r").Replace("\n", "\\n")).Append('\n');
                }
                header.Append(document);
                result.Content = header.ToString();
                if (fallbackCount > 0 || missing > 0)
                    Incomplete(result, $"Resource locale fallback for {fallbackCount} keys; {missing} keys unresolved (original keys and file retained).");
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException || ex is DecoderFallbackException)
            {
                Incomplete(result, "Unsupported or unreadable .s7res syntax: " + ex.Message + "; original keys and file retained.");
            }
            return result;
        }

        private static ResourceReadResult Incomplete(ResourceReadResult result, string message)
        {
            result.Complete = false;
            result.Warning = message;
            return result;
        }

        private static Dictionary<string, Dictionary<string, string>> Parse(string path)
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new FormatException("Resource file exceeds 4 MiB.");
            var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)).TrimStart('\ufeff');
            var rows = text.Replace("\r\n", "\n").Split('\n');
            if (rows.Length < 2 || rows[0] != "MultiLingualTexts:") throw new FormatException("Unknown resource header.");
            var values = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            string id = null;
            foreach (var row in rows.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                var entry = Entry.Match(row);
                if (entry.Success)
                {
                    id = entry.Groups[1].Value;
                    if (values.ContainsKey(id)) throw new FormatException("Duplicate resource id.");
                    values.Add(id, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                    continue;
                }
                var culture = Culture.Match(row);
                if (!culture.Success || id == null) throw new FormatException("Unsupported resource row.");
                var scalar = culture.Groups[2].Value;
                // Only plain, single-line scalar values can be interpreted safely without a full YAML parser.
                if (scalar.Length == 0 || scalar.StartsWith("\"", StringComparison.Ordinal) || scalar.StartsWith("'", StringComparison.Ordinal) ||
                    scalar.StartsWith("|", StringComparison.Ordinal) || scalar.StartsWith(">", StringComparison.Ordinal) ||
                    scalar.Contains(" #") || scalar.Contains(": ") || scalar.Contains("\\"))
                    throw new FormatException("Unsupported quoted, folded, or complex scalar.");
                if (values[id].ContainsKey(culture.Groups[1].Value)) throw new FormatException("Duplicate resource culture.");
                values[id].Add(culture.Groups[1].Value, scalar);
            }
            return values;
        }
    }
}
