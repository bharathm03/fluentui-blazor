using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public static partial class HtmlToPlainText
{
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var text = html;

        // Remove CodeSnippet blocks entirely
        text = CodeSnippetRegex().Replace(text, "");

        // Convert <code> to backticks
        text = CodeTagRegex().Replace(text, "`$1`");

        // Convert <a> to markdown links
        text = AnchorTagRegex().Replace(text, "[$2]($1)");

        // Convert <b>/<strong> to bold
        text = BoldTagRegex().Replace(text, "**$1**");
        text = StrongTagRegex().Replace(text, "**$1**");

        // Convert <em>/<i> to italic
        text = EmTagRegex().Replace(text, "*$1*");
        text = ItalicTagRegex().Replace(text, "*$1*");

        // Convert <blockquote> to > prefix
        text = BlockquoteRegex().Replace(text, m =>
        {
            var inner = Convert(m.Groups[1].Value).Trim();
            return "\n> " + inner.Replace("\n", "\n> ") + "\n";
        });

        // Convert <li> to bullet points
        text = ListItemRegex().Replace(text, "\n- $1");

        // Convert <p> to paragraph breaks
        text = ParagraphRegex().Replace(text, "\n\n$1\n\n");

        // Convert <br> to newlines
        text = BreakRegex().Replace(text, "\n");

        // Convert <kbd> to backticks
        text = KbdTagRegex().Replace(text, "`$1`");

        // Strip remaining HTML tags
        text = AnyTagRegex().Replace(text, "");

        // Decode common HTML entities
        text = text.Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&amp;", "&")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&nbsp;", " ");

        // Collapse multiple blank lines
        text = MultipleNewlinesRegex().Replace(text, "\n\n");

        // Collapse multiple spaces
        text = MultipleSpacesRegex().Replace(text, " ");

        return text.Trim();
    }

    [GeneratedRegex(@"<CodeSnippet[^>]*>.*?</CodeSnippet>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeSnippetRegex();

    [GeneratedRegex(@"<code>(.*?)</code>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CodeTagRegex();

    [GeneratedRegex(@"<a\s[^>]*href=""([^""]*?)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();

    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BoldTagRegex();

    [GeneratedRegex(@"<strong>(.*?)</strong>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex StrongTagRegex();

    [GeneratedRegex(@"<em>(.*?)</em>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex EmTagRegex();

    [GeneratedRegex(@"<i>(.*?)</i>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ItalicTagRegex();

    [GeneratedRegex(@"<blockquote>(.*?)</blockquote>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"<li[^>]*>(.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex(@"<kbd>(.*?)</kbd>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex KbdTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultipleSpacesRegex();
}
