using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace DataExtractor.Extractors;

public static class ComponentReflector
{
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

            if (!name.StartsWith("Fluent"))
            {
                continue;
            }

            names.Add(name);
        }

        return names.OrderBy(n => n).ToList();
    }
}
