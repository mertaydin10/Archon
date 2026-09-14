using System.Globalization;
using System.Net;
using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class SvgGraphRenderer
{
    public static string Render(ProjectGraph graph, IReadOnlySet<(string From, string To)> violations)
    {
        var layers = InferLayers(graph);
        var rows = layers
            .GroupBy(x => x.Value)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(x => x.Key).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray())
            .ToArray();

        const int width = 1120;
        const int rowHeight = 150;
        const int top = 70;
        var height = Math.Max(280, top + (rows.Length * rowHeight));
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);

        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            var y = top + (r * rowHeight);
            for (var i = 0; i < row.Length; i++)
            {
                var x = row.Length == 1
                    ? width / 2.0
                    : 110 + (i * ((width - 220.0) / (row.Length - 1)));
                positions[row[i]] = (x, y);
            }
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"Proje bağımlılık grafı\">");
        sb.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#8a8478\"/></marker>");
        sb.Append("<marker id=\"arrow-bad\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#c23b22\"/></marker></defs>");

        foreach (var edge in graph.Edges.OrderBy(e => violations.Contains((e.From, e.To))))
        {
            if (!positions.TryGetValue(edge.From, out var from) || !positions.TryGetValue(edge.To, out var to))
                continue;

            var broken = violations.Contains((edge.From, edge.To));
            var midX = (from.X + to.X) / 2;
            var midY = (from.Y + to.Y) / 2 - 24;
            var color = broken ? "#c23b22" : "#8a8478";
            var widthAttr = broken ? "2.4" : "1.2";
            var marker = broken ? "arrow-bad" : "arrow";
            sb.Append(CultureInfo.InvariantCulture,
                $"<path d=\"M {from.X:0.#} {from.Y + 22:0.#} Q {midX:0.#} {midY:0.#} {to.X:0.#} {to.Y - 26:0.#}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{widthAttr}\" marker-end=\"url(#{marker})\" opacity=\"{(broken ? "0.95" : "0.55")}\"/>");
        }

        foreach (var project in graph.Projects)
        {
            if (!positions.TryGetValue(project.Name, out var pos))
                continue;

            var layer = layers[project.Name];
            var fill = LayerFill(layer);
            var label = WebUtility.HtmlEncode(project.Name);
            sb.Append(CultureInfo.InvariantCulture,
                $"<g><rect x=\"{pos.X - 92:0.#}\" y=\"{pos.Y - 22:0.#}\" width=\"184\" height=\"44\" rx=\"8\" fill=\"{fill}\" /><text x=\"{pos.X:0.#}\" y=\"{pos.Y + 5:0.#}\" text-anchor=\"middle\" fill=\"#f4efe6\" font-family=\"Georgia, 'Times New Roman', serif\" font-size=\"12\">{label}</text></g>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static Dictionary<string, int> InferLayers(ProjectGraph graph)
    {
        var layers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in graph.Projects)
        {
            var name = project.Name;
            layers[name] =
                Ends(name, "Api", "Web", "Host") ? 0 :
                Ends(name, "Infrastructure", "Infra", "Data") ? 1 :
                Ends(name, "Application", "App", "Services") ? 2 :
                Ends(name, "Domain", "Core") ? 3 :
                4;
        }

        return layers;
    }

    private static bool Ends(string name, params string[] suffixes) =>
        suffixes.Any(suffix => name.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static string LayerFill(int layer) => layer switch
    {
        0 => "#3f4a5a",
        1 => "#8a5a28",
        2 => "#2f5f5b",
        3 => "#3d5a80",
        _ => "#5c5852"
    };
}
