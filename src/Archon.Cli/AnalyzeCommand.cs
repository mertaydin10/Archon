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
    [Description("Çıktı biçimi: console, json, html")]
    public string Format { get; init; } = "console";

    [CommandOption("-o|--out <FILE>")]
    [Description("json/html için dosya yolu")]
    public string? Output { get; init; }
}

internal sealed class AnalyzeCommand : Command<AnalyzeSettings>
{
    protected override int Execute(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var rulesPath = ResolveRulesPath(settings.Rules);
            var ruleSet = File.Exists(rulesPath)
                ? new RuleSetLoader().Load(rulesPath)
                : new RuleSet("Archon", null, [new AcyclicRule("no-cycles", "Proje grafı döngü içeremez.", RuleSeverity.Warning)]);

            var solutionPath = ResolveSolutionPath(settings.Path, ruleSet);
            var graph = new SolutionGraphLoader().Load(solutionPath);
            var report = new ArchitectureAnalyzer().Analyze(graph, ruleSet, solutionPath);

            WriteReport(report, settings);
            return report.HasErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Archon durdu:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }
    }

    private static string ResolveRulesPath(string? rules)
    {
        if (!string.IsNullOrWhiteSpace(rules))
            return Path.GetFullPath(rules);

        return Path.GetFullPath("archon.yaml");
    }

    private static string ResolveSolutionPath(string? path, RuleSet ruleSet)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var full = Path.GetFullPath(path);
            if (Directory.Exists(full))
            {
                var solutions = Directory.GetFiles(full, "*.sln", SearchOption.TopDirectoryOnly);
                if (solutions.Length == 1)
                    return solutions[0];
                if (solutions.Length == 0)
                    throw new InvalidOperationException($"'{full}' içinde .sln yok.");
                throw new InvalidOperationException($"'{full}' içinde birden fazla .sln var. Dosyayı açıkça ver.");
            }

            return full;
        }

        if (!string.IsNullOrWhiteSpace(ruleSet.SolutionPath))
            return ruleSet.SolutionPath;

        throw new InvalidOperationException("Solution yolu verilmedi. analyze <dosya.sln> veya archon.yaml içine solution yaz.");
    }

    private static void WriteReport(AnalysisReport report, AnalyzeSettings settings)
    {
        var format = settings.Format.Trim().ToLowerInvariant();
        switch (format)
        {
            case "json":
                WriteFileOrStdout(settings.Output, JsonReportWriter.Write(report));
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
