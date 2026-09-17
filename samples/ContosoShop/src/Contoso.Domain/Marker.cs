using Contoso.Infrastructure;

namespace Contoso.Domain;

public sealed class Marker
{
    public static string Leak() => typeof(Contoso.Infrastructure.Marker).FullName!;
}
