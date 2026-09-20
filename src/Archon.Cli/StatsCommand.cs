using System.ComponentModel;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class StatsSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Solution dosyası veya klasör.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }

    [CommandOption("--exclude <GLOB>")]
    [Description("Analizden çıkarılacak proje glob'u. Birden fazla kez verilebilir.")]
    public string[]? Exclude { get; init; }
}

internal sealed class StatsCommand : Command<StatsSettings>
{
    protected override int Execute(CommandContext context, StatsSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules, settings.Exclude);
            var metrics = CouplingCalculator.Compute(workspace.Graph)
                .OrderByDescending(m => m.Instability)
                .ThenBy(m => m.Project, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(workspace.RuleSet.Name)}[/]");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(workspace.SolutionPath)}[/]");
            AnsiConsole.MarkupLine(
                $"[grey]{workspace.Graph.Projects.Count} proje · {workspace.Graph.Edges.Count} kenar · {workspace.Packages.Packages.Count} paket · {workspace.Friends.Friends.Count} InternalsVisibleTo[/]");
            AnsiConsole.WriteLine();

            if (metrics.Length == 0)
            {
                AnsiConsole.MarkupLine("[grey]Graf boş.[/]");
                return 0;
            }

            var table = new Table().Border(TableBorder.SimpleHeavy);
            table.AddColumn("Proje");
            table.AddColumn("Ce");
            table.AddColumn("Ca");
            table.AddColumn("I");
            table.AddColumn("Giden");

            foreach (var metric in metrics)
            {
                var outgoing = string.Join(", ", workspace.Graph.Dependencies(metric.Project));
                table.AddRow(
                    Markup.Escape(metric.Project),
                    metric.Ce.ToString(),
                    metric.Ca.ToString(),
                    metric.Instability.ToString("0.00"),
                    Markup.Escape(string.IsNullOrWhiteSpace(outgoing) ? "—" : outgoing));
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }
}
