using System.Text;
using Archon.Core;

namespace Archon.Analysis;

public static class GraphTextWriter
{
    public static string Mermaid(ProjectGraph graph, IReadOnlySet<(string From, string To)>? violations = null)
    {
        violations ??= new HashSet<(string, string)>();
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TB");
        foreach (var project in graph.Projects)
            sb.AppendLine($"    {Id(project.Name)}[\"{project.Name}\"]");

        foreach (var edge in graph.Edges)
        {
            var arrow = violations.Contains((edge.From, edge.To)) ? "-.->|ihlal|" : "-->";
            sb.AppendLine($"    {Id(edge.From)} {arrow} {Id(edge.To)}");
        }

        return sb.ToString();
    }

    public static string Dot(ProjectGraph graph, IReadOnlySet<(string From, string To)>? violations = null)
    {
        violations ??= new HashSet<(string, string)>();
        var sb = new StringBuilder();
        sb.AppendLine("digraph Archon {");
        sb.AppendLine("  rankdir=TB;");
        sb.AppendLine("  node [shape=box, fontname=Helvetica];");
        foreach (var project in graph.Projects)
            sb.AppendLine($"  \"{project.Name}\";");

        foreach (var edge in graph.Edges)
        {
            var extra = violations.Contains((edge.From, edge.To)) ? " [color=\"#c23b22\", style=dashed]" : "";
            sb.AppendLine($"  \"{edge.From}\" -> \"{edge.To}\"{extra};");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Id(string name)
    {
        var chars = name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }
}
