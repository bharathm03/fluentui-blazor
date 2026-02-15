using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DataExtractor.Extractors;

public static class ComponentReflector
{
    /// <summary>
    /// Child, deprecated, and internal components excluded from the top-level catalog.
    /// </summary>
    private static readonly HashSet<string> ExcludedComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        // Child components (only used inside a parent)
        "FluentAccordionItem",
        "FluentAppBarItem",
        "FluentBreadcrumbItem",
        "FluentDataGridCell",
        "FluentDataGridRow",
        "FluentDialogBody",
        "FluentDialogFooter",
        "FluentDialogHeader",
        "FluentDropZone",
        "FluentGridItem",
        "FluentMenuItem",
        "FluentMultiSplitterPane",
        "FluentNavGroup",
        "FluentNavLink",
        "FluentOption",
        "FluentOverflowItem",
        "FluentRadio",
        "FluentSliderLabel",
        "FluentTab",
        "FluentToast",
        "FluentTreeItem",
        "FluentWizardStep",

        // Deprecated (replaced by FluentNavMenu)
        "FluentNavMenuTree",
        "FluentNavMenuGroup",
        "FluentNavMenuLink",

        // Internal utility
        "FluentPageScript",
    };

    public static List<string> GetFluentComponentNames()
    {
        var assembly = typeof(FluentComponentBase).Assembly;
        var names = new HashSet<string>();

        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || !type.IsClass)
            {
                continue;
            }

            if (!type.IsAssignableTo(typeof(ComponentBase)))
            {
                continue;
            }

            var name = type.Name;

            // Strip generic backtick suffix
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex >= 0)
            {
                name = name[..backtickIndex];
            }

            if (!name.StartsWith("Fluent") || name.EndsWith("Provider"))
            {
                continue;
            }

            if (ExcludedComponents.Contains(name))
            {
                continue;
            }

            names.Add(name);
        }

        // Add service classes (not Blazor components, but important for code generation)
        foreach (var serviceName in FolderComponentMapper.ServiceMappings.Keys)
        {
            names.Add(serviceName);
        }

        return names.OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Builds parent → child component relationships by grouping components
    /// that share the same source folder under src/Core/Components/.
    /// </summary>
    public static Dictionary<string, List<string>> BuildRelationships(string srcComponentsDir)
    {
        var razorFiles = Directory.GetFiles(srcComponentsDir, "*.razor.cs", SearchOption.AllDirectories);

        // Group Fluent* component names by their containing folder
        var byFolder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in razorFiles)
        {
            var componentName = Path.GetFileName(file).Replace(".razor.cs", "");
            if (!componentName.StartsWith("Fluent"))
            {
                continue;
            }

            var dir = Path.GetDirectoryName(file)!;
            if (!byFolder.TryGetValue(dir, out var list))
            {
                list = new List<string>();
                byFolder[dir] = list;
            }
            list.Add(componentName);
        }

        // False positives from folder co-location (parent is not actually the owner)
        var excludeRelationships = new HashSet<(string Parent, string Child)>(
        [
            ("FluentPersona", "FluentOption"),           // Persona is a renderer, not a parent of Option
            ("FluentSplitter", "FluentMultiSplitterPane"), // Pane belongs to MultiSplitter, not legacy Splitter
        ]);

        // For each folder, map non-excluded parents to excluded children
        var relationships = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, components) in byFolder)
        {
            var parents = components.Where(c => !ExcludedComponents.Contains(c) && !c.EndsWith("Provider")).ToList();
            var children = components.Where(c => ExcludedComponents.Contains(c)).ToList();

            if (parents.Count == 0 || children.Count == 0)
            {
                continue;
            }

            foreach (var parent in parents)
            {
                var ownedChildren = children
                    .Where(c => !excludeRelationships.Contains((parent, c)))
                    .OrderBy(c => c)
                    .ToList();

                if (ownedChildren.Count > 0)
                {
                    relationships[parent] = ownedChildren;
                }
            }
        }

        return relationships;
    }
}
