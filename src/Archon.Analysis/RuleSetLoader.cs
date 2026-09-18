using System.Collections;
using Archon.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Archon.Analysis;

public sealed class RuleSetLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public RuleSet Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Rules file was not found.", fullPath);

        var document = _deserializer.Deserialize<RuleSetDocument>(File.ReadAllText(fullPath))
            ?? throw new InvalidOperationException("Rules file is empty.");

        if (document.Rules.Count == 0)
            throw new InvalidOperationException("Rules file must contain at least one rule.");

        var rules = document.Rules.Select(ToRule).ToArray();
        var solution = string.IsNullOrWhiteSpace(document.Solution)
            ? null
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory, document.Solution));

        return new RuleSet(
            string.IsNullOrWhiteSpace(document.Name) ? Path.GetFileNameWithoutExtension(fullPath) : document.Name.Trim(),
            solution,
            rules);
    }

    private static ArchitectureRule ToRule(RuleDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Id))
            throw new InvalidOperationException("Every rule needs an id.");

        var kind = (document.Kind ?? "").Trim().ToLowerInvariant();
        var description = string.IsNullOrWhiteSpace(document.Description)
            ? document.Id
            : document.Description.Trim();
        var severity = ParseSeverity(document.Severity);

        return kind switch
        {
            "deny" => new DenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "allow" => new AllowRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "namespace-deny" => new NamespaceDenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "package-deny" => new PackageDenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.Packages ?? document.To, "packages"),
                ReadExceptions(document.Except)),
            "layers" => new LayerRule(
                document.Id.Trim(),
                description,
                severity,
                document.Layers is { Count: > 1 }
                    ? document.Layers
                    : throw new InvalidOperationException($"Rule '{document.Id}' must list at least two layers.")),
            "acyclic" => new AcyclicRule(document.Id.Trim(), description, severity),
            _ => throw new InvalidOperationException($"Unknown rule kind '{document.Kind}' in '{document.Id}'.")
        };
    }

    private static RuleSeverity ParseSeverity(string? value) =>
        (value ?? "error").Trim().ToLowerInvariant() switch
        {
            "warning" or "warn" => RuleSeverity.Warning,
            "error" => RuleSeverity.Error,
            _ => throw new InvalidOperationException($"Unknown severity '{value}'.")
        };

    private static IReadOnlyList<string> ReadPatterns(object? value, string field)
    {
        var list = Flatten(value);
        if (list.Count == 0)
            throw new InvalidOperationException($"Deny rule is missing '{field}'.");
        return list;
    }

    private static IReadOnlyList<RuleException> ReadExceptions(List<ExceptionDocument>? documents)
    {
        if (documents is null || documents.Count == 0)
            return [];

        return documents
            .Select(x => new RuleException(
                x.From ?? throw new InvalidOperationException("Exception is missing 'from'."),
                x.To ?? throw new InvalidOperationException("Exception is missing 'to'.")))
            .ToArray();
    }

    private static IReadOnlyList<string> Flatten(object? value)
    {
        switch (value)
        {
            case null:
                return [];
            case string text:
                return string.IsNullOrWhiteSpace(text) ? [] : [text.Trim()];
            case IEnumerable items:
                return items.Cast<object?>()
                    .Select(item => item?.ToString()?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray();
            default:
                var raw = value.ToString()?.Trim();
                return string.IsNullOrWhiteSpace(raw) ? [] : [raw];
        }
    }

    private sealed class RuleSetDocument
    {
        public string? Name { get; set; }
        public string? Solution { get; set; }
        public List<RuleDocument> Rules { get; set; } = [];
    }

    private sealed class RuleDocument
    {
        public string Id { get; set; } = "";
        public string? Kind { get; set; }
        public string? Description { get; set; }
        public string? Severity { get; set; }
        public object? From { get; set; }
        public object? To { get; set; }
        public object? Packages { get; set; }
        public List<string>? Layers { get; set; }
        public List<ExceptionDocument>? Except { get; set; }
    }

    private sealed class ExceptionDocument
    {
        public string? From { get; set; }
        public string? To { get; set; }
    }
}
