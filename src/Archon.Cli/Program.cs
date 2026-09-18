using Archon.Cli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("archon");
    config.ValidateExamples();
    config.AddCommand<AnalyzeCommand>("analyze")
        .WithDescription("Solution grafını çıkarır ve mimari kuralları uygular.")
        .WithExample("analyze", "samples/ContosoShop/ContosoShop.sln", "--rules", "archon.yaml", "--out", "artifacts/report.html");
    config.AddCommand<ExplainCommand>("explain")
        .WithDescription("Bir projenin bağımlılıklarını, etki alanını ve ilgili ihlalleri gösterir.")
        .WithExample("explain", "Contoso.Domain");
    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("archon.yaml dosyasını çözümlemeden doğrular.");
    config.AddCommand<InitCommand>("init")
        .WithDescription("Örnek archon.yaml dosyası yazar.");
});

return app.Run(args);
