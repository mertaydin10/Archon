namespace Archon.Core;

public enum RuleSeverity
{
    Error,
    Warning
}

public sealed record RuleException(string From, string To);

public abstract record ArchitectureRule(string Id, string Description, RuleSeverity Severity);

public sealed record DenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record LayerRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> Layers) : ArchitectureRule(Id, Description, Severity);

public sealed record AcyclicRule(
    string Id,
    string Description,
    RuleSeverity Severity) : ArchitectureRule(Id, Description, Severity);

public sealed record NamespaceDenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record AllowRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record PackageDenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> Packages,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record MaxFanoutRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    int Max) : ArchitectureRule(Id, Description, Severity);

public sealed record StableDependencyRule(
    string Id,
    string Description,
    RuleSeverity Severity) : ArchitectureRule(Id, Description, Severity);

public sealed record RuleSet(
    string Name,
    string? SolutionPath,
    IReadOnlyList<ArchitectureRule> Rules);
