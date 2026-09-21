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
    [Description("Çıktı biçimi: console, json, html, sarif, markdown, junit")]
    public string Format { get; init; } = "console";

    [CommandOption("-o|--out <FILE>")]
    [Description("json/html/sarif/markdown/junit için dosya yolu")]
    public string? Output { get; init; }

    [CommandOption("--exclude <GLOB>")]
    [Description("Analizden çıkarılacak proje glob'u. Birden fazla kez verilebilir.")]
    public string[]? Exclude { get; init; }

    [CommandOption("--baseline <FILE>")]
    [Description("Bilinen ihlalleri yok saymak için baseline JSON")]
    public string? Baseline { get; init; }

    [CommandOption("--write-baseline <FILE>")]
    [Description("Mevcut ihlalleri baseline JSON olarak yazar")]
    public string? WriteBaseline { get; init; }

    [CommandOption("--fail-on-warning")]
    [Description("Uyarıları da hata gibi işler (çıkış kodu 1).")]
    public bool FailOnWarning { get; init; }

    [CommandOption("--only <RULE>")]
    [Description("Yalnızca eşleşen kural kimliklerini göster. Glob kabul eder.")]
    public string[]? Only { get; init; }
}

internal sealed class AnalyzeCommand : Command<AnalyzeSettings>
{
    protected override int Execute(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
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
            report = ReportPaths.Relativize(report);

            if (!string.IsNullOrWhiteSpace(settings.WriteBaseline))
            {
                WriteFileOrStdout(settings.WriteBaseline, BaselineFile.Write(report));
                AnsiConsole.MarkupLine($"[grey]Baseline yazıldı:[/] {Markup.Escape(Path.GetFullPath(settings.WriteBaseline))}");
            }

            if (!string.IsNullOrWhiteSpace(settings.Baseline))
                report = BaselineFilter.Apply(report, BaselineFile.LoadKeys(settings.Baseline));

            report = ReportFilter.Only(report, settings.Only);

            WriteReport(report, settings);
            return report.ShouldFail(settings.FailOnWarning) ? 1 : 0;
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
                var htmlPath = settings.Output ?? Path.Combine("artifacts", "archon-report.html");
                WriteFileOrStdout(htmlPath, html);
                AnsiConsole.MarkupLine($"[grey]HTML rapor:[/] {Markup.Escape(Path.GetFullPath(htmlPath))}");
                break;
            case "markdown" or "md":
                var mdPath = settings.Output ?? Path.Combine("artifacts", "archon.md");
                WriteFileOrStdout(mdPath, MarkdownReportWriter.Write(report));
                AnsiConsole.MarkupLine($"[grey]Markdown rapor:[/] {Markup.Escape(Path.GetFullPath(mdPath))}");
                break;
            case "junit" or "xml":
                var junitPath = settings.Output ?? Path.Combine("artifacts", "archon-junit.xml");
                WriteFileOrStdout(junitPath, JunitReportWriter.Write(report));
                AnsiConsole.MarkupLine($"[grey]JUnit rapor:[/] {Markup.Escape(Path.GetFullPath(junitPath))}");
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
