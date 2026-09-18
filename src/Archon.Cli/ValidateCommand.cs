using System.ComponentModel;
using Archon.Analysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class ValidateSettings : CommandSettings
{
    [CommandArgument(0, "[file]")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Path { get; init; }
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
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }
}
