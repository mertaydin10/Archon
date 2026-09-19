# Archon — Mimari Jandarma

.NET solution’ındaki proje referanslarını, NuGet paketlerini ve `using` satırlarını okur, senin yazdığın kurallara vurur, ihlalde CI’ı kırmızıya çevirir.

## Ne yapar

- `deny` / `allow` / `namespace-deny` / `package-deny` / `layers` / `acyclic`
- `max-fanout` — giden bağımlılık üst sınırı
- `sdp` — Stable Dependencies Principle (kararlı modül kararsıza bağlanamaz)
- Ce / Ca / I bağlaşım metrikleri
- `includes` ile kural dosyası birleştirme
- Mermaid / DOT graf (`archon graph`)
- Directory.Packages.props (merkezi paket sürümü)
- Baseline, SARIF, HTML, Markdown

## Çalıştırma

```bash
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --rules archon.yaml
dotnet run --project src/Archon.Cli -- graph --format mermaid --out artifacts/graph.mmd
dotnet run --project src/Archon.Cli -- explain Contoso.Domain
dotnet run --project src/Archon.Cli -- validate archon.yaml
dotnet run --project src/Archon.Cli -- analyze Archon.sln --rules archon.self.yaml
dotnet test
```

`samples/ContosoShop` kasıtlı olarak bozuk. `archon.yaml` paket kurallarını `rules/packages.yaml` dosyasından include eder.

```csharp
using Contoso.Infrastructure; // archon:ignore domain-no-infra-namespace
```
