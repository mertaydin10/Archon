namespace Archon.Core;

public sealed record NamespaceImport(
    string ProjectName,
    string Namespace,
    string FilePath,
    int Line,
    string? Suppression = null);

public sealed class SourceIndex
{
    public static SourceIndex Empty { get; } = new([]);

    public SourceIndex(IEnumerable<NamespaceImport> imports)
    {
        Imports = imports.ToArray();
    }

    public IReadOnlyList<NamespaceImport> Imports { get; }
}
