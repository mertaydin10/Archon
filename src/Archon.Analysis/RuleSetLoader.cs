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
        var loaded = Load(path, [], []);
        if (loaded.Rules.Count == 0)
            throw new InvalidOperationException("Rules file must contain at least one rule.");
        return loaded;
    }

    private RuleSet Load(string path, HashSet<string> visiting, HashSet<string> seen)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Rules file was not found.", fullPath);
        if (!visiting.Add(fullPath))
            throw new InvalidOperationException($"Döngüsel include: {fullPath}");

        var document = _deserializer.Deserialize<RuleSetDocument>(File.ReadAllText(fullPath))
            ?? throw new InvalidOperationException("Rules file is empty.");

        var rules = new List<ArchitectureRule>();
        var exclude = new List<string>();
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        if (seen.Add(fullPath))
        {
            foreach (var include in document.Includes ?? [])
            {
                if (string.IsNullOrWhiteSpace(include))
                    continue;
                var included = Load(Path.Combine(directory, include.Trim()), visiting, seen);
                rules.AddRange(included.Rules);
                exclude.AddRange(included.ExcludedProjects);
            }

            rules.AddRange((document.Rules ?? []).Select(ToRule));
            exclude.AddRange(Flatten(document.Exclude));
        }

        visiting.Remove(fullPath);

        var solution = string.IsNullOrWhiteSpace(document.Solution)
            ? null
            : Path.GetFullPath(Path.Combine(directory, document.Solution));

        return new RuleSet(
            string.IsNullOrWhiteSpace(document.Name) ? Path.GetFileNameWithoutExtension(fullPath) : document.Name.Trim(),
            solution,
            rules,
            exclude.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
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
            "max-fanout" => new MaxFanoutRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                document.Max is >= 0
                    ? document.Max.Value
                    : throw new InvalidOperationException($"Rule '{document.Id}' must set max >= 0.")),
            "sdp" or "stable-dependencies" => new StableDependencyRule(document.Id.Trim(), description, severity),
            "isolated" => new IsolatedRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "version-aligned" => new VersionAlignedRule(
                document.Id.Trim(),
                description,
                severity,
                Flatten(document.Packages ?? document.To)),
            "internals-deny" => new InternalsDenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "must-depend" => new MustDependRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "max-fanin" => new MaxFaninRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                document.Max is >= 0
                    ? document.Max.Value
                    : throw new InvalidOperationException($"Rule '{document.Id}' must set max >= 0.")),
            "max-depth" => new MaxDepthRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                document.Max is >= 0
                    ? document.Max.Value
                    : throw new InvalidOperationException($"Rule '{document.Id}' must set max >= 0.")),
            "tfm-aligned" or "framework-aligned" => new TfmAlignedRule(
                document.Id.Trim(),
                description,
                severity,
                Flatten(document.From)),
            "deny-transitive" or "reach-deny" => new TransitiveDenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.To, "to"),
                ReadExceptions(document.Except)),
            "package-allow" => new PackageAllowRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                Flatten(document.Packages ?? document.To),
                ReadExceptions(document.Except)),
            "sdk-deny" => new SdkDenyRule(
                document.Id.Trim(),
                description,
                severity,
                ReadPatterns(document.From, "from"),
                ReadPatterns(document.Sdks ?? document.To, "sdks")),
            "naming" => new NamingRule(
                document.Id.Trim(),
                description,
                severity,
                Flatten(document.From),
                ReadPatterns(document.Patterns ?? document.To, "patterns")),
            "no-orphans" => new NoOrphansRule(
                document.Id.Trim(),
                description,
                severity,
                Flatten(document.From)),
            "package-min-version" => new PackageMinVersionRule(
                document.Id.Trim(),
                description,
                severity,
                Flatten(document.From),
                ReadPatterns(document.Packages ?? document.To, "packages"),
                PackageVersion.TryNormalize(document.Min, out var min)
                    ? min
                    : throw new InvalidOperationException($"Rule '{document.Id}' must set a numeric 'min' version.")),
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
            throw new InvalidOperationException($"Rule is missing '{field}'.");
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
        public List<string>? Includes { get; set; }
        public object? Exclude { get; set; }
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
        public object? Sdks { get; set; }
        public object? Patterns { get; set; }
        public List<string>? Layers { get; set; }
        public int? Max { get; set; }
        public string? Min { get; set; }
        public List<ExceptionDocument>? Except { get; set; }
    }

    private sealed class ExceptionDocument
    {
        public string? From { get; set; }
        public string? To { get; set; }
    }
}
