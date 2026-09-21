using System.ComponentModel;
using Archon.Analysis;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class DiffSettings : CommandSettings
{
    [CommandArgument(0, "<baseline>")]
    [Description("Önceki ihlallerin baseline JSON dosyası.")]
    public string Baseline { get; init; } = "";

    [CommandArgument(1, "[path]")]
    [Description("Solution dosyası veya klasör.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }

    [CommandOption("--exclude <GLOB>")]
    [Description("Analizden çıkarılacak proje glob'u.")]
    public string[]? Exclude { get; init; }
}

internal sealed class DiffCommand : Command<DiffSettings>
{
    protected override int Execute(CommandContext context, DiffSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var previous = BaselineFile.LoadViolations(settings.Baseline);
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules, settings.Exclude);
            var report = ReportPaths.Relativize(new ArchitectureAnalyzer().Analyze(
                workspace.Graph,
                workspace.RuleSet,
                workspace.SolutionPath,
                workspace.Sources,
                workspace.Packages,
                workspace.Friends));
            var diff = ViolationDiffCalculator.Compute(previous, report.Violations);

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(workspace.RuleSet.Name)}[/]");
            AnsiConsole.MarkupLine($"[grey]önceki:[/] {Markup.Escape(Path.GetFullPath(settings.Baseline))}");
            AnsiConsole.MarkupLine($"[grey]+{diff.Added.Count} yeni · -{diff.Removed.Count} düzelmiş[/]");
            AnsiConsole.WriteLine();

            WriteSection("[red]Yeni ihlaller[/]", diff.Added);
            WriteSection("[green]Düzelen ihlaller[/]", diff.Removed);

            if (diff.Added.Count == 0 && diff.Removed.Count == 0)
                AnsiConsole.MarkupLine("[green]Baseline ile mevcut analiz aynı.[/]");

            return diff.Added.Count > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }

    private static void WriteSection(string title, IReadOnlyList<Violation> violations)
    {
        if (violations.Count == 0)
            return;

        AnsiConsole.MarkupLine(title);
        foreach (var violation in violations)
        {
            var edge = $"{violation.From} → {violation.To}";
            AnsiConsole.MarkupLine($"  • {Markup.Escape(violation.RuleId)}  {Markup.Escape(edge)}");
        }

        AnsiConsole.WriteLine();
    }
}
