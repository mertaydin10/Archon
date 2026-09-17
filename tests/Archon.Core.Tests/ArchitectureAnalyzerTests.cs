using Archon.Core;

namespace Archon.Core.Tests;

public sealed class ArchitectureAnalyzerTests
{
    [Fact]
    public void Deny_rule_flags_forbidden_edge()
    {
        var graph = Graph(
            ["Catalog", "Payments", "Domain"],
            [("Catalog", "Payments"), ("Catalog", "Domain"), ("Payments", "Domain")]);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("catalog-payments", "Catalog Payments'ı göremez.", RuleSeverity.Error,
                ["*Catalog*"], ["*Payments*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("catalog-payments", hit.RuleId);
        Assert.Equal("Catalog", hit.From);
        Assert.Equal("Payments", hit.To);
    }

    [Fact]
    public void Exception_suppresses_deny()
    {
        var graph = Graph(["Catalog", "Payments"], [("Catalog", "Payments")]);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("catalog-payments", "hayır", RuleSeverity.Error,
                ["Catalog"], ["Payments"], [new RuleException("Catalog", "Payments")])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Layer_rule_blocks_inner_to_outer()
    {
        var graph = Graph(
            ["Shop.Domain", "Shop.Infrastructure"],
            [("Shop.Domain", "Shop.Infrastructure")]);
        var rules = new RuleSet("shop", null,
        [
            new LayerRule("layers", "katman", RuleSeverity.Error,
                ["*.Api", "*.Infrastructure", "*.Application", "*.Domain"])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        Assert.Contains(report.Violations, v => v.RuleId == "layers" && v.From == "Shop.Domain");
    }

    [Fact]
    public void Acyclic_rule_reports_normalized_cycle()
    {
        var graph = Graph(
            ["Domain", "Infrastructure", "Application"],
            [("Domain", "Infrastructure"), ("Infrastructure", "Application"), ("Application", "Domain")]);
        var rules = new RuleSet("shop", null,
        [
            new AcyclicRule("no-cycles", "döngü yasak", RuleSeverity.Error)
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");
        var hit = Assert.Single(report.Violations);
        Assert.Equal(3, hit.Cycle!.Count);
        Assert.Equal("Application", hit.Cycle[0]);
    }

    [Fact]
    public void Allow_rule_flags_unlisted_target()
    {
        var graph = Graph(
            ["Catalog", "Shop.Domain", "Payments"],
            [("Catalog", "Payments"), ("Catalog", "Shop.Domain")]);
        var rules = new RuleSet("shop", null,
        [
            new AllowRule("catalog-surface", "yalnızca Domain", RuleSeverity.Error,
                ["*Catalog*"], ["*.Domain"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("catalog-surface", hit.RuleId);
        Assert.Equal("Payments", hit.To);
    }

    [Fact]
    public void Namespace_deny_reports_file_and_line()
    {
        var graph = Graph(["Shop.Domain"], []);
        var sources = new SourceIndex(
        [
            new NamespaceImport("Shop.Domain", "Shop.Infrastructure", "Domain/Leak.cs", 3)
        ]);
        var rules = new RuleSet("shop", null,
        [
            new NamespaceDenyRule("no-infra", "import yasak", RuleSeverity.Error,
                ["*.Domain"], ["Shop.Infrastructure*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", sources);

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Domain/Leak.cs", hit.FilePath);
        Assert.Equal(3, hit.Line);
        Assert.Equal("Shop.Infrastructure", hit.To);
    }

    [Fact]
    public void Explainer_returns_related_violations()
    {
        var graph = Graph(
            ["Domain", "Infrastructure", "Api"],
            [("Domain", "Infrastructure"), ("Api", "Domain")]);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("domain-isolation", "hayır", RuleSeverity.Error,
                ["Domain"], ["Infrastructure"], [])
        ]);
        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");
        var explanation = ProjectExplainer.Explain(graph, "Domain", report.Violations);

        Assert.Equal(["Infrastructure"], explanation.Dependencies);
        Assert.Equal(["Api"], explanation.Dependents);
        Assert.Equal(["Api"], explanation.BlastRadius);
        Assert.Contains(explanation.RelatedViolations, v => v.RuleId == "domain-isolation");
    }

    [Fact]
    public void Transitive_dependents_follow_reverse_edges()
    {
        var graph = Graph(
            ["Domain", "Application", "Api"],
            [("Application", "Domain"), ("Api", "Application")]);

        Assert.Equal(["Api", "Application"], graph.TransitiveDependents("Domain"));
    }

    private static ProjectGraph Graph(
        IEnumerable<string> names,
        IEnumerable<(string From, string To)> edges)
    {
        return ProjectGraph.Create(
            names.Select(n => new ProjectNode(n, n + ".csproj", "net10.0")),
            edges.Select(e => new ProjectEdge(e.From, e.To)));
    }
}
