namespace Archon.Core;

public sealed record FriendAssembly(
    string ProjectName,
    string Friend,
    string? FilePath = null,
    int? Line = null);

public sealed class FriendIndex
{
    public static FriendIndex Empty { get; } = new([]);

    public FriendIndex(IEnumerable<FriendAssembly> friends)
    {
        Friends = friends.ToArray();
    }

    public IReadOnlyList<FriendAssembly> Friends { get; }
}
