using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class MarkdownReportWriter
{
    public static string Write(AnalysisReport report)
    {
        var sb = new StringBuilder();
        var status = report.HasErrors ? "KIRILDI" : "TEMİZ";
        sb.AppendLine($"# Archon — {report.RuleSetName}");
        sb.AppendLine();
        sb.AppendLine($"**{status}** · {report.Graph.Projects.Count} proje · {report.Graph.Edges.Count} kenar · {report.ErrorCount} hata · {report.WarningCount} uyarı");
        if (report.BaselineSuppressed > 0)
            sb.AppendLine($"{report.BaselineSuppressed} ihlal baseline'da olduğu için yok sayıldı.");
        sb.AppendLine();
        sb.AppendLine($"`{report.SolutionPath}`");
        sb.AppendLine();

        if (report.Violations.Count == 0)
        {
            sb.AppendLine("Tanımlı kurallara göre yeni ihlal yok.");
            return sb.ToString();
        }

        sb.AppendLine("| Kural | Şiddet | Kenar | Konum |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var violation in report.Violations)
        {
            var edge = violation.Cycle is { Count: > 0 }
                ? string.Join(" → ", violation.Cycle.Append(violation.Cycle[0]))
                : $"{violation.From} → {violation.To}";
            var location = string.IsNullOrWhiteSpace(violation.FilePath)
                ? "—"
                : violation.Line is int line ? $"{violation.FilePath}:{line}" : violation.FilePath;
            sb.AppendLine($"| `{Escape(violation.RuleId)}` | {violation.Severity} | {Escape(edge)} | {Escape(location)} |");
        }

        return sb.ToString();
    }

    private static string Escape(string? value) =>
        (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
