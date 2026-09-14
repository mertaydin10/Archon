namespace Archon.Core;

public sealed record Violation(
    string RuleId,
    RuleSeverity Severity,
    string Message,
    string? From,
    string? To,
    IReadOnlyList<string>? Cycle = null);

public sealed record AnalysisReport(
    string RuleSetName,
    string SolutionPath,
    ProjectGraph Graph,
    IReadOnlyList<Violation> Violations)
{
    public int ErrorCount => Violations.Count(v => v.Severity == RuleSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == RuleSeverity.Warning);
    public bool HasErrors => ErrorCount > 0;
}
