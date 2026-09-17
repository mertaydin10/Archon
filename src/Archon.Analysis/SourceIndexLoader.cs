using System.Text.RegularExpressions;
using Archon.Core;

namespace Archon.Analysis;

public sealed class SourceIndexLoader
{
    private static readonly Regex UsingDirective = new(
        @"^\s*(?:global\s+)?using\s+(?:static\s+)?(?:[A-Za-z_][\w.]*\s*=\s*)?([A-Za-z_][\w.]*)\s*;",
        RegexOptions.CultureInvariant);

    public SourceIndex Load(ProjectGraph graph)
    {
        var imports = new List<NamespaceImport>();
        foreach (var project in graph.Projects)
        {
            if (!File.Exists(project.Path))
                continue;

            var root = Path.GetDirectoryName(project.Path);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedOrOutput(file, root))
                    continue;

                imports.AddRange(ReadImports(project.Name, file));
            }
        }

        return new SourceIndex(imports);
    }

    internal static IEnumerable<NamespaceImport> ReadImports(string projectName, string filePath)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(filePath))
        {
            lineNumber++;
            var match = UsingDirective.Match(line);
            if (!match.Success)
                continue;

            yield return new NamespaceImport(projectName, match.Groups[1].Value, filePath, lineNumber);
        }
    }

    private static bool IsGeneratedOrOutput(string file, string projectRoot)
    {
        var relative = Path.GetRelativePath(projectRoot, file);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part =>
            part.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || part.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }
}
