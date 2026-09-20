using System.ComponentModel;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class ExplainSettings : CommandSettings
{
    [CommandArgument(0, "<project>")]
    [Description("Açıklanacak proje adı.")]
    public string Project { get; init; } = "";

    [CommandArgument(1, "[path]")]
    [Description("Solution dosyası veya klasör.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }
}

internal sealed class ExplainCommand : Command<ExplainSettings>
{
    protected override int Execute(CommandContext context, ExplainSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules);
            var report = new ArchitectureAnalyzer().Analyze(
                workspace.Graph,
                workspace.RuleSet,
                workspace.SolutionPath,
                workspace.Sources,
                workspace.Packages,
                workspace.Friends);
            var explanation = ProjectExplainer.Explain(workspace.Graph, settings.Project.Trim(), report.Violations);

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(explanation.Project)}[/]");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(workspace.SolutionPath)}[/]");
            var metric = CouplingCalculator.Of(workspace.Graph, explanation.Project);
            AnsiConsole.MarkupLine($"[grey]Ce={metric.Ce}  Ca={metric.Ca}  I={metric.Instability:0.00}[/]");
            AnsiConsole.WriteLine();
            WriteList("Giden bağımlılıklar", explanation.Dependencies);
            WriteList("Gelen bağımlılıklar", explanation.Dependents);
            WriteList("Etki alanı (geçişli)", explanation.BlastRadius);

            if (explanation.RelatedViolations.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]Bu projeyi doğrudan ilgilendiren ihlal yok.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine("[red]Bu projeyi ilgilendiren ihlaller[/]");
            foreach (var violation in explanation.RelatedViolations)
            {
                var edge = $"{violation.From} → {violation.To}";
                AnsiConsole.MarkupLine($"[red]•[/] {Markup.Escape(violation.RuleId)}  {Markup.Escape(edge)}");
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(violation.Message)}[/]");
            }

            return report.HasErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }

    private static void WriteList(string title, IReadOnlyList<string> items)
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]  —[/]");
            AnsiConsole.WriteLine();
            return;
        }

        foreach (var item in items)
            AnsiConsole.MarkupLine($"  • {Markup.Escape(item)}");
        AnsiConsole.WriteLine();
    }
}
