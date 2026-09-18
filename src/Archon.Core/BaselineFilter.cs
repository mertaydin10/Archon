namespace Archon.Core;

public static class ViolationKey
{
    public static string Of(Violation violation)
    {
        var cycle = violation.Cycle is { Count: > 0 }
            ? string.Join('>', violation.Cycle)
            : "";
        var file = (violation.FilePath ?? "").Replace('\\', '/');
        return string.Join(
            '\u001f',
            violation.RuleId,
            violation.From ?? "",
            violation.To ?? "",
            file,
            violation.Line?.ToString() ?? "",
            cycle);
    }
}

public static class BaselineFilter
{
    public static AnalysisReport Apply(AnalysisReport report, IReadOnlySet<string> knownKeys)
    {
        if (knownKeys.Count == 0)
            return report;

        var remaining = report.Violations
            .Where(v => !knownKeys.Contains(ViolationKey.Of(v)))
            .ToArray();

        return report with
        {
            Violations = remaining,
            BaselineSuppressed = report.Violations.Count - remaining.Length
        };
    }
}
