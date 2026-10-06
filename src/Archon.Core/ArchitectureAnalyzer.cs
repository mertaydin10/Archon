namespace Archon.Core;

public sealed class ArchitectureAnalyzer
{
    public AnalysisReport Analyze(
        ProjectGraph graph,
        RuleSet ruleSet,
        string solutionPath,
        SourceIndex? sources = null,
        PackageIndex? packages = null,
        FriendIndex? friends = null)
    {
        sources ??= SourceIndex.Empty;
        packages ??= PackageIndex.Empty;
        friends ??= FriendIndex.Empty;
        var violations = new List<Violation>();
        foreach (var rule in ruleSet.Rules)
        {
            switch (rule)
            {
                case DenyRule deny:
                    violations.AddRange(EvaluateDeny(graph, deny));
                    break;
                case AllowRule allow:
                    violations.AddRange(EvaluateAllow(graph, allow));
                    break;
                case NamespaceDenyRule namespaces:
                    violations.AddRange(EvaluateNamespaceDeny(namespaces, sources));
                    break;
                case PackageDenyRule packageDeny:
                    violations.AddRange(EvaluatePackageDeny(packageDeny, packages));
                    break;
                case LayerRule layers:
                    violations.AddRange(EvaluateLayers(graph, layers));
                    break;
                case AcyclicRule acyclic:
                    violations.AddRange(EvaluateCycles(graph, acyclic));
                    break;
                case MaxFanoutRule fanout:
                    violations.AddRange(EvaluateFanout(graph, fanout));
                    break;
                case StableDependencyRule sdp:
                    violations.AddRange(EvaluateStableDependencies(graph, sdp));
                    break;
                case IsolatedRule isolated:
                    violations.AddRange(EvaluateIsolated(graph, isolated));
                    break;
                case VersionAlignedRule aligned:
                    violations.AddRange(EvaluateVersionAligned(aligned, packages));
                    break;
                case InternalsDenyRule internals:
                    violations.AddRange(EvaluateInternalsDeny(internals, friends));
                    break;
                case MustDependRule must:
                    violations.AddRange(EvaluateMustDepend(graph, must));
                    break;
                case MaxFaninRule fanin:
                    violations.AddRange(EvaluateFanin(graph, fanin));
                    break;
                case MaxDepthRule depth:
                    violations.AddRange(EvaluateDepth(graph, depth));
                    break;
                case TfmAlignedRule tfm:
                    violations.AddRange(EvaluateTfmAligned(graph, tfm));
                    break;
                case TransitiveDenyRule reach:
                    violations.AddRange(EvaluateTransitiveDeny(graph, reach));
                    break;
                case PackageAllowRule packageAllow:
                    violations.AddRange(EvaluatePackageAllow(packageAllow, packages));
                    break;
                case SdkDenyRule sdk:
                    violations.AddRange(EvaluateSdkDeny(graph, sdk));
                    break;
                case NamingRule naming:
                    violations.AddRange(EvaluateNaming(graph, naming));
                    break;
                case NoOrphansRule orphans:
                    violations.AddRange(EvaluateOrphans(graph, orphans));
                    break;
                case PackageMinVersionRule minVersion:
                    violations.AddRange(EvaluatePackageMinVersion(minVersion, packages));
                    break;
                case TestIsolationRule isolation:
                    violations.AddRange(EvaluateTestIsolation(graph, isolation));
                    break;
            }
        }

        return new AnalysisReport(
            ruleSet.Name,
            solutionPath,
            graph,
            violations
                .OrderBy(v => v.Severity)
                .ThenBy(v => v.RuleId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.From, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.To, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(v => v.Line)
                .ToArray(),
            Metrics: CouplingCalculator.Compute(graph));
    }

    private static IEnumerable<Violation> EvaluateDeny(ProjectGraph graph, DenyRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            if (!MatchesAny(rule.From, edge.From) || !MatchesAny(rule.To, edge.To))
                continue;
            if (IsExcepted(rule.Exceptions, edge.From, edge.To))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                rule.Description,
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateAllow(ProjectGraph graph, AllowRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            if (!MatchesAny(rule.From, edge.From))
                continue;
            if (MatchesAny(rule.To, edge.To))
                continue;
            if (IsExcepted(rule.Exceptions, edge.From, edge.To))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {edge.From} yalnızca izinli hedeflere bağlanabilir; {edge.To} listede yok.",
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateNamespaceDeny(NamespaceDenyRule rule, SourceIndex sources)
    {
        foreach (var import in sources.Imports)
        {
            if (IsSuppressed(import.Suppression, rule.Id))
                continue;
            if (!MatchesAny(rule.From, import.ProjectName) || !MatchesAny(rule.To, import.Namespace))
                continue;
            if (IsExcepted(rule.Exceptions, import.ProjectName, import.Namespace))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {import.ProjectName} → {import.Namespace}",
                import.ProjectName,
                import.Namespace,
                FilePath: import.FilePath,
                Line: import.Line);
        }
    }

    private static IEnumerable<Violation> EvaluatePackageDeny(PackageDenyRule rule, PackageIndex packages)
    {
        foreach (var package in packages.Packages)
        {
            if (!MatchesAny(rule.From, package.ProjectName) || !MatchesAny(rule.Packages, package.PackageId))
                continue;
            if (IsExcepted(rule.Exceptions, package.ProjectName, package.PackageId))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {package.ProjectName} → {package.PackageId}",
                package.ProjectName,
                package.PackageId);
        }
    }

    private static IEnumerable<Violation> EvaluateLayers(ProjectGraph graph, LayerRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            var fromLayer = LayerIndex(rule.Layers, edge.From);
            var toLayer = LayerIndex(rule.Layers, edge.To);
            if (fromLayer is null || toLayer is null)
                continue;
            if (fromLayer.Value <= toLayer.Value)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} İç katman dışa bağlanamaz: {edge.From} ({rule.Layers[fromLayer.Value]}) → {edge.To} ({rule.Layers[toLayer.Value]})",
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateCycles(ProjectGraph graph, AcyclicRule rule)
    {
        foreach (var cycle in graph.FindCycles())
        {
            var path = string.Join(" → ", cycle.Append(cycle[0]));
            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} Döngü: {path}",
                cycle[0],
                cycle[^1],
                cycle);
        }
    }

    private static IEnumerable<Violation> EvaluateFanout(ProjectGraph graph, MaxFanoutRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (!MatchesAny(rule.From, project.Name))
                continue;

            var count = graph.Dependencies(project.Name).Count;
            if (count <= rule.Max)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} {count} giden bağımlılık içeriyor (üst sınır {rule.Max}).",
                project.Name,
                count.ToString());
        }
    }

    private static IEnumerable<Violation> EvaluateStableDependencies(ProjectGraph graph, StableDependencyRule rule)
    {
        var metrics = CouplingCalculator.Compute(graph)
            .ToDictionary(m => m.Project, m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in graph.Edges)
        {
            if (!metrics.TryGetValue(edge.From, out var from) || !metrics.TryGetValue(edge.To, out var to))
                continue;
            if (from.Instability + 0.001 >= to.Instability)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {edge.From} (I={from.Instability:0.00}) daha kararsız {edge.To} (I={to.Instability:0.00}) üzerine bağlanıyor.",
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateIsolated(ProjectGraph graph, IsolatedRule rule)
    {
        foreach (var edge in graph.Edges)
        {
            var forward = MatchesAny(rule.From, edge.From) && MatchesAny(rule.To, edge.To);
            var backward = MatchesAny(rule.From, edge.To) && MatchesAny(rule.To, edge.From);
            if (!forward && !backward)
                continue;
            if (IsExcepted(rule.Exceptions, edge.From, edge.To))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {edge.From} ↔ {edge.To} bağlamları birbirini görmemeli.",
                edge.From,
                edge.To);
        }
    }

    private static IEnumerable<Violation> EvaluateVersionAligned(VersionAlignedRule rule, PackageIndex packages)
    {
        var groups = packages.Packages
            .Where(p => rule.Packages.Count == 0 || MatchesAny(rule.Packages, p.PackageId))
            .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var versions = group
                .Select(p => $"{p.ProjectName} {(p.Version ?? "?")}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var distinctVersions = group
                .Select(p => p.Version ?? "?")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (distinctVersions.Length <= 1)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {group.Key}: {string.Join(", ", versions)}",
                group.Key,
                string.Join(" | ", distinctVersions));
        }
    }

    private static IEnumerable<Violation> EvaluateInternalsDeny(InternalsDenyRule rule, FriendIndex friends)
    {
        foreach (var friend in friends.Friends)
        {
            if (!MatchesAny(rule.From, friend.ProjectName) || !MatchesAny(rule.To, friend.Friend))
                continue;
            if (IsExcepted(rule.Exceptions, friend.ProjectName, friend.Friend))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {friend.ProjectName} InternalsVisibleTo({friend.Friend})",
                friend.ProjectName,
                friend.Friend,
                FilePath: friend.FilePath,
                Line: friend.Line);
        }
    }

    private static IEnumerable<Violation> EvaluateMustDepend(ProjectGraph graph, MustDependRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (!MatchesAny(rule.From, project.Name))
                continue;
            if (rule.Exceptions.Any(ex => GlobPattern.IsMatch(ex.From, project.Name)))
                continue;
            if (graph.Dependencies(project.Name).Any(dep => MatchesAny(rule.To, dep)))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} şunlardan en az birine bağlanmalı: {string.Join(", ", rule.To)}",
                project.Name,
                string.Join(" | ", rule.To));
        }
    }

    private static IEnumerable<Violation> EvaluateFanin(ProjectGraph graph, MaxFaninRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (!MatchesAny(rule.From, project.Name))
                continue;

            var count = graph.Dependents(project.Name).Count;
            if (count <= rule.Max)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} {count} gelen bağımlılık içeriyor (üst sınır {rule.Max}).",
                project.Name,
                count.ToString());
        }
    }

    private static IEnumerable<Violation> EvaluateDepth(ProjectGraph graph, MaxDepthRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (!MatchesAny(rule.From, project.Name))
                continue;

            var depth = graph.LongestSimplePathFrom(project.Name);
            if (depth <= rule.Max)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} en uzun yol {depth} (üst sınır {rule.Max}).",
                project.Name,
                depth.ToString());
        }
    }

    private static IEnumerable<Violation> EvaluateTfmAligned(ProjectGraph graph, TfmAlignedRule rule)
    {
        var projects = graph.Projects
            .Where(p => rule.From.Count == 0 || MatchesAny(rule.From, p.Name))
            .ToArray();
        var distinct = projects
            .Select(p => $"{p.Name} {(p.TargetFramework ?? "?")}")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tfms = projects
            .Select(p => p.TargetFramework ?? "?")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tfms.Length <= 1)
            yield break;

        yield return new Violation(
            rule.Id,
            rule.Severity,
            $"{rule.Description} {string.Join(", ", distinct)}",
            "TargetFramework",
            string.Join(" | ", tfms.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
    }

    private static IEnumerable<Violation> EvaluateTransitiveDeny(ProjectGraph graph, TransitiveDenyRule rule)
    {
        foreach (var from in graph.Projects)
        {
            if (!MatchesAny(rule.From, from.Name))
                continue;

            foreach (var to in graph.Projects)
            {
                if (from.Name.Equals(to.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!MatchesAny(rule.To, to.Name))
                    continue;
                if (IsExcepted(rule.Exceptions, from.Name, to.Name))
                    continue;

                var path = graph.ShortestPath(from.Name, to.Name);
                if (path is null || path.Count < 2)
                    continue;

                yield return new Violation(
                    rule.Id,
                    rule.Severity,
                    $"{rule.Description} Yol: {string.Join(" → ", path)}",
                    from.Name,
                    to.Name);
            }
        }
    }

    private static IEnumerable<Violation> EvaluatePackageAllow(PackageAllowRule rule, PackageIndex packages)
    {
        foreach (var package in packages.Packages)
        {
            if (!MatchesAny(rule.From, package.ProjectName))
                continue;
            if (MatchesAny(rule.Packages, package.PackageId))
                continue;
            if (IsExcepted(rule.Exceptions, package.ProjectName, package.PackageId))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {package.ProjectName} → {package.PackageId} izinli listede yok.",
                package.ProjectName,
                package.PackageId);
        }
    }

    private static IEnumerable<Violation> EvaluateSdkDeny(ProjectGraph graph, SdkDenyRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (!MatchesAny(rule.From, project.Name))
                continue;
            var sdk = project.Sdk ?? "";
            if (string.IsNullOrWhiteSpace(sdk) || !MatchesAny(rule.Sdks, sdk))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} Sdk={sdk}",
                project.Name,
                sdk);
        }
    }

    private static IEnumerable<Violation> EvaluateNaming(ProjectGraph graph, NamingRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (rule.From.Count > 0 && !MatchesAny(rule.From, project.Name))
                continue;
            if (MatchesAny(rule.Patterns, project.Name))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} şu kalıplardan hiçbirine uymuyor: {string.Join(", ", rule.Patterns)}",
                project.Name,
                string.Join(" | ", rule.Patterns));
        }
    }

    private static IEnumerable<Violation> EvaluateOrphans(ProjectGraph graph, NoOrphansRule rule)
    {
        foreach (var project in graph.Projects)
        {
            if (rule.From.Count > 0 && !MatchesAny(rule.From, project.Name))
                continue;
            if (graph.Dependents(project.Name).Count > 0 || IsEntryPoint(project))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {project.Name} hiçbir proje tarafından kullanılmıyor ve giriş noktası değil.",
                project.Name,
                null);
        }
    }

    private static IEnumerable<Violation> EvaluatePackageMinVersion(PackageMinVersionRule rule, PackageIndex packages)
    {
        foreach (var package in packages.Packages)
        {
            if (rule.From.Count > 0 && !MatchesAny(rule.From, package.ProjectName))
                continue;
            if (!MatchesAny(rule.Packages, package.PackageId))
                continue;
            if (!PackageVersion.TryNormalize(package.Version, out var version))
                continue;
            if (PackageVersion.Compare(version, rule.Min) >= 0)
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {package.ProjectName} → {package.PackageId} {version} (en az {rule.Min}).",
                package.ProjectName,
                package.PackageId);
        }
    }

    private static IEnumerable<Violation> EvaluateTestIsolation(ProjectGraph graph, TestIsolationRule rule)
    {
        var patterns = rule.Patterns.Count > 0 ? rule.Patterns : TestIsolationRule.DefaultPatterns;
        foreach (var edge in graph.Edges)
        {
            if (rule.From.Count > 0 && !MatchesAny(rule.From, edge.From))
                continue;
            if (!MatchesAny(patterns, edge.To) || MatchesAny(patterns, edge.From))
                continue;
            if (IsExcepted(rule.Exceptions, edge.From, edge.To))
                continue;

            yield return new Violation(
                rule.Id,
                rule.Severity,
                $"{rule.Description} {edge.From} test projesi {edge.To} projesine bağlanıyor.",
                edge.From,
                edge.To);
        }
    }

    private static bool IsEntryPoint(ProjectNode project) =>
        project.OutputType is not null
            && (project.OutputType.Equals("Exe", StringComparison.OrdinalIgnoreCase)
                || project.OutputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase))
        || project.Sdk is not null && project.Sdk.EndsWith(".Web", StringComparison.OrdinalIgnoreCase)
        || GlobPattern.IsMatch("*Tests*", project.Name);

    private static bool IsSuppressed(string? suppression, string ruleId) =>
        suppression is "*"
        || (suppression is not null && suppression.Equals(ruleId, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAny(IReadOnlyList<string> patterns, string value) =>
        patterns.Any(pattern => GlobPattern.IsMatch(pattern, value));

    private static bool IsExcepted(IReadOnlyList<RuleException> exceptions, string from, string to) =>
        exceptions.Any(ex => GlobPattern.IsMatch(ex.From, from) && GlobPattern.IsMatch(ex.To, to));

    private static int? LayerIndex(IReadOnlyList<string> layers, string projectName)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (GlobPattern.IsMatch(layers[i], projectName))
                return i;
        }

        return null;
    }
}
