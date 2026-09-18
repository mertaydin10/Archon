namespace Archon.Core;

public sealed record Violation(
    string RuleId,
    RuleSeverity Severity,
    string Message,
    string? From,
    string? To,
    IReadOnlyList<string>? Cycle = null,
    string? FilePath = null,
    int? Line = null);

public sealed record AnalysisReport(
    string RuleSetName,
    string SolutionPath,
    ProjectGraph Graph,
    IReadOnlyList<Violation> Violations,
    int BaselineSuppressed = 0)
{
    public int ErrorCount => Violations.Count(v => v.Severity == RuleSeverity.Error);
    public int WarningCount => Violations.Count(v => v.Severity == RuleSeverity.Warning);
    public bool HasErrors => ErrorCount > 0;

    public bool ShouldFail(bool failOnWarning) =>
        HasErrors || (failOnWarning && WarningCount > 0);
}
