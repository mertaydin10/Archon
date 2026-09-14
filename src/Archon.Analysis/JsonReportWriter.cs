using System.Text.Json;
using System.Text.Json.Serialization;
using Archon.Core;

namespace Archon.Analysis;

public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Write(AnalysisReport report)
    {
        var payload = new
        {
            name = report.RuleSetName,
            solution = report.SolutionPath,
            projects = report.Graph.Projects.Count,
            edges = report.Graph.Edges.Count,
            errorCount = report.ErrorCount,
            warningCount = report.WarningCount,
            violations = report.Violations.Select(v => new
            {
                v.RuleId,
                severity = v.Severity,
                v.Message,
                v.From,
                v.To,
                cycle = v.Cycle
            }),
            graph = new
            {
                projects = report.Graph.Projects.Select(p => new { p.Name, p.Path, p.TargetFramework }),
                edges = report.Graph.Edges.Select(e => new { e.From, e.To })
            }
        };

        return JsonSerializer.Serialize(payload, Options);
    }
}
