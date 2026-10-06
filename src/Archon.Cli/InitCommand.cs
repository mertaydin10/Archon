using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Archon.Cli;

internal sealed class InitSettings : CommandSettings
{
    [CommandOption("-f|--force")]
    [Description("Var olan archon.yaml dosyasının üzerine yaz.")]
    public bool Force { get; init; }
}

internal sealed class InitCommand : Command<InitSettings>
{
    protected override int Execute(CommandContext context, InitSettings settings, CancellationToken cancellationToken)
    {
        const string path = "archon.yaml";
        if (File.Exists(path) && !settings.Force)
        {
            AnsiConsole.MarkupLine("[yellow]archon.yaml zaten var. Üzerine yazmak için --force kullan.[/]");
            return 1;
        }

        File.WriteAllText(path, Template);
        AnsiConsole.MarkupLine($"[green]Yazıldı:[/] {Path.GetFullPath(path)}");
        return 0;
    }

    private const string Template =
        """
        name: Contoso Shop
        solution: samples/ContosoShop/ContosoShop.sln

        rules:
          - id: domain-isolation
            kind: deny
            description: Domain, Infrastructure veya Api'ye bağlanamaz.
            from: "*.Domain"
            to:
              - "*.Infrastructure"
              - "*.Api"

          - id: catalog-must-not-know-payments
            kind: deny
            description: Catalog, Payments'a proje referansı veremez.
            from: "*Catalog*"
            to: "*Payments*"

          - id: catalog-surface
            kind: allow
            description: Catalog yalnızca Domain'e bağlanabilir.
            from: "*Catalog*"
            to:
              - "*.Domain"

          - id: domain-no-infra-namespace
            kind: namespace-deny
            description: Domain kaynakları Infrastructure namespace'ini import edemez.
            from: "*.Domain"
            to: "Contoso.Infrastructure*"

          - id: catalog-no-payments-namespace
            kind: namespace-deny
            description: Catalog kaynakları Payments namespace'ini import edemez.
            from: "*Catalog*"
            to: "Contoso.Payments*"

          - id: domain-no-json-package
            kind: package-deny
            description: Domain Newtonsoft.Json alamaz.
            from: "*.Domain"
            packages:
              - Newtonsoft.Json

          - id: clean-architecture
            kind: layers
            description: Dış katman içe bağlanır; tersi yasak.
            layers:
              - "*.Api"
              - "*.Infrastructure"
              - "*.Application"
              - "*.Domain"

          - id: no-cycles
            kind: acyclic
            description: Proje grafı döngü içeremez.

          - id: api-fanout
            kind: max-fanout
            from: "*.Api"
            max: 2

          - id: stable-dependencies
            kind: sdp
            description: Kararlı modül, daha kararsız olana bağlanamaz.

          - id: catalog-payments-isolated
            kind: isolated
            from: "*Catalog*"
            to: "*Payments*"

          - id: newtonsoft-aligned
            kind: version-aligned
            packages:
              - Newtonsoft.Json

          - id: api-must-touch-domain
            kind: must-depend
            from: "*.Api"
            to: "*.Domain"

          - id: domain-fanin
            kind: max-fanin
            from: "*.Domain"
            max: 2

          - id: api-depth
            kind: max-depth
            from: "*.Api"
            max: 3

          - id: tfm-aligned
            kind: tfm-aligned

          - id: catalog-no-infra-reach
            kind: deny-transitive
            from: "*Catalog*"
            to: "*Infrastructure*"

          - id: domain-package-allow
            kind: package-allow
            from: "*.Domain"
            packages:
              - System.*

          - id: payments-no-web-sdk
            kind: sdk-deny
            from: "*Payments*"
            sdks:
              - Microsoft.NET.Sdk.Web

          - id: project-naming
            kind: naming
            patterns:
              - "Contoso.*"

          - id: no-orphans
            kind: no-orphans

          - id: newtonsoft-min-version
            kind: package-min-version
            packages:
              - Newtonsoft.Json
            min: "13.0.1"

          - id: tests-are-leaves
            kind: test-isolation
            description: Üretim kodu test projelerine bağlanamaz.
        """;
}
