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
    public void Package_deny_flags_forbidden_nuget()
    {
        var graph = Graph(["Shop.Domain"], []);
        var packages = new PackageIndex(
        [
            new PackageReference("Shop.Domain", "Newtonsoft.Json", "13.0.3")
        ]);
        var rules = new RuleSet("shop", null,
        [
            new PackageDenyRule("no-json", "json yasak", RuleSeverity.Error,
                ["*.Domain"], ["Newtonsoft.Json"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", packages: packages);

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Newtonsoft.Json", hit.To);
    }

    [Fact]
    public void Namespace_suppression_skips_matching_rule()
    {
        var graph = Graph(["Shop.Domain"], []);
        var sources = new SourceIndex(
        [
            new NamespaceImport("Shop.Domain", "Shop.Infrastructure", "Domain/Leak.cs", 1, "no-infra")
        ]);
        var rules = new RuleSet("shop", null,
        [
            new NamespaceDenyRule("no-infra", "import yasak", RuleSeverity.Error,
                ["*.Domain"], ["Shop.Infrastructure*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", sources);

        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Baseline_hides_known_violations()
    {
        var graph = Graph(["Catalog", "Payments"], [("Catalog", "Payments")]);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("catalog-payments", "hayır", RuleSeverity.Error,
                ["Catalog"], ["Payments"], [])
        ]);
        var full = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");
        var known = new HashSet<string> { ViolationKey.Of(full.Violations[0]) };

        var filtered = BaselineFilter.Apply(full, known);

        Assert.Empty(filtered.Violations);
        Assert.Equal(1, filtered.BaselineSuppressed);
        Assert.False(filtered.ShouldFail(false));
    }

    [Fact]
    public void Transitive_dependents_follow_reverse_edges()
    {
        var graph = Graph(
            ["Domain", "Application", "Api"],
            [("Application", "Domain"), ("Api", "Application")]);

        Assert.Equal(["Api", "Application"], graph.TransitiveDependents("Domain"));
    }

    [Fact]
    public void Max_fanout_flags_too_many_outgoing_edges()
    {
        var graph = Graph(
            ["Api", "A", "B", "C"],
            [("Api", "A"), ("Api", "B"), ("Api", "C")]);
        var rules = new RuleSet("shop", null,
        [
            new MaxFanoutRule("api-fanout", "en fazla 2", RuleSeverity.Error, ["Api"], 2)
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("api-fanout", hit.RuleId);
        Assert.Equal("3", hit.To);
    }

    [Fact]
    public void Sdp_flags_stable_depending_on_unstable()
    {
        var graph = Graph(
            ["Stable", "Unstable", "A", "B", "C", "X", "Y"],
            [
                ("Stable", "Unstable"),
                ("A", "Stable"),
                ("B", "Stable"),
                ("C", "Stable"),
                ("Unstable", "X"),
                ("Unstable", "Y")
            ]);
        var rules = new RuleSet("shop", null,
        [
            new StableDependencyRule("sdp", "SDP", RuleSeverity.Error)
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        Assert.Contains(report.Violations, v => v.From == "Stable" && v.To == "Unstable");
    }

    [Fact]
    public void Coupling_metrics_count_afferent_and_efferent()
    {
        var graph = Graph(
            ["Api", "Domain"],
            [("Api", "Domain")]);
        var domain = CouplingCalculator.Of(graph, "Domain");
        var api = CouplingCalculator.Of(graph, "Api");

        Assert.Equal(0, domain.Ce);
        Assert.Equal(1, domain.Ca);
        Assert.Equal(0, domain.Instability);
        Assert.Equal(1, api.Ce);
        Assert.Equal(0, api.Ca);
        Assert.Equal(1, api.Instability);
    }

    [Fact]
    public void Isolated_rule_flags_both_directions()
    {
        var graph = Graph(
            ["Catalog", "Payments"],
            [("Catalog", "Payments")]);
        var rules = new RuleSet("shop", null,
        [
            new IsolatedRule("iso", "ayrı bağlam", RuleSeverity.Error,
                ["*Catalog*"], ["*Payments*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("iso", hit.RuleId);
        Assert.Equal("Catalog", hit.From);
        Assert.Equal("Payments", hit.To);
    }

    [Fact]
    public void Version_aligned_flags_drift()
    {
        var graph = Graph(["Catalog", "Domain"], []);
        var packages = new PackageIndex(
        [
            new PackageReference("Catalog", "Newtonsoft.Json", "12.0.3"),
            new PackageReference("Domain", "Newtonsoft.Json", "13.0.3")
        ]);
        var rules = new RuleSet("shop", null,
        [
            new VersionAlignedRule("aligned", "sürüm hizalı", RuleSeverity.Error, ["Newtonsoft.Json"])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", packages: packages);

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Newtonsoft.Json", hit.From);
        Assert.Contains("12.0.3", hit.To);
        Assert.Contains("13.0.3", hit.To);
    }

    [Fact]
    public void Internals_deny_flags_friend_assembly()
    {
        var graph = Graph(["Payments", "Catalog"], []);
        var friends = new FriendIndex(
        [
            new FriendAssembly("Payments", "Contoso.Catalog", "Payments.csproj")
        ]);
        var rules = new RuleSet("shop", null,
        [
            new InternalsDenyRule("no-friends", "dostluk yasak", RuleSeverity.Error,
                ["*Payments*"], ["*Catalog*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", friends: friends);

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Payments", hit.From);
        Assert.Equal("Contoso.Catalog", hit.To);
        Assert.Equal("Payments.csproj", hit.FilePath);
    }

    [Fact]
    public void Exclude_drops_matching_projects_and_edges()
    {
        var graph = Graph(
            ["Api", "Tests", "Domain"],
            [("Api", "Domain"), ("Tests", "Api")]);

        var filtered = graph.Exclude(["*Tests*"]);

        Assert.DoesNotContain(filtered.Projects, p => p.Name == "Tests");
        Assert.DoesNotContain(filtered.Edges, e => e.From == "Tests");
        Assert.Contains(filtered.Edges, e => e.From == "Api" && e.To == "Domain");
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
