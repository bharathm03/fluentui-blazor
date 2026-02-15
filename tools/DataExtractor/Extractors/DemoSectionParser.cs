using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public record DemoSectionInfo(string TypeName, string Title, string Description);

public static partial class DemoSectionParser
{
    /// <summary>
    /// Parses all &lt;DemoSection&gt; blocks from a page and extracts type name + description.
    /// </summary>
    public static List<DemoSectionInfo> Parse(string pageContent)
    {
        var results = new List<DemoSectionInfo>();

        // Match both self-closing and content DemoSection blocks
        var matches = DemoSectionRegex().Matches(pageContent);
        foreach (Match match in matches)
        {
            var attrs = match.Groups[1].Value;
            var innerContent = match.Groups[2].Value;

            // Extract Component="@typeof(Xxx)" or Component="typeof(Xxx)"
            var typeMatch = ComponentAttrRegex().Match(attrs);
            if (!typeMatch.Success)
            {
                continue;
            }

            var typeName = typeMatch.Groups[1].Value;

            // Extract Title
            var titleMatch = TitleAttrRegex().Match(attrs);
            var title = titleMatch.Success ? titleMatch.Groups[1].Value : "";

            // Extract <Description>...</Description> content
            var description = "";
            var descMatch = DescriptionRegex().Match(innerContent);
            if (descMatch.Success)
            {
                description = HtmlToPlainText.Convert(descMatch.Groups[1].Value);
            }

            results.Add(new DemoSectionInfo(typeName, title, description));
        }

        return results;
    }

    // Matches <DemoSection ...> ... </DemoSection> and self-closing <DemoSection ... />
    [GeneratedRegex(@"<DemoSection\s+([^>]*?)(?:>(.*?)</DemoSection>|/>)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DemoSectionRegex();

    [GeneratedRegex(@"Component=""@?typeof\(([^)]+)\)""")]
    private static partial Regex ComponentAttrRegex();

    [GeneratedRegex(@"Title=""([^""]+)""")]
    private static partial Regex TitleAttrRegex();

    [GeneratedRegex(@"<Description>(.*?)</Description>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionRegex();
}
