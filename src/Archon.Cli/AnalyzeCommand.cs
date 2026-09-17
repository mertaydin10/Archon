using System.ComponentModel;
using Archon.Analysis;
using Archon.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class AnalyzeSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Solution dosyası veya klasör. Boşsa kurallardaki solution yolu kullanılır.")]
    public string? Path { get; init; }

    [CommandOption("-r|--rules <FILE>")]
    [Description("YAML kural dosyası. Varsayılan: ./archon.yaml")]
    public string? Rules { get; init; }

    [CommandOption("-f|--format <FORMAT>")]
    [Description("Çıktı biçimi: console, json, html, sarif")]
    public string Format { get; init; } = "console";

    [CommandOption("-o|--out <FILE>")]
    [Description("json/html/sarif için dosya yolu")]
    public string? Output { get; init; }
}

internal sealed class AnalyzeCommand : Command<AnalyzeSettings>
{
    protected override int Execute(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = ArchonWorkspace.Load(settings.Path, settings.Rules);
            var report = new ArchitectureAnalyzer().Analyze(
                workspace.Graph,
                workspace.RuleSet,
                workspace.SolutionPath,
                workspace.Sources);

            WriteReport(report, settings);
            return report.HasErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }

    private static void WriteReport(AnalysisReport report, AnalyzeSettings settings)
    {
        var format = settings.Format.Trim().ToLowerInvariant();
        switch (format)
        {
            case "json":
                WriteFileOrStdout(settings.Output, JsonReportWriter.Write(report));
                break;
            case "sarif":
                var sarifPath = settings.Output ?? Path.Combine("artifacts", "archon.sarif");
                WriteFileOrStdout(sarifPath, SarifReportWriter.Write(report));
                AnsiConsole.MarkupLine($"[grey]SARIF rapor:[/] {Markup.Escape(Path.GetFullPath(sarifPath))}");
                break;
            case "html":
                var html = HtmlReportWriter.Write(report);
                var output = settings.Output ?? Path.Combine("artifacts", "archon-report.html");
                WriteFileOrStdout(output, html);
                AnsiConsole.MarkupLine($"[grey]HTML rapor:[/] {Markup.Escape(Path.GetFullPath(output))}");
                break;
            case "console":
                ConsoleReportWriter.Write(report);
                break;
            default:
                throw new InvalidOperationException($"Bilinmeyen format '{settings.Format}'.");
        }
    }

    private static void WriteFileOrStdout(string? output, string content)
    {
        if (string.IsNullOrWhiteSpace(output) || output == "-")
        {
            Console.WriteLine(content);
            return;
        }

        var full = Path.GetFullPath(output);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }
}
