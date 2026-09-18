# Archon — Mimari Jandarma

.NET solution’ındaki proje referanslarını, NuGet paketlerini ve `using` satırlarını okur, senin yazdığın kurallara vurur, ihlalde CI’ı kırmızıya çevirir.

Kişisel bir CRUD uygulaması değil: **mimari güvenlik duvarı**. Catalog’un Payments’ı tanıması, Domain’in EF/JSON paketi alması veya gizli bir döngü — bunlar code review’da kaçabilir; Archon kaçırmaz.

## Ne yapar

1. `.sln` ve `.csproj` dosyalarından yönlü bir bağımlılık grafı çıkarır.
2. Kaynak dosyalardaki `using` yönergelerini tarar (`// archon:ignore` ile satır bastırılabilir).
3. `PackageReference` listesini okur.
4. `archon.yaml` içindeki kuralları uygular:
   - `deny` / `allow` — proje kenarları
   - `namespace-deny` — kaynak import
   - `package-deny` — NuGet
   - `layers` — dış katman içe gider
   - `acyclic` — döngü yok
5. Konsol, JSON, HTML, SARIF veya Markdown rapor üretir.
6. `--baseline` ile bilinen ihlalleri dondurur; yalnızca yeni kaçaklar CI’ı kırar.
7. `explain` etki alanını, `validate` YAML’ı kontrol eder.

## Çalıştırma

```bash
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --rules archon.yaml
dotnet run --project src/Archon.Cli -- analyze --format markdown --out artifacts/archon.md
dotnet run --project src/Archon.Cli -- analyze --write-baseline artifacts/baseline.json
dotnet run --project src/Archon.Cli -- analyze --baseline artifacts/baseline.json
dotnet run --project src/Archon.Cli -- explain Contoso.Domain
dotnet run --project src/Archon.Cli -- validate archon.yaml
dotnet run --project src/Archon.Cli -- analyze Archon.sln --rules archon.self.yaml
dotnet test
```

`samples/ContosoShop` kasıtlı olarak bozuk. Solution XML olarak okunur; MSBuild döngü yüzünden derlemeyi reddedebilir — bu beklenen.

Satır içi muafiyet:

```csharp
using Contoso.Infrastructure; // archon:ignore domain-no-infra-namespace
```

## Dizin

- `src/Archon.Core` — graf, glob, kural motoru, baseline
- `src/Archon.Analysis` — sln/csproj/YAML/kaynak/paket, raporlar
- `src/Archon.Cli` — `analyze` / `explain` / `validate` / `init`
- `archon.self.yaml` — Archon’un kendi katman kuralları
