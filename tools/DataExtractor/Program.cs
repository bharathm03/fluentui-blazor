using System.Text.Json;
using System.Xml.Linq;
using DataExtractor.Extractors;
using DataExtractor.Models;

// Resolve repo root from command-line arg or current directory
var repoRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var pagesDir = Path.Combine(repoRoot, "examples", "Demo", "Shared", "Pages");
var xmlDocPath = Path.Combine(repoRoot, "examples", "Demo", "Shared", "Microsoft.FluentUI.AspNetCore.Components.xml");
var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");
Directory.CreateDirectory(outputDir);

Console.WriteLine($"Repo root: {repoRoot}");
Console.WriteLine($"Pages dir: {pagesDir}");

if (!Directory.Exists(pagesDir))
{
    Console.Error.WriteLine($"Pages directory not found: {pagesDir}");
    return 1;
}

// Step 1: Reflect all Fluent component names
var componentNames = ComponentReflector.GetFluentComponentNames();
Console.WriteLine($"Found {componentNames.Count} Fluent components via reflection");

var knownComponents = new HashSet<string>(componentNames, StringComparer.OrdinalIgnoreCase);

// Step 2: Build folder → component map
var componentMap = FolderComponentMapper.BuildMap(pagesDir, knownComponents);
Console.WriteLine($"Mapped {componentMap.Count} components to demo pages");

// Load XML doc for fallback descriptions
var xmlSummaries = LoadXmlSummaries(xmlDocPath);
Console.WriteLine($"Loaded {xmlSummaries.Count} XML doc summaries");

// Step 3-7: Build outputs
var components = new List<FluentComponentInfo>();
var examples = new List<ExampleUsageDoc>();

foreach (var componentName in componentNames)
{
    var title = NamingHelper.MakeTitle(componentName);
    var description = "";

    if (componentMap.TryGetValue(componentName, out var mapping))
    {
        // Parse description from page
        var pageContent = File.ReadAllText(mapping.PageFilePath);
        description = DemoPageParser.ExtractDescription(pageContent);

        // Scan examples
        if (!string.IsNullOrEmpty(mapping.ExamplesDir) && Directory.Exists(mapping.ExamplesDir))
        {
            var folderKey = NamingHelper.GetFolderKey(mapping.ExamplesDir);
            var exampleGroups = ExampleScanner.Scan(mapping.ExamplesDir);
            var demoSections = DemoSectionParser.Parse(pageContent);

            var seq = 1;
            foreach (var group in exampleGroups)
            {
                // Find matching DemoSection by type name
                var section = demoSections.FirstOrDefault(s =>
                    s.TypeName.Equals(group.BaseName, StringComparison.OrdinalIgnoreCase));

                examples.Add(new ExampleUsageDoc
                {
                    ComponentName = componentName,
                    Title = group.BaseName,
                    Key = NamingHelper.MakeKey(folderKey, seq),
                    Summary = "",
                    CodeFiles = group.CodeFiles,
                    Description = "",
                    OriginalDescription = section?.Description ?? ""
                });
                seq++;
            }
        }
    }

    // Prefix XML doc summary to the page description when both exist
    xmlSummaries.TryGetValue(componentName, out var xmlSummary);
    if (!string.IsNullOrWhiteSpace(xmlSummary) && !string.IsNullOrWhiteSpace(description))
    {
        description = xmlSummary + "\n\n" + description;
    }
    else if (!string.IsNullOrWhiteSpace(xmlSummary))
    {
        description = xmlSummary;
    }

    components.Add(new FluentComponentInfo
    {
        Title = title,
        Description = description,
        Name = componentName,
        Aliases = null
    });
}

// Serialize to JSON
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = null, // PascalCase
    WriteIndented = true
};

var componentsJson = JsonSerializer.Serialize(components, jsonOptions);
var examplesJson = JsonSerializer.Serialize(examples, jsonOptions);

var componentsPath = Path.Combine(outputDir, "FluentComponents.json");
var examplesPath = Path.Combine(outputDir, "FluentExampleUsageDoc.json");

File.WriteAllText(componentsPath, componentsJson);
File.WriteAllText(examplesPath, examplesJson);

Console.WriteLine($"\nOutput:");
Console.WriteLine($"  {componentsPath} ({components.Count} components)");
Console.WriteLine($"  {examplesPath} ({examples.Count} examples)");

return 0;

// --- Helper functions ---

static Dictionary<string, string> LoadXmlSummaries(string xmlPath)
{
    var summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (!File.Exists(xmlPath))
    {
        return summaries;
    }

    var doc = XDocument.Load(xmlPath);
    var members = doc.Descendants("member");

    foreach (var member in members)
    {
        var nameAttr = member.Attribute("name")?.Value;
        if (nameAttr == null || !nameAttr.StartsWith("T:"))
        {
            continue;
        }

        // T:Microsoft.FluentUI.AspNetCore.Components.FluentButton → FluentButton
        var fullType = nameAttr["T:".Length..];
        var typeName = fullType.Split('.').Last();

        if (!typeName.StartsWith("Fluent"))
        {
            continue;
        }

        var summary = member.Element("summary")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            // Clean up XML doc artifacts
            summary = summary.Replace("\r\n", " ").Replace("\n", " ");
            summary = System.Text.RegularExpressions.Regex.Replace(summary, @"\s{2,}", " ");
            summary = System.Text.RegularExpressions.Regex.Replace(summary, @"<[^>]+>", "");
            summaries.TryAdd(typeName, summary.Trim());
        }
    }

    return summaries;
}
