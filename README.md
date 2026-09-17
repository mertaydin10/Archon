# Archon — Mimari Jandarma

.NET solution’ındaki proje referanslarını ve `using` satırlarını okur, senin yazdığın kurallara vurur, ihlalde CI’ı kırmızıya çevirir.

Kişisel bir CRUD uygulaması değil: **mimari güvenlik duvarı**. Catalog’un Payments’ı tanıması, Domain’in Infrastructure’a bağlanması veya gizli bir döngü — bunlar code review’da kaçabilir; Archon kaçırmaz.

## Ne yapar

1. `.sln` ve `.csproj` dosyalarından yönlü bir bağımlılık grafı çıkarır.
2. Kaynak dosyalardaki `using` yönergelerini tarar.
3. `archon.yaml` içindeki kuralları uygular:
   - `deny` — şu proje glob’u şuraya bağlanamaz
   - `allow` — şu proje yalnızca listedeki hedeflere bağlanabilir
   - `namespace-deny` — şu projenin kaynakları şu namespace’i import edemez
   - `layers` — dış katman içe gider, tersi yasak
   - `acyclic` — proje grafında döngü olamaz
4. Konsol, JSON, HTML veya SARIF rapor üretir. Hata varsa çıkış kodu `1`.
5. `explain` ile bir projenin etki alanını ve ilgili ihlalleri gösterir.

## Çalıştırma

```bash
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --rules archon.yaml
dotnet run --project src/Archon.Cli -- analyze --format html --out artifacts/report.html
dotnet run --project src/Archon.Cli -- analyze --format sarif --out artifacts/archon.sarif
dotnet run --project src/Archon.Cli -- explain Contoso.Domain
dotnet run --project src/Archon.Cli -- analyze Archon.sln --rules archon.self.yaml
dotnet test
```

`samples/ContosoShop` kasıtlı olarak bozuk: Domain → Infrastructure (katman + döngü + namespace), Catalog → Payments (deny + allow + namespace). Solution XML olarak okunur; MSBuild döngü yüzünden derlemeyi reddedebilir — bu beklenen.

## Kural örneği

```yaml
rules:
  - id: catalog-must-not-know-payments
    kind: deny
    from: "*Catalog*"
    to: "*Payments*"
  - id: catalog-surface
    kind: allow
    from: "*Catalog*"
    to:
      - "*.Domain"
  - id: domain-no-infra-namespace
    kind: namespace-deny
    from: "*.Domain"
    to: "Contoso.Infrastructure*"
  - id: clean-architecture
    kind: layers
    layers:
      - "*.Api"
      - "*.Infrastructure"
      - "*.Application"
      - "*.Domain"
```

`except` ile tek kenarı muaf tutabilirsin. Şiddet varsayılanı `error`; `warning` da yazılabilir.

## Dizin

- `src/Archon.Core` — graf, glob, kural motoru
- `src/Archon.Analysis` — sln/csproj/YAML/kaynak tarama, HTML/JSON/SARIF rapor
- `src/Archon.Cli` — `archon analyze` / `explain` / `init`
- `tests/` — motor ve yükleyici testleri
- `archon.self.yaml` — Archon’un kendi katman kuralları
