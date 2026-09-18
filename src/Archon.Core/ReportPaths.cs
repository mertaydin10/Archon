namespace Archon.Core;

public static class ReportPaths
{
    public static AnalysisReport Relativize(AnalysisReport report)
    {
        var root = Path.GetDirectoryName(report.SolutionPath);
        if (string.IsNullOrWhiteSpace(root))
            return report;

        var violations = report.Violations.Select(v =>
        {
            if (string.IsNullOrWhiteSpace(v.FilePath))
                return v;
            if (!Path.IsPathRooted(v.FilePath))
                return v with { FilePath = v.FilePath.Replace('\\', '/') };

            var relative = Path.GetRelativePath(root, v.FilePath).Replace('\\', '/');
            return v with { FilePath = relative };
        }).ToArray();

        return report with { Violations = violations };
    }
}
