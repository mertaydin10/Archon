using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class PathSettings : CommandSettings
{
    [CommandArgument(0, "<from>")]
    [Description("Başlangıç projesi.")]
    public string From { get; init; } = "";

    [CommandArgument(1, "<to>")]
    [Description("Hedef proje.")]
    public string To { get; init; } = "";

    [CommandArgument(2, "[path]")]
    [Description("Solution dosyası veya klasör.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }
}

internal sealed class PathCommand : Command<PathSettings>
{
    protected override int Execute(CommandContext context, PathSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules);
            var from = settings.From.Trim();
            var to = settings.To.Trim();
            if (!workspace.Graph.Contains(from))
                throw new InvalidOperationException($"'{from}' grafikte yok.");
            if (!workspace.Graph.Contains(to))
                throw new InvalidOperationException($"'{to}' grafikte yok.");

            var path = workspace.Graph.ShortestPath(from, to);
            if (path is null)
            {
                AnsiConsole.MarkupLine($"[yellow]Yol yok:[/] {Markup.Escape(from)} → {Markup.Escape(to)}");
                return 1;
            }

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(string.Join(" → ", path))}[/]");
            AnsiConsole.MarkupLine($"[grey]{path.Count - 1} kenar[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }
}
