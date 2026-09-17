using Archon.Core;
using Spectre.Console;

namespace Archon.Cli;

internal static class ConsoleReportWriter
{
    public static void Write(AnalysisReport report)
    {
        AnsiConsole.Write(new FigletText("ARCHON").Color(Color.Tan));
        AnsiConsole.MarkupLine("[grey]Mimari Jandarma[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(report.RuleSetName)}[/]");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(report.SolutionPath)}[/]");
        AnsiConsole.MarkupLine(
            $"[grey]{report.Graph.Projects.Count} proje · {report.Graph.Edges.Count} kenar · {report.ErrorCount} hata · {report.WarningCount} uyarı[/]");
        AnsiConsole.WriteLine();

        if (report.Violations.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]Kurallar temiz. Bu solution tanımlı sınırları bozmuyor.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.SimpleHeavy);
        table.AddColumn("Kural");
        table.AddColumn("Şiddet");
        table.AddColumn("Kenar");
        table.AddColumn("Konum");
        table.AddColumn("Açıklama");

        foreach (var violation in report.Violations)
        {
            var edge = violation.Cycle is { Count: > 0 }
                ? string.Join(" → ", violation.Cycle.Append(violation.Cycle[0]))
                : $"{violation.From} → {violation.To}";
            var severity = violation.Severity == RuleSeverity.Error
                ? "[red]HATA[/]"
                : "[yellow]UYARI[/]";
            var location = string.IsNullOrWhiteSpace(violation.FilePath)
                ? "—"
                : violation.Line is int line
                    ? $"{violation.FilePath}:{line}"
                    : violation.FilePath!;
            table.AddRow(
                Markup.Escape(violation.RuleId),
                severity,
                Markup.Escape(edge),
                Markup.Escape(location),
                Markup.Escape(violation.Message));
        }

        AnsiConsole.Write(table);
    }
}
