using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class GithubAnnotationWriter
{
    public static string Write(AnalysisReport report, string? pathPrefix = null)
    {
        var solutionDir = Path.GetDirectoryName(report.SolutionPath) ?? "";
        var sb = new StringBuilder();

        foreach (var violation in report.Violations)
        {
            var level = violation.Severity == RuleSeverity.Error ? "error" : "warning";
            var file = ResolveFile(report, violation, solutionDir);
            sb.Append("::").Append(level).Append(' ');

            var properties = new List<string>();
            if (file is not null)
                properties.Add("file=" + EscapeProperty(Prefix(pathPrefix, file)));
            if (violation.Line is int line)
                properties.Add("line=" + line);
            properties.Add("title=" + EscapeProperty("Archon " + violation.RuleId));

            sb.Append(string.Join(',', properties));
            sb.Append("::").Append(EscapeData(violation.Message)).Append('\n');
        }

        return sb.ToString();
    }

    private static string? ResolveFile(AnalysisReport report, Violation violation, string solutionDir)
    {
        if (!string.IsNullOrWhiteSpace(violation.FilePath))
            return violation.FilePath.Replace('\\', '/');
        if (violation.From is null || !report.Graph.Contains(violation.From))
            return null;

        var projectPath = report.Graph.Get(violation.From).Path;
        if (!Path.IsPathRooted(projectPath) || solutionDir.Length == 0)
            return null;
        return Path.GetRelativePath(solutionDir, projectPath).Replace('\\', '/');
    }

    private static string Prefix(string? prefix, string file)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix == ".")
            return file;
        return prefix.Replace('\\', '/').TrimEnd('/') + "/" + file;
    }

    private static string EscapeData(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

    private static string EscapeProperty(string value) =>
        EscapeData(value).Replace(":", "%3A").Replace(",", "%2C");
}
