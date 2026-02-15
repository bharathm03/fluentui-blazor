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

// Step 1: Discover component and service names
var componentNames = ComponentReflector.GetFluentComponentNames();
Console.WriteLine($"Found {componentNames.Count} Fluent components via reflection");

var knownComponents = new HashSet<string>(componentNames, StringComparer.OrdinalIgnoreCase);

// Cache: scan results, page content, and DemoSections per examples dir to avoid duplicate I/O
var scanCache = new Dictionary<string, List<ExampleGroup>>(StringComparer.OrdinalIgnoreCase);
var pageContentCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var demoSectionCache = new Dictionary<string, List<DemoSectionInfo>>(StringComparer.OrdinalIgnoreCase);

// Step 2: Build folder → component map (populates pageContentCache to avoid re-reading)
var componentMap = FolderComponentMapper.BuildMap(pagesDir, knownComponents, pageContentCache);
Console.WriteLine($"Mapped {componentMap.Count} components to demo pages");

// Load XML doc summaries
var xmlSummaries = LoadXmlSummaries(xmlDocPath);
Console.WriteLine($"Loaded {xmlSummaries.Count} XML doc summaries");

List<ExampleGroup> GetOrScanExamples(string examplesDir)
{
    if (!scanCache.TryGetValue(examplesDir, out var groups))
    {
        groups = ExampleScanner.Scan(examplesDir);
        scanCache[examplesDir] = groups;
    }
    return groups;
}

string GetOrReadPage(string path)
{
    if (!pageContentCache.TryGetValue(path, out var content))
    {
        content = File.ReadAllText(path);
        pageContentCache[path] = content;
    }
    return content;
}

List<DemoSectionInfo> GetOrParseDemoSections(string examplesDir)
{
    if (!demoSectionCache.TryGetValue(examplesDir, out var sections))
    {
        sections = new List<DemoSectionInfo>();
        var componentDir = Path.GetDirectoryName(examplesDir);
        if (componentDir == null) { demoSectionCache[examplesDir] = sections; return sections; }
        var pageFiles = Directory.GetFiles(componentDir, "*Page.razor", SearchOption.AllDirectories);
        foreach (var pageFile in pageFiles)
        {
            sections.AddRange(DemoSectionParser.Parse(GetOrReadPage(pageFile)));
        }
        demoSectionCache[examplesDir] = sections;
    }
    return sections;
}

// Step 3: Pre-compute service → example ownership by checking code for service interface injection
var serviceOwnedExamples = BuildServiceOwnedExamples(componentNames, componentMap);

// Step 4: Build outputs
var components = new List<FluentComponentInfo>();
var examples = new List<ExampleUsageDoc>();

foreach (var componentName in componentNames)
{
    var title = NamingHelper.MakeTitle(componentName);
    var description = "";
    var isService = FolderComponentMapper.ServiceMappings.ContainsKey(componentName);

    if (componentMap.TryGetValue(componentName, out var mapping))
    {
        var pageContent = GetOrReadPage(mapping.PageFilePath);
        description = DemoPageParser.ExtractDescription(pageContent);

        if (!string.IsNullOrEmpty(mapping.ExamplesDir) && Directory.Exists(mapping.ExamplesDir))
        {
            var folderKey = NamingHelper.GetFolderKey(mapping.ExamplesDir);
            var exampleGroups = GetOrScanExamples(mapping.ExamplesDir);

            // Parse DemoSections from all pages in the examples parent directory
            var demoSections = GetOrParseDemoSections(mapping.ExamplesDir);

            // Build additional-file and service-split lookups
            var additionalFileOwners = BuildAdditionalFileOwners(demoSections);
            var groupsByBaseName = exampleGroups.ToDictionary(
                g => g.BaseName, g => g, StringComparer.OrdinalIgnoreCase);

            // Determine service split for this examples folder
            HashSet<string>? serviceOwned = null;
            string? sharingServiceName = null;
            if (isService)
            {
                serviceOwnedExamples.TryGetValue(componentName, out serviceOwned);
            }
            else if (TryGetSharingService(mapping.ExamplesDir, out var svcName))
            {
                serviceOwnedExamples.TryGetValue(svcName, out serviceOwned);
                sharingServiceName = svcName;
            }

            var seq = 1;
            foreach (var group in exampleGroups)
            {
                if (ShouldSkipExample(group.BaseName, isService, additionalFileOwners, serviceOwned, sharingServiceName))
                {
                    continue;
                }

                var section = demoSections.FirstOrDefault(s =>
                    s.TypeName.Equals(group.BaseName, StringComparison.OrdinalIgnoreCase));

                var codeFiles = MergeAdditionalFiles(group, section, groupsByBaseName);

                examples.Add(new ExampleUsageDoc
                {
                    ComponentName = componentName,
                    Title = section?.Title ?? group.BaseName,
                    Key = NamingHelper.MakeKey(folderKey, seq),
                    Summary = "",
                    CodeFiles = codeFiles,
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

// Step 5: Build parent → child relationships from source folder structure
var srcComponentsDir = Path.Combine(repoRoot, "src", "Core", "Components");
var relationships = ComponentReflector.BuildRelationships(srcComponentsDir);

var componentsPath = Path.Combine(outputDir, "FluentComponents.json");
var examplesPath = Path.Combine(outputDir, "FluentExampleUsageDoc.json");
var relPath = Path.Combine(outputDir, "FluentComponents_rel.json");

File.WriteAllText(componentsPath, JsonSerializer.Serialize(components, jsonOptions));
File.WriteAllText(examplesPath, JsonSerializer.Serialize(examples, jsonOptions));
File.WriteAllText(relPath, JsonSerializer.Serialize(
    new SortedDictionary<string, List<string>>(relationships, StringComparer.OrdinalIgnoreCase),
    jsonOptions));

Console.WriteLine($"\nOutput:");
Console.WriteLine($"  {componentsPath} ({components.Count} components)");
Console.WriteLine($"  {examplesPath} ({examples.Count} examples)");
Console.WriteLine($"  {relPath} ({relationships.Count} relationships)");

return 0;

// --- Helper functions ---

Dictionary<string, HashSet<string>> BuildServiceOwnedExamples(
    List<string> names,
    Dictionary<string, ComponentMapping> map)
{
    var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    foreach (var (serviceName, config) in FolderComponentMapper.ServiceMappings)
    {
        if (!map.TryGetValue(serviceName, out var svcMapping) ||
            string.IsNullOrEmpty(svcMapping.ExamplesDir))
        {
            continue;
        }

        var assignedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exampleGroups = GetOrScanExamples(svcMapping.ExamplesDir);

        foreach (var group in exampleGroups)
        {
            var usesService = group.CodeFiles.Any(f =>
                f.Code.Contains(config.InterfaceName, StringComparison.OrdinalIgnoreCase));
            if (usesService)
            {
                assignedNames.Add(group.BaseName);
            }
        }

        result[serviceName] = assignedNames;
    }

    return result;
}

bool TryGetSharingService(string examplesDir, out string serviceName)
{
    foreach (var (name, _) in FolderComponentMapper.ServiceMappings)
    {
        if (componentMap.TryGetValue(name, out var svcMapping) &&
            string.Equals(svcMapping.ExamplesDir, examplesDir, StringComparison.OrdinalIgnoreCase))
        {
            serviceName = name;
            return true;
        }
    }
    serviceName = "";
    return false;
}

static Dictionary<string, string> BuildAdditionalFileOwners(List<DemoSectionInfo> demoSections)
{
    var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var section in demoSections)
    {
        foreach (var addlFile in section.AdditionalFiles)
        {
            var baseName = ExampleScanner.GetBaseName(addlFile);
            owners.TryAdd(baseName, section.TypeName);
        }
    }
    return owners;
}

static bool ShouldSkipExample(
    string baseName,
    bool isService,
    Dictionary<string, string> additionalFileOwners,
    HashSet<string>? serviceOwned,
    string? sharingServiceName)
{
    // Skip files that are AdditionalFiles of another DemoSection
    if (additionalFileOwners.ContainsKey(baseName))
    {
        return true;
    }

    if (serviceOwned == null)
    {
        return false;
    }

    // Service: skip examples that don't use the service
    if (isService && !serviceOwned.Contains(baseName))
    {
        return true;
    }

    // Component: skip examples owned by the service
    if (!isService && sharingServiceName != null && serviceOwned.Contains(baseName))
    {
        return true;
    }

    return false;
}

static List<CodeFileInfo> MergeAdditionalFiles(
    ExampleGroup group,
    DemoSectionInfo? section,
    Dictionary<string, ExampleGroup> groupsByBaseName)
{
    var codeFiles = new List<CodeFileInfo>(group.CodeFiles);
    if (section == null)
    {
        return codeFiles;
    }

    var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var addlFile in section.AdditionalFiles)
    {
        var addlBaseName = ExampleScanner.GetBaseName(addlFile);
        if (merged.Add(addlBaseName) && groupsByBaseName.TryGetValue(addlBaseName, out var addlGroup))
        {
            codeFiles.AddRange(addlGroup.CodeFiles);
        }
    }

    return codeFiles;
}

static Dictionary<string, string> LoadXmlSummaries(string xmlPath)
{
    var summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if (!File.Exists(xmlPath))
    {
        return summaries;
    }

    var doc = XDocument.Load(xmlPath);

    foreach (var member in doc.Descendants("member"))
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
            summary = summary.Replace("\r\n", " ").Replace("\n", " ");
            summary = System.Text.RegularExpressions.Regex.Replace(summary, @"\s{2,}", " ");
            summary = System.Text.RegularExpressions.Regex.Replace(summary, @"<[^>]+>", "");
            summaries.TryAdd(typeName, summary.Trim());
        }
    }

    return summaries;
}
