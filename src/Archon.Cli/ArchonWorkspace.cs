using Archon.Analysis;
using Archon.Core;

namespace Archon.Cli;

internal sealed record LoadedWorkspace(
    RuleSet RuleSet,
    string SolutionPath,
    ProjectGraph Graph,
    SourceIndex Sources,
    PackageIndex Packages);

internal static class ArchonWorkspace
{
    public static LoadedWorkspace Load(string? path, string? rules)
    {
        var rulesPath = ResolveRulesPath(rules);
        var ruleSet = File.Exists(rulesPath)
            ? new RuleSetLoader().Load(rulesPath)
            : new RuleSet("Archon", null, [new AcyclicRule("no-cycles", "Proje grafı döngü içeremez.", RuleSeverity.Warning)]);

        var solutionPath = ResolveSolutionPath(path, ruleSet);
        var graph = new SolutionGraphLoader().Load(solutionPath);
        var sources = new SourceIndexLoader().Load(graph);
        var packages = new PackageIndexLoader().Load(graph);
        return new LoadedWorkspace(ruleSet, solutionPath, graph, sources, packages);
    }

    public static string ResolveRulesPath(string? rules)
    {
        if (!string.IsNullOrWhiteSpace(rules))
            return Path.GetFullPath(rules);

        return Path.GetFullPath("archon.yaml");
    }

    public static string ResolveSolutionPath(string? path, RuleSet ruleSet)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full))
            {
                var solutions = Directory.GetFiles(full, "*.sln", SearchOption.TopDirectoryOnly);
                if (solutions.Length == 1)
                    return solutions[0];
                if (solutions.Length == 0)
                    throw new InvalidOperationException($"'{full}' içinde .sln yok.");
                throw new InvalidOperationException($"'{full}' içinde birden fazla .sln var. Dosyayı açıkça ver.");
            }

            return full;
        }

        if (!string.IsNullOrWhiteSpace(ruleSet.SolutionPath))
            return ruleSet.SolutionPath;

        throw new InvalidOperationException("Solution yolu verilmedi. analyze <dosya.sln> veya archon.yaml içine solution yaz.");
    }
}
