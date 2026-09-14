# Archon — Mimari Jandarma

.NET solution’ındaki proje referanslarını okur, senin yazdığın kurallara vurur, ihlalde CI’ı kırmızıya çevirir.

Kişisel bir CRUD uygulaması değil: **mimari güvenlik duvarı**. Catalog’un Payments’ı tanıması, Domain’in Infrastructure’a bağlanması veya gizli bir döngü — bunlar code review’da kaçabilir; Archon kaçırmaz.

## Ne yapar

1. `.sln` ve `.csproj` dosyalarından yönlü bir bağımlılık grafı çıkarır.
2. `archon.yaml` içindeki kuralları uygular:
   - `deny` — şu glob şuraya bağlanamaz
   - `layers` — dış katman içe gider, tersi yasak
   - `acyclic` — proje grafında döngü olamaz
3. Konsol, JSON veya tek dosyalık HTML rapor üretir. Hata varsa çıkış kodu `1`.

## Çalıştırma

```bash
dotnet run --project src/Archon.Cli -- analyze samples/ContosoShop/ContosoShop.sln --rules archon.yaml
dotnet run --project src/Archon.Cli -- analyze --format html --out artifacts/report.html
dotnet test
```

`samples/ContosoShop` kasıtlı olarak bozuk: Domain → Infrastructure (katman + döngü) ve Catalog → Payments. Solution XML olarak okunur; MSBuild döngü yüzünden derlemeyi reddedebilir — bu beklenen.

## Kural örneği

```yaml
rules:
  - id: catalog-must-not-know-payments
    kind: deny
    from: "*Catalog*"
    to: "*Payments*"
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
- `src/Archon.Analysis` — sln/csproj/YAML, HTML/JSON rapor
- `src/Archon.Cli` — `archon analyze` / `archon init`
- `tests/` — motor ve yükleyici testleri
