using Contoso.Payments;

namespace Contoso.Catalog;

public sealed class Marker
{
    public static string Leak() => typeof(Contoso.Payments.Marker).FullName!;
}
