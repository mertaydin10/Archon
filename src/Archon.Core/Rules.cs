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

public sealed record IsolatedRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record VersionAlignedRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> Packages) : ArchitectureRule(Id, Description, Severity);

public sealed record InternalsDenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record MustDependRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record MaxFaninRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    int Max) : ArchitectureRule(Id, Description, Severity);

public sealed record MaxDepthRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    int Max) : ArchitectureRule(Id, Description, Severity);

public sealed record TfmAlignedRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From) : ArchitectureRule(Id, Description, Severity);

public sealed record TransitiveDenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> To,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record PackageAllowRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> Packages,
    IReadOnlyList<RuleException> Exceptions) : ArchitectureRule(Id, Description, Severity);

public sealed record SdkDenyRule(
    string Id,
    string Description,
    RuleSeverity Severity,
    IReadOnlyList<string> From,
    IReadOnlyList<string> Sdks) : ArchitectureRule(Id, Description, Severity);

public sealed record RuleSet(
    string Name,
    string? SolutionPath,
    IReadOnlyList<ArchitectureRule> Rules,
    IReadOnlyList<string>? Exclude = null)
{
    public IReadOnlyList<string> ExcludedProjects => Exclude ?? [];
}
