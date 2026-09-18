namespace Archon.Core;

public sealed record PackageReference(string ProjectName, string PackageId, string? Version);

public sealed class PackageIndex
{
    public static PackageIndex Empty { get; } = new([]);

    public PackageIndex(IEnumerable<PackageReference> packages)
    {
        Packages = packages.ToArray();
    }

    public IReadOnlyList<PackageReference> Packages { get; }
}
