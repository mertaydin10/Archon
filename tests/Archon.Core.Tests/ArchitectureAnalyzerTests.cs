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

    [Fact]
    public void Must_depend_flags_missing_required_edge()
    {
        var graph = Graph(["Api", "Domain", "Catalog"], [("Api", "Catalog")]);
        var rules = new RuleSet("shop", null,
        [
            new MustDependRule("api-domain", "Api Domain'e bağlanmalı", RuleSeverity.Error,
                ["Api"], ["Domain"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Api", hit.From);
        Assert.Equal("Domain", hit.To);
    }

    [Fact]
    public void Max_fanin_flags_too_many_incoming_edges()
    {
        var graph = Graph(
            ["Domain", "A", "B", "C"],
            [("A", "Domain"), ("B", "Domain"), ("C", "Domain")]);
        var rules = new RuleSet("shop", null,
        [
            new MaxFaninRule("domain-fanin", "en fazla 2", RuleSeverity.Error, ["Domain"], 2)
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("3", hit.To);
    }

    [Fact]
    public void Max_depth_flags_long_simple_path()
    {
        var graph = Graph(
            ["Api", "App", "Domain", "Infra"],
            [("Api", "App"), ("App", "Domain"), ("Domain", "Infra")]);
        var rules = new RuleSet("shop", null,
        [
            new MaxDepthRule("api-depth", "en fazla 2", RuleSeverity.Error, ["Api"], 2)
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("3", hit.To);
    }

    [Fact]
    public void Tfm_aligned_flags_mixed_frameworks()
    {
        var graph = ProjectGraph.Create(
            [
                new ProjectNode("Api", "Api.csproj", "net8.0"),
                new ProjectNode("Domain", "Domain.csproj", "net10.0")
            ],
            []);
        var rules = new RuleSet("shop", null,
        [
            new TfmAlignedRule("tfm", "aynı TFM", RuleSeverity.Error, [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Contains("net8.0", hit.To);
        Assert.Contains("net10.0", hit.To);
    }

    [Fact]
    public void Only_filter_keeps_matching_rule_ids()
    {
        var graph = Graph(["Catalog", "Payments"], [("Catalog", "Payments")]);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("catalog-payments", "hayır", RuleSeverity.Error, ["Catalog"], ["Payments"], []),
            new AcyclicRule("no-cycles", "döngü yok", RuleSeverity.Error)
        ]);
        var full = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var filtered = ReportFilter.Only(full, ["catalog-*"]);

        var hit = Assert.Single(filtered.Violations);
        Assert.Equal("catalog-payments", hit.RuleId);
    }

    [Fact]
    public void Diff_reports_added_and_removed()
    {
        var previous = new[]
        {
            new Violation("old", RuleSeverity.Error, "", "A", "B")
        };
        var current = new[]
        {
            new Violation("new", RuleSeverity.Error, "", "C", "D")
        };

        var diff = ViolationDiffCalculator.Compute(previous, current);

        Assert.Equal("new", Assert.Single(diff.Added).RuleId);
        Assert.Equal("old", Assert.Single(diff.Removed).RuleId);
    }

    [Fact]
    public void Shortest_path_returns_bfs_route()
    {
        var graph = Graph(
            ["Catalog", "Domain", "Infrastructure", "Payments"],
            [("Catalog", "Domain"), ("Catalog", "Payments"), ("Payments", "Domain"), ("Domain", "Infrastructure")]);

        var path = graph.ShortestPath("Catalog", "Infrastructure");

        Assert.Equal(["Catalog", "Domain", "Infrastructure"], path);
    }

    [Fact]
    public void Transitive_deny_reports_reachability_path()
    {
        var graph = Graph(
            ["Catalog", "Domain", "Infrastructure"],
            [("Catalog", "Domain"), ("Domain", "Infrastructure")]);
        var rules = new RuleSet("shop", null,
        [
            new TransitiveDenyRule("no-reach", "ulaşma", RuleSeverity.Error,
                ["*Catalog*"], ["*Infrastructure*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Catalog", hit.From);
        Assert.Equal("Infrastructure", hit.To);
        Assert.Contains("Domain", hit.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Package_allow_flags_unlisted_package()
    {
        var graph = Graph(["Shop.Domain"], []);
        var packages = new PackageIndex(
        [
            new PackageReference("Shop.Domain", "Newtonsoft.Json", "13.0.3")
        ]);
        var rules = new RuleSet("shop", null,
        [
            new PackageAllowRule("allow", "yalnızca System", RuleSeverity.Error,
                ["*.Domain"], ["System.*"], [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln", packages: packages);

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Newtonsoft.Json", hit.To);
    }

    [Fact]
    public void Sdk_deny_flags_forbidden_sdk()
    {
        var graph = ProjectGraph.Create(
            [new ProjectNode("Payments", "Payments.csproj", "net10.0", "Microsoft.NET.Sdk.Web")],
            []);
        var rules = new RuleSet("shop", null,
        [
            new SdkDenyRule("no-web", "web yok", RuleSeverity.Error, ["*Payments*"], ["*.Web"])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Microsoft.NET.Sdk.Web", hit.To);
    }

    [Fact]
    public void Naming_flags_projects_outside_patterns()
    {
        var graph = Graph(["Contoso.Api", "LegacyReports"], []);
        var rules = new RuleSet("shop", null,
        [
            new NamingRule("naming", "önek", RuleSeverity.Error, [], ["Contoso.*"])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("LegacyReports", hit.From);
    }

    [Fact]
    public void No_orphans_skips_entry_points_and_tests()
    {
        var graph = ProjectGraph.Create(
            [
                new ProjectNode("Cli", "Cli.csproj", "net10.0", "Microsoft.NET.Sdk", "Exe"),
                new ProjectNode("Web", "Web.csproj", "net10.0", "Microsoft.NET.Sdk.Web"),
                new ProjectNode("Core.Tests", "Core.Tests.csproj", "net10.0"),
                new ProjectNode("Core", "Core.csproj", "net10.0"),
                new ProjectNode("Legacy", "Legacy.csproj", "net10.0")
            ],
            [new ProjectEdge("Cli", "Core"), new ProjectEdge("Core.Tests", "Core")]);
        var rules = new RuleSet("shop", null,
        [
            new NoOrphansRule("orphans", "sahipsiz", RuleSeverity.Error, [])
        ]);

        var report = new ArchitectureAnalyzer().Analyze(graph, rules, "shop.sln");

        var hit = Assert.Single(report.Violations);
        Assert.Equal("Legacy", hit.From);
    }

    [Fact]
    public void Rule_coverage_reports_patterns_matching_no_project()
    {
        var graph = Graph(["Shop.Api", "Shop.Domain"], []);
        var rules = new RuleSet("shop", null,
        [
            new DenyRule("typo", "yazım hatası", RuleSeverity.Error, ["*.Domian"], ["*.Api"], []),
            new LayerRule("layers", "katman", RuleSeverity.Error, ["*.Api", "*.Domain"])
        ]);

        var unmatched = RuleCoverage.FindUnmatched(graph, rules);

        var item = Assert.Single(unmatched);
        Assert.Equal("typo", item.RuleId);
        Assert.Equal("from", item.Field);
        Assert.Equal("*.Domian", item.Pattern);
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
