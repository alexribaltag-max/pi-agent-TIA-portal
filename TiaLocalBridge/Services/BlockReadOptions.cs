using System;
using System.Globalization;

namespace TiaLocalBridge.Services
{
    internal static class BlockReadOptions
    {
        public static string ParseLocale(string[] args)
        {
            if (args == null || (args.Length != 2 && args.Length != 3) ||
                string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("GETPLCBLOCK requires device reference, block reference and optional locale=<culture>.");
            if (args.Length == 2) return "en-US"; // Provisional until a project-preference policy is approved.
            var value = args[2] ?? "";
            if (!value.StartsWith("locale=", StringComparison.Ordinal) || value.Length < 8 || value.IndexOf('|') >= 0)
                throw new ArgumentException("Only one nonempty locale=<culture> option is supported.");
            var name = value.Substring(7);
            if (name.Length > 35 || name.StartsWith("-", StringComparison.Ordinal) || name.EndsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Invalid locale.");
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(name); }
            catch (CultureNotFoundException) { throw new ArgumentException("Invalid locale."); }
            if (culture.Equals(CultureInfo.InvariantCulture)) throw new ArgumentException("Invariant locale is not supported.");
            return culture.Name;
        }
    }
}
