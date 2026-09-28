namespace Archon.Core;

public static class PackageVersion
{
    public static int Compare(string left, string right)
    {
        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);

        var length = Math.Max(leftCore.Length, rightCore.Length);
        for (var i = 0; i < length; i++)
        {
            var a = i < leftCore.Length ? leftCore[i] : 0;
            var b = i < rightCore.Length ? rightCore[i] : 0;
            if (a != b)
                return a.CompareTo(b);
        }

        if (leftPre is null && rightPre is null)
            return 0;
        if (leftPre is null)
            return 1;
        if (rightPre is null)
            return -1;
        return string.Compare(leftPre, rightPre, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryNormalize(string? version, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var trimmed = version.Trim().TrimStart('[', '(').Split(',')[0].TrimEnd(']', ')').Trim();
        if (trimmed.Length == 0 || !char.IsDigit(trimmed[0]))
            return false;

        normalized = trimmed;
        return true;
    }

    private static (long[] Core, string? Prerelease) Split(string version)
    {
        var withoutMetadata = version.Split('+')[0];
        var dash = withoutMetadata.IndexOf('-');
        var core = dash < 0 ? withoutMetadata : withoutMetadata[..dash];
        var prerelease = dash < 0 ? null : withoutMetadata[(dash + 1)..];
        var parts = core
            .Split('.')
            .Select(part => long.TryParse(part, out var value) ? value : 0)
            .ToArray();
        return (parts, string.IsNullOrWhiteSpace(prerelease) ? null : prerelease);
    }
}
