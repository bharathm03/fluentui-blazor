using System.Text.RegularExpressions;

namespace DataExtractor.Extractors;

public static partial class NamingHelper
{
    /// <summary>
    /// Converts "FluentDataGrid" → "Data Grid"
    /// </summary>
    public static string MakeTitle(string componentName)
    {
        var name = componentName;

        // Remove "Fluent" prefix
        if (name.StartsWith("Fluent"))
        {
            name = name["Fluent".Length..];
        }

        // Insert spaces before uppercase letters
        name = UppercaseSplitRegex().Replace(name, " $1").Trim();

        return name;
    }

    /// <summary>
    /// Generates a key like "BUTTON01" from folder name and sequence number.
    /// </summary>
    public static string MakeKey(string folderName, int sequence)
    {
        return $"{folderName.ToUpperInvariant()}{sequence:D2}";
    }

    /// <summary>
    /// Extracts the folder key name from a path.
    /// For nested folders like Badge/CounterBadge, returns "CounterBadge".
    /// </summary>
    public static string GetFolderKey(string examplesDir)
    {
        // Go up from Examples/ to get the component folder
        var parent = Path.GetDirectoryName(examplesDir) ?? "";
        return Path.GetFileName(parent);
    }

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex UppercaseSplitRegex();
}
