using System;
using System.Linq;
using Siemens.Engineering;

namespace TiaLocalBridge.Commands
{
    internal class SearchHardwareCatalogCommand : ITiaCommand
    {
        public string Name => "SEARCHHWCATALOG";
        public string Description => "Searches the TIA hardware catalog and returns matching entries with type identifier, article number, version, and catalog path so you can choose a concrete device type before creating hardware.";
        public string Usage => "SEARCHHWCATALOG|<filter>|[max-results]";
        public string Example => "SEARCHHWCATALOG|1510SP-1 PN|10";
        public bool RequiresPortal => true;
        public bool ProducesJson => false;

        public string Execute(string[] args, TiaPortal portal)
        {
            var providedArgs = CommandSupport.GetProvidedArgs(args);
            if (providedArgs.Length < 1 || providedArgs.Length > 2)
            {
                throw new ArgumentException($"Expected one or two arguments (<filter>, optional [max-results]). {Description} Usage: {Usage}. Example: {Example}");
            }

            var filter = providedArgs[0];
            var maxResults = 20;
            if (providedArgs.Length == 2 && !int.TryParse(providedArgs[1], out maxResults))
            {
                throw new ArgumentException($"Invalid [max-results] value '{providedArgs[1]}'. It must be an integer. Usage: {Usage}. Example: {Example}");
            }

            if (maxResults <= 0)
            {
                throw new ArgumentException("[max-results] must be greater than zero.");
            }

            var matches = portal.HardwareCatalog
                .Find(filter)
                .Where(entry => entry != null)
                .Take(maxResults)
                .Select(entry => string.Format(
                    "{0} [TypeIdentifier={1}, ArticleNumber={2}, Version={3}, CatalogPath={4}]",
                    string.IsNullOrWhiteSpace(entry.TypeName) ? "<no-type-name>" : entry.TypeName,
                    string.IsNullOrWhiteSpace(entry.TypeIdentifier) ? "<no-type-identifier>" : entry.TypeIdentifier,
                    string.IsNullOrWhiteSpace(entry.ArticleNumber) ? "<no-article-number>" : entry.ArticleNumber,
                    string.IsNullOrWhiteSpace(entry.Version) ? "<no-version>" : entry.Version,
                    string.IsNullOrWhiteSpace(entry.CatalogPath) ? "<no-catalog-path>" : entry.CatalogPath))
                .ToList();

            return matches.Any()
                ? $"Hardware catalog matches for '{filter}': {string.Join(" || ", matches)}"
                : $"No hardware catalog entries matched '{filter}'.";
        }
    }
}
