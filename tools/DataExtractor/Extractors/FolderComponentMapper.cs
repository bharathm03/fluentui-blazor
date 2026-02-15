using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public record ComponentMapping(
    string ComponentName,
    string PageFilePath,
    string ExamplesDir
);

public static partial class FolderComponentMapper
{
    /// <summary>
    /// Scans demo pages and builds a map of component name → page/examples info.
    /// </summary>
    public static Dictionary<string, ComponentMapping> BuildMap(string pagesDir, HashSet<string> knownComponents)
    {
        var map = new Dictionary<string, ComponentMapping>(StringComparer.OrdinalIgnoreCase);

        var pageFiles = Directory.GetFiles(pagesDir, "*Page.razor", SearchOption.AllDirectories);

        foreach (var pageFile in pageFiles)
        {
            var content = File.ReadAllText(pageFile);
            var pageDir = Path.GetDirectoryName(pageFile)!;
            var examplesDir = Path.Combine(pageDir, "Examples");

            // Extract component names from <ApiDocumentation Component="typeof(FluentXxx)" />
            var matches = ApiDocRegex().Matches(content);
            foreach (Match match in matches)
            {
                var rawType = match.Groups[1].Value;

                // Strip generic parameters: FluentDataGrid<> → FluentDataGrid
                var componentName = GenericSuffixRegex().Replace(rawType, "");

                if (!componentName.StartsWith("Fluent"))
                {
                    continue;
                }

                if (!knownComponents.Contains(componentName))
                {
                    continue;
                }

                // Only map the first page found for each component
                if (map.ContainsKey(componentName))
                {
                    continue;
                }

                map[componentName] = new ComponentMapping(
                    componentName,
                    pageFile,
                    Directory.Exists(examplesDir) ? examplesDir : ""
                );
            }
        }

        return map;
    }

    [GeneratedRegex(@"<ApiDocumentation\s+Component=""typeof\(([^)]+)\)""", RegexOptions.IgnoreCase)]
    private static partial Regex ApiDocRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex GenericSuffixRegex();
}
