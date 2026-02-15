using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public static partial class DemoPageParser
{
    /// <summary>
    /// Extracts the description HTML between &lt;h1&gt; and &lt;h2 id="example"&gt; from a page,
    /// then converts to plain text/markdown.
    /// </summary>
    public static string ExtractDescription(string pageContent)
    {
        // Find content between </h1> and <h2 (the first h2, typically id="example")
        var match = DescriptionRegex().Match(pageContent);
        if (!match.Success)
        {
            return "";
        }

        var html = match.Groups[1].Value.Trim();
        return HtmlToPlainText.Convert(html);
    }

    [GeneratedRegex(@"</h1>\s*(.*?)\s*<h2[\s>]", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionRegex();
}
