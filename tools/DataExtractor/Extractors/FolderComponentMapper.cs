using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public record ComponentMapping(
    string ComponentName,
    string PageFilePath,
    string ExamplesDir,
    string? ComponentPagePath = null
);

public static partial class FolderComponentMapper
{
    /// <summary>
    /// Service classes that share example folders with their corresponding component.
    /// </summary>
    public static readonly Dictionary<string, ServiceConfig> ServiceMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DialogService"] = new("DialogServicePage.razor", "Dialog", "IDialogService"),
        ["MessageService"] = new("MessageServicePage.razor", "MessageBar", "IMessageService"),
        ["ToastService"] = new("ToastServicePage.razor", "Toast", "IToastService"),
    };

    /// <summary>
    /// Scans demo pages and builds a map of component name → page/examples info.
    /// Page content read during scanning is stored in <paramref name="pageContentCache"/> to avoid re-reading later.
    /// </summary>
    public static Dictionary<string, ComponentMapping> BuildMap(
        string pagesDir,
        HashSet<string> knownComponents,
        Dictionary<string, string>? pageContentCache = null)
    {
        var map = new Dictionary<string, ComponentMapping>(StringComparer.OrdinalIgnoreCase);

        var pageFiles = Directory.GetFiles(pagesDir, "*Page.razor", SearchOption.AllDirectories);

        foreach (var pageFile in pageFiles)
        {
            var content = File.ReadAllText(pageFile);
            pageContentCache?.TryAdd(pageFile, content);
            var pageDir = Path.GetDirectoryName(pageFile)!;
            var examplesDir = Path.Combine(pageDir, "Examples");

            // Extract component names from <ApiDocumentation Component="typeof(FluentXxx)" />
            foreach (Match match in ApiDocRegex().Matches(content))
            {
                var rawType = match.Groups[1].Value;

                // Strip generic parameters: FluentDataGrid<TItem> → FluentDataGrid
                var angleBracket = rawType.IndexOf('<');
                var componentName = angleBracket >= 0 ? rawType[..angleBracket] : rawType;

                if (!componentName.StartsWith("Fluent") || !knownComponents.Contains(componentName))
                {
                    continue;
                }

                map.TryAdd(componentName, new ComponentMapping(
                    componentName,
                    pageFile,
                    Directory.Exists(examplesDir) ? examplesDir : ""
                ));
            }
        }

        // Map service classes to their dedicated service pages and shared example folders
        foreach (var (serviceName, config) in ServiceMappings)
        {
            if (!knownComponents.Contains(serviceName) || map.ContainsKey(serviceName))
            {
                continue;
            }

            var servicePageFile = Directory.GetFiles(pagesDir, config.PageFileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (servicePageFile == null)
            {
                continue;
            }

            var examplesDir = Path.Combine(pagesDir, config.ExampleFolder, "Examples");
            var componentPageFile = Directory.GetFiles(
                Path.Combine(pagesDir, config.ExampleFolder), "*Page.razor", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            map[serviceName] = new ComponentMapping(
                serviceName,
                servicePageFile,
                Directory.Exists(examplesDir) ? examplesDir : "",
                componentPageFile
            );
        }

        return map;
    }

    [GeneratedRegex(@"<ApiDocumentation\s+Component=""typeof\(([^)]+)\)""", RegexOptions.IgnoreCase)]
    private static partial Regex ApiDocRegex();
}

public record ServiceConfig(string PageFileName, string ExampleFolder, string InterfaceName);
