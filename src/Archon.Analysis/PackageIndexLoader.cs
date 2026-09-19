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

            var central = ReadCentralVersions(Path.GetDirectoryName(project.Path) ?? Environment.CurrentDirectory);
            packages.AddRange(ReadPackages(project.Name, project.Path, central));
        }

        return new PackageIndex(packages);
    }

    public static IEnumerable<PackageReference> ReadPackages(
        string projectName,
        string projectPath,
        IReadOnlyDictionary<string, string>? centralVersions = null)
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
            if (string.IsNullOrWhiteSpace(version))
                centralVersions?.TryGetValue(id.Trim(), out version);

            yield return new PackageReference(
                projectName,
                id.Trim(),
                string.IsNullOrWhiteSpace(version) ? null : version.Trim());
        }
    }

    public static IReadOnlyDictionary<string, string> ReadCentralVersions(string startDirectory)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var props = Path.Combine(directory.FullName, "Directory.Packages.props");
            if (File.Exists(props))
            {
                try
                {
                    var document = XDocument.Load(props);
                    foreach (var element in document.Descendants("PackageVersion"))
                    {
                        var id = element.Attribute("Include")?.Value;
                        var version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value;
                        if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version))
                            versions[id.Trim()] = version.Trim();
                    }
                }
                catch (Exception)
                {
                    return versions;
                }

                return versions;
            }

            directory = directory.Parent;
        }

        return versions;
    }
}
