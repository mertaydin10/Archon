using System.ComponentModel;
using Archon.Analysis;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class GraphSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Solution dosyası veya klasör.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }

    [CommandOption("-f|--format <FORMAT>")]
    [Description("Çıktı biçimi: mermaid veya dot")]
    public string Format { get; init; } = "mermaid";

    [CommandOption("-o|--out <FILE>")]
    [Description("Dosya yolu. Boşsa stdout.")]
    public string? Output { get; init; }

    [CommandOption("--exclude <GLOB>")]
    [Description("Grafikten çıkarılacak proje glob'u.")]
    public string[]? Exclude { get; init; }
}

internal sealed class GraphCommand : Command<GraphSettings>
{
    protected override int Execute(CommandContext context, GraphSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules, settings.Exclude);
            var report = new ArchitectureAnalyzer().Analyze(
                workspace.Graph,
                workspace.RuleSet,
                workspace.SolutionPath,
                workspace.Sources,
                workspace.Packages,
                workspace.Friends);
            var broken = report.Violations
                .Where(v => v.From is not null && v.To is not null && v.Cycle is null)
                .Select(v => (v.From!, v.To!))
                .ToHashSet();

            var text = settings.Format.Trim().ToLowerInvariant() switch
            {
                "dot" => GraphTextWriter.Dot(workspace.Graph, broken),
                "mermaid" or "md" => GraphTextWriter.Mermaid(workspace.Graph, broken),
                _ => throw new InvalidOperationException($"Bilinmeyen format '{settings.Format}'.")
            };

            if (string.IsNullOrWhiteSpace(settings.Output) || settings.Output == "-")
                Console.Write(text);
            else
            {
                var full = Path.GetFullPath(settings.Output);
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(full, text);
                AnsiConsole.MarkupLine($"[grey]Graf:[/] {Markup.Escape(full)}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }
}
