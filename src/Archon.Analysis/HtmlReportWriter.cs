using System.Net;
using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class HtmlReportWriter
{
    public static string Write(AnalysisReport report)
    {
        var broken = report.Violations
            .Where(v => v.From is not null && v.To is not null)
            .Select(v => (v.From!, v.To!))
            .ToHashSet();
        var svg = SvgGraphRenderer.Render(report.Graph, broken);
        var status = report.HasErrors ? "KIRILDI" : "TEMİZ";
        var statusClass = report.HasErrors ? "bad" : "ok";

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"/>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<title>Archon — ").Append(Encode(report.RuleSetName)).Append("</title>");
        sb.Append("<style>");
        sb.Append(":root{--ink:#221f1b;--paper:#f3efe6;--rule:#d8d0c4;--bad:#c23b22;--ok:#2f6f4f;--muted:#6d675e}");
        sb.Append("html,body{margin:0;background:var(--paper);color:var(--ink);font:16px/1.5 Georgia,'Times New Roman',serif}");
        sb.Append("header,main{max-width:1080px;margin:0 auto;padding:32px 24px}");
        sb.Append("header{padding-top:48px;border-bottom:1px solid var(--rule)}");
        sb.Append(".eyebrow{letter-spacing:.18em;text-transform:uppercase;font-size:12px;color:var(--muted)}");
        sb.Append("h1{font-size:42px;font-weight:500;margin:8px 0 12px}");
        sb.Append(".meta{color:var(--muted)}");
        sb.Append(".cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:28px 0}");
        sb.Append(".card{border:1px solid var(--rule);padding:16px 18px;background:#fffdf8}");
        sb.Append(".card b{display:block;font-size:28px;font-weight:500}");
        sb.Append(".card.bad b{color:var(--bad)} .card.ok b{color:var(--ok)}");
        sb.Append("h2{font-size:22px;font-weight:500;margin:36px 0 12px}");
        sb.Append("table{width:100%;border-collapse:collapse}");
        sb.Append("th,td{text-align:left;padding:10px 8px;border-bottom:1px solid var(--rule);vertical-align:top}");
        sb.Append("th{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted);font-weight:500}");
        sb.Append(".sev{font-size:12px;letter-spacing:.08em;text-transform:uppercase}");
        sb.Append(".sev.error{color:var(--bad)} .sev.warning{color:#8a5a28}");
        sb.Append(".graph{border:1px solid var(--rule);background:#1c1a17;padding:12px;margin:16px 0 32px}");
        sb.Append(".status{display:inline-block;padding:4px 10px;border:1px solid currentColor;font-size:12px;letter-spacing:.12em}");
        sb.Append(".status.bad{color:var(--bad)} .status.ok{color:var(--ok)}");
        sb.Append("@media(max-width:800px){.cards{grid-template-columns:1fr 1fr}}");
        sb.Append("</style></head><body><header>");
        sb.Append("<div class=\"eyebrow\">Archon · Mimari Jandarma</div>");
        sb.Append("<h1>").Append(Encode(report.RuleSetName)).Append("</h1>");
        sb.Append("<p class=\"meta\">").Append(Encode(report.SolutionPath)).Append("</p>");
        sb.Append("<p><span class=\"status ").Append(statusClass).Append("\">").Append(status).Append("</span></p>");
        sb.Append("</header><main>");
        sb.Append("<div class=\"cards\">");
        sb.Append(Card("Proje", report.Graph.Projects.Count.ToString(), null));
        sb.Append(Card("Kenar", report.Graph.Edges.Count.ToString(), null));
        sb.Append(Card("Hata", report.ErrorCount.ToString(), report.ErrorCount > 0 ? "bad" : "ok"));
        sb.Append(Card("Uyarı", report.WarningCount.ToString(), report.WarningCount > 0 ? "bad" : null));
        sb.Append("</div>");
        sb.Append("<h2>Bağımlılık grafı</h2><div class=\"graph\">").Append(svg).Append("</div>");
        sb.Append("<h2>İhlaller</h2>");

        if (report.Violations.Count == 0)
        {
            sb.Append("<p>Tanımlı kurallara göre ihlal yok.</p>");
        }
        else
        {
            sb.Append("<table><thead><tr><th>Kural</th><th>Şiddet</th><th>Kenar</th><th>Konum</th><th>Açıklama</th></tr></thead><tbody>");
            foreach (var violation in report.Violations)
            {
                var edge = violation.Cycle is { Count: > 0 }
                    ? string.Join(" → ", violation.Cycle.Append(violation.Cycle[0]))
                    : $"{violation.From} → {violation.To}";
                var location = string.IsNullOrWhiteSpace(violation.FilePath)
                    ? "—"
                    : violation.Line is int line
                        ? $"{violation.FilePath}:{line}"
                        : violation.FilePath;
                sb.Append("<tr><td>").Append(Encode(violation.RuleId)).Append("</td>");
                sb.Append("<td class=\"sev ").Append(violation.Severity.ToString().ToLowerInvariant()).Append("\">")
                    .Append(Encode(violation.Severity.ToString())).Append("</td>");
                sb.Append("<td>").Append(Encode(edge)).Append("</td>");
                sb.Append("<td>").Append(Encode(location)).Append("</td>");
                sb.Append("<td>").Append(Encode(violation.Message)).Append("</td></tr>");
            }

            sb.Append("</tbody></table>");
        }

        sb.Append("<h2>Etki alanı</h2>");
        sb.Append("<table><thead><tr><th>Proje</th><th>Doğrudan bağımlı</th><th>Geçişli etki</th></tr></thead><tbody>");
        foreach (var project in report.Graph.Projects)
        {
            var dependents = report.Graph.Dependents(project.Name);
            var blast = report.Graph.TransitiveDependents(project.Name);
            sb.Append("<tr><td>").Append(Encode(project.Name)).Append("</td>");
            sb.Append("<td>").Append(dependents.Count).Append("</td>");
            sb.Append("<td>").Append(Encode(blast.Count == 0 ? "—" : string.Join(", ", blast))).Append("</td></tr>");
        }

        sb.Append("</tbody></table></main></body></html>");
        return sb.ToString();
    }

    private static string Card(string label, string value, string? extraClass)
    {
        var cls = string.IsNullOrEmpty(extraClass) ? "card" : "card " + extraClass;
        return $"<div class=\"{cls}\"><span>{Encode(label)}</span><b>{Encode(value)}</b></div>";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");
}
