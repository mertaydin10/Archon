using Archon.Analysis;
using Archon.Core;

namespace Archon.Analysis.Tests;

public sealed class SolutionAndRulesTests
{
    [Fact]
    public void Loader_reads_project_references_from_sln()
    {
        var root = CreateShop();
        try
        {
            var graph = new SolutionGraphLoader().Load(Path.Combine(root, "Shop.sln"));

            Assert.Contains(graph.Projects, p => p.Name == "Shop.Domain");
            Assert.Contains(graph.Edges, e => e.From == "Shop.Catalog" && e.To == "Shop.Payments");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Yaml_rules_and_sample_graph_produce_expected_violations()
    {
        var root = CreateShop();
        try
        {
            var rulesPath = Path.Combine(root, "archon.yaml");
            File.WriteAllText(rulesPath,
                """
                name: Shop
                rules:
                  - id: catalog-payments
                    kind: deny
                    from: "*Catalog*"
                    to: "*Payments*"
                  - id: no-cycles
                    kind: acyclic
                    description: döngü yok
                """);

            var ruleSet = new RuleSetLoader().Load(rulesPath);
            var graph = new SolutionGraphLoader().Load(Path.Combine(root, "Shop.sln"));
            var report = new ArchitectureAnalyzer().Analyze(graph, ruleSet, "Shop.sln");

            Assert.Contains(report.Violations, v => v.RuleId == "catalog-payments");
            Assert.Equal("Shop", ruleSet.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Yaml_parses_allow_and_namespace_deny()
    {
        var path = Path.Combine(Path.GetTempPath(), "archon-rules-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path,
            """
            name: Shop
            rules:
              - id: catalog-surface
                kind: allow
                from: "*Catalog*"
                to: "*.Domain"
              - id: no-infra-ns
                kind: namespace-deny
                from: "*.Domain"
                to: "Shop.Infrastructure*"
            """);
        try
        {
            var ruleSet = new RuleSetLoader().Load(path);
            Assert.Contains(ruleSet.Rules, r => r is AllowRule);
            Assert.Contains(ruleSet.Rules, r => r is NamespaceDenyRule);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Yaml_parses_package_deny()
    {
        var path = Path.Combine(Path.GetTempPath(), "archon-pkg-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path,
            """
            name: Shop
            rules:
              - id: domain-no-json
                kind: package-deny
                from: "*.Domain"
                packages:
                  - Newtonsoft.Json
            """);
        try
        {
            var ruleSet = new RuleSetLoader().Load(path);
            var rule = Assert.IsType<PackageDenyRule>(Assert.Single(ruleSet.Rules));
            Assert.Equal("Newtonsoft.Json", Assert.Single(rule.Packages));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Source_index_reads_using_directives()
    {
        var root = CreateShop();
        try
        {
            var file = Path.Combine(root, "src", "Shop.Catalog", "Leak.cs");
            File.WriteAllText(file,
                """
                using Shop.Payments;

                namespace Shop.Catalog;

                public sealed class Leak;
                """);
            var graph = new SolutionGraphLoader().Load(Path.Combine(root, "Shop.sln"));
            var index = new SourceIndexLoader().Load(graph);

            Assert.Contains(index.Imports, i =>
                i.ProjectName == "Shop.Catalog"
                && i.Namespace == "Shop.Payments"
                && i.Line == 1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Source_index_reads_inline_suppression()
    {
        var path = Path.Combine(Path.GetTempPath(), "archon-ignore-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(path, "using Shop.Infrastructure; // archon:ignore no-infra\n");
        try
        {
            var import = Assert.Single(SourceIndexLoader.ReadImports("Shop.Domain", path));
            Assert.Equal("no-infra", import.Suppression);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Markdown_report_contains_status()
    {
        var graph = ProjectGraph.Create(
            [new ProjectNode("A.Domain", "A.csproj", "net10.0")],
            []);
        var report = new AnalysisReport("Demo", "demo.sln", graph, []);
        var markdown = MarkdownReportWriter.Write(report);
        Assert.Contains("TEMİZ", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Sarif_report_contains_results()
    {
        var graph = ProjectGraph.Create(
            [new ProjectNode("Catalog", "Catalog.csproj", "net10.0"), new ProjectNode("Payments", "Payments.csproj", "net10.0")],
            [new ProjectEdge("Catalog", "Payments")]);
        var report = new AnalysisReport("Demo", Path.Combine(Path.GetTempPath(), "demo.sln"), graph,
        [
            new Violation("catalog-payments", RuleSeverity.Error, "yasak", "Catalog", "Payments")
        ]);

        var sarif = SarifReportWriter.Write(report);

        Assert.Contains("\"$schema\":", sarif, StringComparison.Ordinal);
        Assert.Contains("catalog-payments", sarif, StringComparison.Ordinal);
        Assert.Contains("\"level\": \"error\"", sarif, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_report_contains_status_and_graph()
    {
        var graph = ProjectGraph.Create(
            [new ProjectNode("A.Domain", "A.csproj", "net10.0"), new ProjectNode("B.Api", "B.csproj", "net10.0")],
            [new ProjectEdge("B.Api", "A.Domain")]);
        var report = new AnalysisReport("Demo", "demo.sln", graph, []);

        var html = HtmlReportWriter.Write(report);

        Assert.Contains("TEMİZ", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
        Assert.Contains("A.Domain", html, StringComparison.Ordinal);
    }

    private static string CreateShop()
    {
        var root = Path.Combine(Path.GetTempPath(), "archon-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Shop.Domain"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Shop.Payments"));
        Directory.CreateDirectory(Path.Combine(root, "src", "Shop.Catalog"));

        File.WriteAllText(Path.Combine(root, "src", "Shop.Domain", "Shop.Domain.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(Path.Combine(root, "src", "Shop.Payments", "Shop.Payments.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Shop.Domain\Shop.Domain.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "src", "Shop.Catalog", "Shop.Catalog.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Shop.Payments\Shop.Payments.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "Shop.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shop.Domain", "src\Shop.Domain\Shop.Domain.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shop.Payments", "src\Shop.Payments\Shop.Payments.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shop.Catalog", "src\Shop.Catalog\Shop.Catalog.csproj", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            """);

        return root;
    }
}
