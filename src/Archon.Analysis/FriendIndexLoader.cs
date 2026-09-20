using System.Text.RegularExpressions;
using System.Xml.Linq;
using Archon.Core;

namespace Archon.Analysis;

public sealed class FriendIndexLoader
{
    private static readonly Regex AssemblyAttribute = new(
        @"InternalsVisibleTo\s*\(\s*""([^""]+)""\s*\)",
        RegexOptions.CultureInvariant);

    public FriendIndex Load(ProjectGraph graph)
    {
        var friends = new List<FriendAssembly>();
        foreach (var project in graph.Projects)
        {
            if (!File.Exists(project.Path))
                continue;

            friends.AddRange(ReadFromProject(project.Name, project.Path));
            var root = Path.GetDirectoryName(project.Path);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    continue;
                friends.AddRange(ReadFromSource(project.Name, file));
            }
        }

        return new FriendIndex(friends);
    }

    public static IEnumerable<FriendAssembly> ReadFromProject(string projectName, string projectPath)
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

        foreach (var include in document.Descendants("InternalsVisibleTo").Select(x => x.Attribute("Include")?.Value ?? x.Value))
        {
            if (string.IsNullOrWhiteSpace(include))
                continue;
            yield return new FriendAssembly(projectName, include.Trim(), projectPath);
        }
    }

    public static IEnumerable<FriendAssembly> ReadFromSource(string projectName, string filePath)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(filePath))
        {
            lineNumber++;
            var match = AssemblyAttribute.Match(line);
            if (!match.Success)
                continue;
            yield return new FriendAssembly(projectName, match.Groups[1].Value, filePath, lineNumber);
        }
    }
}
