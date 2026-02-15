using DataExtractor.Models;

namespace DataExtractor.Extractors;

public record ExampleGroup(string BaseName, List<CodeFileInfo> CodeFiles);

public static class ExampleScanner
{
    /// <summary>
    /// Scans an Examples/ directory and groups files by base name.
    /// E.g. ButtonDefault.razor + ButtonDefault.razor.cs → one group "ButtonDefault"
    /// </summary>
    public static List<ExampleGroup> Scan(string examplesDir)
    {
        if (string.IsNullOrEmpty(examplesDir) || !Directory.Exists(examplesDir))
        {
            return new List<ExampleGroup>();
        }

        var files = Directory.GetFiles(examplesDir);
        var groups = new Dictionary<string, List<CodeFileInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files.OrderBy(f => f))
        {
            var fileName = Path.GetFileName(filePath);
            var baseName = GetBaseName(fileName);

            if (!groups.ContainsKey(baseName))
            {
                groups[baseName] = new List<CodeFileInfo>();
            }

            var code = File.ReadAllText(filePath);
            groups[baseName].Add(new CodeFileInfo
            {
                Code = code,
                Name = fileName,
                IsMain = false,
                Hidden = false,
                Type = 1
            });
        }

        return groups
            .OrderBy(g => g.Key)
            .Select(g => new ExampleGroup(g.Key, g.Value))
            .ToList();
    }

    /// <summary>
    /// Gets the base name for grouping:
    /// "ButtonDefault.razor" → "ButtonDefault"
    /// "ButtonDefault.razor.cs" → "ButtonDefault"
    /// "ButtonDefault.razor.css" → "ButtonDefault"
    /// "CustomCSS.css" → "CustomCSS"
    /// "DataGridTypical_Helper.razor" → "DataGridTypical"
    /// </summary>
    private static string GetBaseName(string fileName)
    {
        // Strip all extensions: .razor.cs → strip .cs then .razor
        var name = fileName;

        // Handle compound extensions
        if (name.EndsWith(".razor.cs", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^".razor.cs".Length];
        }
        else if (name.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^".razor.css".Length];
        }
        else if (name.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^".razor".Length];
        }
        else
        {
            // Generic: strip last extension
            var ext = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext))
            {
                name = name[..^ext.Length];
            }
        }

        // Handle underscore helpers: DataGridTypical_Helper → DataGridTypical
        var underscoreIdx = name.IndexOf('_');
        if (underscoreIdx > 0)
        {
            name = name[..underscoreIdx];
        }

        return name;
    }
}
