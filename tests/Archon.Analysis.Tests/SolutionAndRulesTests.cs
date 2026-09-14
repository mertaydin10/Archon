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
