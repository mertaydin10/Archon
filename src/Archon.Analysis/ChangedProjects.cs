using System.Diagnostics;
using Archon.Core;

namespace Archon.Analysis;

public static class ChangedProjects
{
    public static IReadOnlySet<string> Resolve(ProjectGraph graph, IEnumerable<string> changedFiles)
    {
        var roots = graph.Projects
            .Where(p => Path.IsPathRooted(p.Path))
            .Select(p => (p.Name, Root: WithSeparator(Path.GetDirectoryName(p.Path) ?? "")))
            .Where(p => p.Root.Length > 1)
            .OrderByDescending(p => p.Root.Length)
            .ToArray();

        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in changedFiles)
        {
            var full = Path.GetFullPath(file);
            foreach (var (name, root) in roots)
            {
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;
                changed.Add(name);
                break;
            }
        }

        return changed;
    }

    public static IReadOnlyList<string> FromGit(string workingDirectory, string since)
    {
        var top = RunGit(workingDirectory, "rev-parse", "--show-toplevel").Trim();
        if (top.Length == 0)
            throw new InvalidOperationException($"'{workingDirectory}' bir git deposu değil.");

        var output = RunGit(top, "diff", "--name-only", since);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(relative => Path.GetFullPath(Path.Combine(top, relative)))
            .ToArray();
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("git başlatılamadı.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} başarısız: {stderr.Trim()}");
        return stdout;
    }

    private static string WithSeparator(string directory) =>
        directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
}
