namespace Archon.Core;

public static class ReportFilter
{
    public static AnalysisReport Only(AnalysisReport report, IReadOnlyList<string>? ruleGlobs)
    {
        if (ruleGlobs is null || ruleGlobs.Count == 0)
            return report;

        var remaining = report.Violations
            .Where(v => ruleGlobs.Any(glob => GlobPattern.IsMatch(glob, v.RuleId)))
            .ToArray();

        return report with { Violations = remaining };
    }
}

public sealed record ViolationDiff(
    IReadOnlyList<Violation> Added,
    IReadOnlyList<Violation> Removed);

public static class ViolationDiffCalculator
{
    public static ViolationDiff Compute(IEnumerable<Violation> previous, IEnumerable<Violation> current)
    {
        var previousMap = Index(previous);
        var currentMap = Index(current);

        var added = currentMap
            .Where(pair => !previousMap.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();
        var removed = previousMap
            .Where(pair => !currentMap.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .ToArray();

        return new ViolationDiff(added, removed);
    }

    private static Dictionary<string, Violation> Index(IEnumerable<Violation> violations)
    {
        var map = new Dictionary<string, Violation>(StringComparer.Ordinal);
        foreach (var violation in violations)
            map[ViolationKey.Of(violation)] = violation;
        return map;
    }
}
