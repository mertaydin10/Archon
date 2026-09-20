using System.Net;
using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class JunitReportWriter
{
    public static string Write(AnalysisReport report)
    {
        var sb = new StringBuilder();
        sb.Append("<testsuite name=\"").Append(Encode(report.RuleSetName)).Append('"');
        sb.Append(" tests=\"").Append(Math.Max(1, report.Violations.Count)).Append('"');
        sb.Append(" failures=\"").Append(report.ErrorCount).Append('"');
        sb.Append(" errors=\"0\">");

        if (report.Violations.Count == 0)
        {
            sb.Append("<testcase classname=\"Archon\" name=\"architecture\" />");
        }
        else
        {
            foreach (var violation in report.Violations)
            {
                var name = $"{violation.RuleId} {violation.From} -> {violation.To}";
                sb.Append("<testcase classname=\"Archon\" name=\"").Append(Encode(name)).Append("\">");
                if (violation.Severity == RuleSeverity.Error)
                {
                    sb.Append("<failure message=\"").Append(Encode(violation.Message)).Append("\" />");
                }

                sb.Append("</testcase>");
            }
        }

        sb.Append("</testsuite>");
        return sb.ToString();
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
