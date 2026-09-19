namespace Archon.Core;

public sealed record CouplingMetric(string Project, int Ce, int Ca, double Instability);

public static class CouplingCalculator
{
    public static IReadOnlyList<CouplingMetric> Compute(ProjectGraph graph)
    {
        return graph.Projects
            .Select(project =>
            {
                var ce = graph.Dependencies(project.Name).Count;
                var ca = graph.Dependents(project.Name).Count;
                var instability = ce + ca == 0 ? 0d : (double)ce / (ce + ca);
                return new CouplingMetric(project.Name, ce, ca, instability);
            })
            .ToArray();
    }

    public static CouplingMetric Of(ProjectGraph graph, string project) =>
        Compute(graph).First(m => m.Project.Equals(project, StringComparison.OrdinalIgnoreCase));
}
