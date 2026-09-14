using Archon.Core;

namespace Archon.Core.Tests;

public sealed class GlobPatternTests
{
    [Theory]
    [InlineData("*.Domain", "Contoso.Domain")]
    [InlineData("*Catalog*", "Contoso.Catalog")]
    [InlineData("Contoso.Api", "Contoso.Api")]
    [InlineData("Contoso.*", "Contoso.Payments")]
    public void Matches_expected_names(string pattern, string value)
    {
        Assert.True(GlobPattern.IsMatch(pattern, value));
    }

    [Theory]
    [InlineData("*.Domain", "Contoso.Application")]
    [InlineData("*Payments*", "Contoso.Catalog")]
    public void Rejects_other_names(string pattern, string value)
    {
        Assert.False(GlobPattern.IsMatch(pattern, value));
    }
}
