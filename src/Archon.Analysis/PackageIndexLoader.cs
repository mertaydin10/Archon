using System.Xml.Linq;
using Archon.Core;

namespace Archon.Analysis;

public sealed class PackageIndexLoader
{
    public PackageIndex Load(ProjectGraph graph)
    {
        var packages = new List<PackageReference>();
        foreach (var project in graph.Projects)
        {
            if (!File.Exists(project.Path))
                continue;

            packages.AddRange(ReadPackages(project.Name, project.Path));
        }

        return new PackageIndex(packages);
    }

    internal static IEnumerable<PackageReference> ReadPackages(string projectName, string projectPath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(projectPath);
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var element in document.Descendants("PackageReference"))
        {
            var id = element.Attribute("Include")?.Value ?? element.Element("Include")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value;
            yield return new PackageReference(projectName, id.Trim(), string.IsNullOrWhiteSpace(version) ? null : version.Trim());
        }
    }
}
