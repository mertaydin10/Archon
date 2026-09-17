using System.Text.Json;
using Archon.Core;

namespace Archon.Analysis;

public static class SarifReportWriter
{
    public static string Write(AnalysisReport report)
    {
        var solutionDir = Path.GetDirectoryName(report.SolutionPath) ?? Environment.CurrentDirectory;
        var rules = report.Violations
            .GroupBy(v => v.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                id = g.Key,
                shortDescription = new { text = g.First().Message },
                defaultConfiguration = new
                {
                    level = g.Any(v => v.Severity == RuleSeverity.Error) ? "error" : "warning"
                }
            })
            .ToArray();

        var results = report.Violations.Select(v => new
        {
            ruleId = v.RuleId,
            level = v.Severity == RuleSeverity.Error ? "error" : "warning",
            message = new { text = v.Message },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri = ToUri(v, report.Graph, solutionDir) },
                        region = new { startLine = v.Line ?? 1 }
                    }
                }
            }
        }).ToArray();

        var payload = new
        {
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "Archon",
                            informationUri = "https://github.com/mertaydin10/Archon",
                            rules
                        }
                    },
                    results
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return json.Replace("\"schema\":", "\"$schema\":", StringComparison.Ordinal);
    }

    private static string ToUri(Violation violation, ProjectGraph graph, string solutionDir)
    {
        var path = violation.FilePath;
        if (string.IsNullOrWhiteSpace(path) && violation.From is not null && graph.Contains(violation.From))
            path = graph.Get(violation.From).Path;

        if (string.IsNullOrWhiteSpace(path))
            return Path.GetFileName(graph.Projects.FirstOrDefault()?.Path ?? "solution.sln");

        var relative = Path.GetRelativePath(solutionDir, path);
        return relative.Replace('\\', '/');
    }
}
