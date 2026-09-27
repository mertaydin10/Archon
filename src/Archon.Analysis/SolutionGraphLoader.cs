using System.Text.RegularExpressions;
using System.Xml.Linq;
using Archon.Core;

namespace Archon.Analysis;

public sealed class SolutionGraphLoader
{
    private static readonly Regex ProjectLine = new(
        @"^Project\(""[^""]+""\)\s*=\s*""([^""]+)"",\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ProjectGraph Load(string solutionPath)
    {
        var fullSolution = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullSolution))
            throw new FileNotFoundException("Solution file was not found.", fullSolution);

        var solutionDir = Path.GetDirectoryName(fullSolution) ?? Environment.CurrentDirectory;
        var nodes = new List<ProjectNode>();
        var pathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, rawRelative) in ReadSolutionEntries(fullSolution))
        {
            var relative = rawRelative.Replace('\\', Path.DirectorySeparatorChar);
            if (!relative.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                && !relative.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)
                && !relative.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var projectPath = Path.GetFullPath(Path.Combine(solutionDir, relative));
            pathByName[name] = projectPath;
            var facts = ReadProjectFacts(projectPath);
            nodes.Add(new ProjectNode(name, projectPath, facts.TargetFramework, facts.Sdk, facts.OutputType));
        }

        var pathToName = pathByName
            .GroupBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        var edges = new List<ProjectEdge>();
        foreach (var (name, projectPath) in pathByName)
        {
            foreach (var referencePath in ReadProjectReferences(projectPath))
            {
                if (!pathToName.TryGetValue(referencePath, out var toName))
                    toName = Path.GetFileNameWithoutExtension(referencePath);
                edges.Add(new ProjectEdge(name, toName));
            }
        }

        return ProjectGraph.Create(nodes, edges);
    }

    public static IReadOnlyList<(string Name, string RelativePath)> ReadSolutionEntries(string solutionPath)
    {
        if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            return ReadSlnxEntries(solutionPath);

        var entries = new List<(string, string)>();
        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = ProjectLine.Match(line);
            if (match.Success)
                entries.Add((match.Groups[1].Value, match.Groups[2].Value));
        }

        return entries;
    }

    private static IReadOnlyList<(string Name, string RelativePath)> ReadSlnxEntries(string solutionPath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(solutionPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse '{solutionPath}'.", ex);
        }

        return document.Descendants("Project")
            .Select(x => x.Attribute("Path")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => (Path.GetFileNameWithoutExtension(x!.Replace('\\', '/')), x!))
            .ToArray();
    }

    private static IEnumerable<string> ReadProjectReferences(string projectPath)
    {
        if (!File.Exists(projectPath))
            yield break;

        var projectDir = Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory;
        XDocument document;
        try
        {
            document = XDocument.Load(projectPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse '{projectPath}'.", ex);
        }

        foreach (var include in document.Descendants("ProjectReference").Select(x => x.Attribute("Include")?.Value))
        {
            if (string.IsNullOrWhiteSpace(include))
                continue;

            var relative = include.Replace('\\', Path.DirectorySeparatorChar);
            yield return Path.GetFullPath(Path.Combine(projectDir, relative));
        }
    }

    public static string? ReadTargetFramework(string projectPath) =>
        ReadProjectFacts(projectPath).TargetFramework;

    public static string? ReadSdk(string projectPath) =>
        ReadProjectFacts(projectPath).Sdk;

    public static (string? TargetFramework, string? Sdk, string? OutputType) ReadProjectFacts(string projectPath)
    {
        if (!File.Exists(projectPath))
            return (null, null, null);

        try
        {
            var document = XDocument.Load(projectPath);
            var framework = ReadFramework(document)
                ?? ReadInheritedTargetFramework(Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory);
            var sdk = document.Root?.Attribute("Sdk")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(sdk))
                sdk = document.Descendants("Sdk").Select(x => x.Attribute("Name")?.Value).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            var output = document.Descendants("OutputType").Select(x => x.Value).FirstOrDefault();
            return (
                framework,
                string.IsNullOrWhiteSpace(sdk) ? null : sdk.Trim(),
                string.IsNullOrWhiteSpace(output) ? null : output.Trim());
        }
        catch (Exception)
        {
            return (null, null, null);
        }
    }

    public static string? ReadInheritedTargetFramework(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var props = Path.Combine(directory.FullName, "Directory.Build.props");
            if (File.Exists(props))
            {
                try
                {
                    var document = XDocument.Load(props);
                    var framework = ReadFramework(document);
                    if (!string.IsNullOrWhiteSpace(framework))
                        return framework;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? ReadFramework(XDocument document)
    {
        var single = document.Descendants("TargetFramework").Select(x => x.Value).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(single))
            return single.Trim();

        var many = document.Descendants("TargetFrameworks").Select(x => x.Value).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(many))
            return null;

        return many.Split(';').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);
    }
}
