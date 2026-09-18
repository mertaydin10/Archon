using System.Text.Json;
using System.Text.Json.Serialization;
using Archon.Core;

namespace Archon.Analysis;

public static class BaselineFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Write(AnalysisReport report)
    {
        var payload = new BaselineDocument
        {
            Name = report.RuleSetName,
            Violations = report.Violations.Select(v => new BaselineViolation
            {
                RuleId = v.RuleId,
                From = v.From,
                To = v.To,
                FilePath = v.FilePath,
                Line = v.Line,
                Cycle = v.Cycle?.ToArray()
            }).ToList()
        };
        return JsonSerializer.Serialize(payload, Options);
    }

    public static IReadOnlySet<string> LoadKeys(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException("Baseline file was not found.", full);

        var document = JsonSerializer.Deserialize<BaselineDocument>(File.ReadAllText(full), Options)
            ?? new BaselineDocument();

        return document.Violations
            .Select(v => ViolationKey.Of(new Violation(
                v.RuleId ?? "",
                RuleSeverity.Error,
                "",
                v.From,
                v.To,
                v.Cycle,
                v.FilePath,
                v.Line)))
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed class BaselineDocument
    {
        public string? Name { get; set; }
        public List<BaselineViolation> Violations { get; set; } = [];
    }

    private sealed class BaselineViolation
    {
        public string? RuleId { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? FilePath { get; set; }
        public int? Line { get; set; }
        public string[]? Cycle { get; set; }
    }
}
