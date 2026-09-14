using System.Text.RegularExpressions;

namespace Archon.Core;

public sealed class GlobPattern
{
    private readonly Regex _regex;

    public GlobPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern.Trim();
        var escaped = Regex.Escape(Pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal);
        _regex = new Regex(
            "^" + escaped + "$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public string Pattern { get; }

    public bool IsMatch(string value) => _regex.IsMatch(value);

    public static bool IsMatch(string pattern, string value) => new GlobPattern(pattern).IsMatch(value);
}
