namespace Archon.Core;

public sealed record ProjectNode(
    string Name,
    string Path,
    string? TargetFramework,
    string? Sdk = null,
    string? OutputType = null);

public sealed record ProjectEdge(string From, string To);

public sealed class ProjectGraph
{
    private readonly Dictionary<string, ProjectNode> _nodes;
    private readonly Dictionary<string, List<string>> _forward;
    private readonly Dictionary<string, List<string>> _reverse;

    private ProjectGraph(
        Dictionary<string, ProjectNode> nodes,
        Dictionary<string, List<string>> forward,
        Dictionary<string, List<string>> reverse,
        IReadOnlyList<ProjectEdge> edges)
    {
        _nodes = nodes;
        _forward = forward;
        _reverse = reverse;
        Edges = edges;
    }

    public IReadOnlyList<ProjectNode> Projects =>
        _nodes.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<ProjectEdge> Edges { get; }

    public static ProjectGraph Create(IEnumerable<ProjectNode> nodes, IEnumerable<ProjectEdge> edges)
    {
        var nodeMap = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
            nodeMap[node.Name] = node;

        var forward = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var uniqueEdges = new List<ProjectEdge>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges)
        {
            if (string.Equals(edge.From, edge.To, StringComparison.OrdinalIgnoreCase))
                continue;

            EnsureNode(nodeMap, edge.From);
            EnsureNode(nodeMap, edge.To);

            var key = edge.From + "\u2192" + edge.To;
            if (!seen.Add(key))
                continue;

            uniqueEdges.Add(edge);
            Add(forward, edge.From, edge.To);
            Add(reverse, edge.To, edge.From);
        }

        foreach (var name in nodeMap.Keys)
        {
            if (!forward.ContainsKey(name))
                forward[name] = [];
            if (!reverse.ContainsKey(name))
                reverse[name] = [];
        }

        return new ProjectGraph(nodeMap, forward, reverse, uniqueEdges);
    }

    public bool Contains(string name) => _nodes.ContainsKey(name);

    public ProjectNode Get(string name) => _nodes[name];

    public IReadOnlyList<string> Dependencies(string name) =>
        _forward.TryGetValue(name, out var list) ? list : [];

    public IReadOnlyList<string> Dependents(string name) =>
        _reverse.TryGetValue(name, out var list) ? list : [];

    public IReadOnlyList<string> TransitiveDependents(string name)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
        var queue = new Queue<string>();
        queue.Enqueue(name);

        while (queue.Count > 0)
        {
            foreach (var dependent in Dependents(queue.Dequeue()))
            {
                if (!seen.Add(dependent))
                    continue;
                found.Add(dependent);
                queue.Enqueue(dependent);
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    public IReadOnlyList<string>? ShortestPath(string from, string to)
    {
        if (!Contains(from) || !Contains(to))
            return null;
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
            return [from];

        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var next in Dependencies(node))
            {
                if (!seen.Add(next))
                    continue;
                previous[next] = node;
                if (next.Equals(to, StringComparison.OrdinalIgnoreCase))
                    return Reconstruct(from, to, previous);
                queue.Enqueue(next);
            }
        }

        return null;
    }

    public int LongestSimplePathFrom(string name)
    {
        if (!Contains(name))
            return 0;

        var best = 0;
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };

        void Visit(string node, int depth)
        {
            if (depth > best)
                best = depth;

            foreach (var next in Dependencies(node))
            {
                if (!visiting.Add(next))
                    continue;
                Visit(next, depth + 1);
                visiting.Remove(next);
            }
        }

        Visit(name, 0);
        return best;
    }

    public ProjectGraph Exclude(IReadOnlyList<string> globs)
    {
        if (globs.Count == 0)
            return this;

        bool Drop(string name) => globs.Any(glob => GlobPattern.IsMatch(glob, name));
        var nodes = Projects.Where(p => !Drop(p.Name)).ToArray();
        var keep = nodes.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = Edges.Where(e => keep.Contains(e.From) && keep.Contains(e.To)).ToArray();
        return Create(nodes, edges);
    }

    public IReadOnlyList<IReadOnlyList<string>> FindCycles()
    {
        const int visiting = 1;
        const int done = 2;
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        var cycles = new List<IReadOnlyList<string>>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string node)
        {
            state[node] = visiting;
            stack.Add(node);

            foreach (var next in Dependencies(node))
            {
                state.TryGetValue(next, out var nextState);
                if (nextState == 0)
                {
                    Visit(next);
                }
                else if (nextState == visiting)
                {
                    var start = stack.FindIndex(x => x.Equals(next, StringComparison.OrdinalIgnoreCase));
                    if (start < 0)
                        continue;

                    var cycle = stack.Skip(start).ToArray();
                    var normalized = NormalizeCycle(cycle);
                    if (unique.Add(string.Join("→", normalized)))
                        cycles.Add(normalized);
                }
            }

            stack.RemoveAt(stack.Count - 1);
            state[node] = done;
        }

        foreach (var project in Projects)
        {
            state.TryGetValue(project.Name, out var current);
            if (current == 0)
                Visit(project.Name);
        }

        return cycles;
    }

    private static void EnsureNode(Dictionary<string, ProjectNode> nodes, string name)
    {
        if (!nodes.ContainsKey(name))
            nodes[name] = new ProjectNode(name, name, null);
    }

    private static void Add(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        if (!list.Exists(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)))
            list.Add(value);
    }

    private static IReadOnlyList<string> Reconstruct(
        string from,
        string to,
        Dictionary<string, string> previous)
    {
        var path = new List<string> { to };
        var current = to;
        while (!current.Equals(from, StringComparison.OrdinalIgnoreCase))
        {
            current = previous[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static IReadOnlyList<string> NormalizeCycle(IReadOnlyList<string> cycle)
    {
        if (cycle.Count == 0)
            return cycle;

        var min = 0;
        for (var i = 1; i < cycle.Count; i++)
        {
            if (string.Compare(cycle[i], cycle[min], StringComparison.OrdinalIgnoreCase) < 0)
                min = i;
        }

        var rotated = new string[cycle.Count];
        for (var i = 0; i < cycle.Count; i++)
            rotated[i] = cycle[(min + i) % cycle.Count];
        return rotated;
    }
}
