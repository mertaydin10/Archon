# Archon — Mimari Jandarma

.NET solution’ındaki proje referanslarını, NuGet paketlerini ve `using` satırlarını okur, senin yazdığın kurallara vurur, ihlalde CI’ı kırmızıya çevirir.

## Ne yapar

- `deny` / `allow` / `namespace-deny` / `package-deny` / `layers` / `acyclic`
- `max-fanout` — giden bağımlılık üst sınırı
- `sdp` — Stable Dependencies Principle (kararlı modül kararsıza bağlanamaz)
- `isolated` — iki bağlam birbirini (her iki yönde) görmesin
- `version-aligned` — aynı NuGet paketinin sürümleri solution genelinde hizalı olsun
- `internals-deny` — yasak `InternalsVisibleTo` dostlukları
- `exclude` ve `--exclude` ile proje grafından çıkarma
- Ce / Ca / I bağlaşım metrikleri (`archon stats`)
- `includes` ile kural dosyası birleştirme
- Mermaid / DOT graf (`archon graph`)
- Directory.Packages.props (merkezi paket sürümü)
- Baseline, SARIF, HTML, Markdown, JUnit

## Çalıştırma

```bash
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --rules archon.yaml
dotnet run --project src/Archon.Cli -- graph --format mermaid --out artifacts/graph.mmd
dotnet run --project src/Archon.Cli -- explain Contoso.Domain
dotnet run --project src/Archon.Cli -- stats samples/ContosoShop/ContosoShop.sln
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --format junit --out artifacts/archon-junit.xml
dotnet run --project src/Archon.Cli -- validate archon.yaml
dotnet run --project src/Archon.Cli -- analyze Archon.sln --rules archon.self.yaml
dotnet test
```

`samples/ContosoShop` kasıtlı olarak bozuk. `archon.yaml` paket kurallarını `rules/packages.yaml` dosyasından include eder.

```csharp
using Contoso.Infrastructure; // archon:ignore domain-no-infra-namespace
```
