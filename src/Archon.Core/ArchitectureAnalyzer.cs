namespace Archon.Core;

public sealed class ArchitectureAnalyzer
{
    public AnalysisReport Analyze(ProjectGraph graph, RuleSet ruleSet, string solutionPath)
    {
        var violations = new List<Violation>();
        foreach (var rule in ruleSet.Rules)
        {
            switch (rule)
            {
                case DenyRule deny:
                    violations.AddRange(EvaluateDeny(graph, deny));
                    break;
                case LayerRule layers:
                    violations.AddRange(EvaluateLayers(graph, layers));
                    break;
                case AcyclicRule acyclic:
                    violations.AddRange(EvaluateCycles(graph, acyclic));
                    break;
            }
        }

        return new AnalysisReport(
            ruleSet.Name,
            solutionPath,
            graph,
            violations
                .OrderBy(v => v.Severity)
                .ThenBy(v => v.RuleId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.From, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.To, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<Violation> EvaluateDeny(ProjectGraph graph, DenyRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            if (!MatchesAny(rule.From, edge.From) || !MatchesAny(rule.To, edge.To))
                continue;
            if (IsExcepted(rule.Exceptions, edge.From, edge.To))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                rule.Description,
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateLayers(ProjectGraph graph, LayerRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            var fromLayer = LayerIndex(rule.Layers, edge.From);
            var toLayer = LayerIndex(rule.Layers, edge.To);
            if (fromLayer is null || toLayer is null)
                continue;
            if (fromLayer.Value <= toLayer.Value)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} İç katman dışa bağlanamaz: {edge.From} ({rule.Layers[fromLayer.Value]}) → {edge.To} ({rule.Layers[toLayer.Value]})",
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateCycles(ProjectGraph graph, AcyclicRule rule)
    {
        foreach (var cycle in graph.FindCycles())
        {
            var path = string.Join(" → ", cycle.Append(cycle[0]));
            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} Döngü: {path}",
                cycle[0],
                cycle[^1],
                cycle);
        }
    }

    private static bool MatchesAny(IReadOnlyList<string> patterns, string value) =>
        patterns.Any(pattern => GlobPattern.IsMatch(pattern, value));

    private static bool IsExcepted(IReadOnlyList<RuleException> exceptions, string from, string to) =>
        exceptions.Any(ex => GlobPattern.IsMatch(ex.From, from) && GlobPattern.IsMatch(ex.To, to));

    private static int? LayerIndex(IReadOnlyList<string> layers, string projectName)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (GlobPattern.IsMatch(layers[i], projectName))
                return i;
        }

        return null;
    }
}
