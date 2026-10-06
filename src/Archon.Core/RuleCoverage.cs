namespace Archon.Core;

public sealed record UnmatchedPattern(string RuleId, string Field, string Pattern);

public static class RuleCoverage
{
    public static IReadOnlyList<UnmatchedPattern> FindUnmatched(ProjectGraph graph, RuleSet ruleSet)
    {
        var names = graph.Projects.Select(p => p.Name).ToArray();
        var unmatched = new List<UnmatchedPattern>();

        foreach (var rule in ruleSet.Rules)
        {
            foreach (var (field, pattern) in ProjectPatterns(rule))
            {
                if (!names.Any(name => GlobPattern.IsMatch(pattern, name)))
                    unmatched.Add(new UnmatchedPattern(rule.Id, field, pattern));
            }
        }

        return unmatched;
    }

    private static IEnumerable<(string Field, string Pattern)> ProjectPatterns(ArchitectureRule rule) =>
        rule switch
        {
            DenyRule r => Tag("from", r.From).Concat(Tag("to", r.To)),
            AllowRule r => Tag("from", r.From),
            IsolatedRule r => Tag("from", r.From).Concat(Tag("to", r.To)),
            MustDependRule r => Tag("from", r.From).Concat(Tag("to", r.To)),
            TransitiveDenyRule r => Tag("from", r.From).Concat(Tag("to", r.To)),
            LayerRule r => Tag("layers", r.Layers),
            NamespaceDenyRule r => Tag("from", r.From),
            PackageDenyRule r => Tag("from", r.From),
            PackageAllowRule r => Tag("from", r.From),
            InternalsDenyRule r => Tag("from", r.From),
            MaxFanoutRule r => Tag("from", r.From),
            MaxFaninRule r => Tag("from", r.From),
            MaxDepthRule r => Tag("from", r.From),
            SdkDenyRule r => Tag("from", r.From),
            TfmAlignedRule r => Tag("from", r.From),
            NamingRule r => Tag("from", r.From),
            NoOrphansRule r => Tag("from", r.From),
            PackageMinVersionRule r => Tag("from", r.From),
            TestIsolationRule r => Tag("from", r.From).Concat(Tag("patterns", r.Patterns)),
            _ => []
        };

    private static IEnumerable<(string, string)> Tag(string field, IReadOnlyList<string> patterns) =>
        patterns.Select(pattern => (field, pattern));
}
