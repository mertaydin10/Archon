namespace Archon.Core;

public sealed record ProjectExplanation(
    string Project,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Dependents,
    IReadOnlyList<string> BlastRadius,
    IReadOnlyList<Violation> RelatedViolations);

public static class ProjectExplainer
{
    public static ProjectExplanation Explain(
        ProjectGraph graph,
        string project,
        IReadOnlyList<Violation>? violations = null)
    {
        if (!graph.Contains(project))
            throw new InvalidOperationException($"'{project}' solution grafında yok.");

        var related = (violations ?? [])
            .Where(v => Touches(v, project))
            .ToArray();

        return new ProjectExplanation(
            project,
            graph.Dependencies(project),
            graph.Dependents(project),
            graph.TransitiveDependents(project),
            related);
    }

    private static bool Touches(Violation violation, string project)
    {
        if (violation.From?.Equals(project, StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (violation.To?.Equals(project, StringComparison.OrdinalIgnoreCase) == true)
            return true;
        return violation.Cycle?.Any(name => name.Equals(project, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
