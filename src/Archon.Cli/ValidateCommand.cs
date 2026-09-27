using System.ComponentModel;
using Archon.Analysis;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class ValidateSettings : CommandSettings
{
    [CommandArgument(0, "[file]")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Path { get; init; }

    [CommandOption("-s|--solution <FILE>")]
    [Description("Kalıpları bu solution'a karşı denetle. Boşsa kurallardaki solution kullanılır.")]
    public string? Solution { get; init; }

    [CommandOption("--strict")]
    [Description("Hiçbir projeye uymayan kalıp varsa çıkış kodu 1 döner.")]
    public bool Strict { get; init; }
}

internal sealed class ValidateCommand : Command<ValidateSettings>
{
    protected override int Execute(CommandContext context, ValidateSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(settings.Path)
                ? Path.GetFullPath("archon.yaml")
                : Path.GetFullPath(settings.Path);
            var ruleSet = new RuleSetLoader().Load(path);
            AnsiConsole.MarkupLine($"[green]Geçerli:[/] {Markup.Escape(path)}");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(ruleSet.Name)} · {ruleSet.Rules.Count} kural[/]");
            foreach (var rule in ruleSet.Rules)
                AnsiConsole.MarkupLine($"  • {Markup.Escape(rule.Id)} ({Markup.Escape(rule.GetType().Name)})");

            if (string.IsNullOrWhiteSpace(settings.Solution) && string.IsNullOrWhiteSpace(ruleSet.SolutionPath))
                return 0;

            var solution = ArchonWorkspace.ResolveSolutionPath(settings.Solution, ruleSet);
            var graph = new SolutionGraphLoader().Load(solution).Exclude(ruleSet.ExcludedProjects);
            var unmatched = RuleCoverage.FindUnmatched(graph, ruleSet);

            AnsiConsole.WriteLine();
            if (unmatched.Count == 0)
            {
                AnsiConsole.MarkupLine($"[green]Tüm proje kalıpları en az bir projeye uyuyor.[/] [grey]{graph.Projects.Count} proje[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]{unmatched.Count} kalıp hiçbir projeye uymuyor:[/]");
            foreach (var item in unmatched)
                AnsiConsole.MarkupLine($"  • {Markup.Escape(item.RuleId)} {Markup.Escape(item.Field)}: {Markup.Escape(item.Pattern)}");

            return settings.Strict ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }
}
