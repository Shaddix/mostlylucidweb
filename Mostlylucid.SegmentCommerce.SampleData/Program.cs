using Microsoft.Extensions.Configuration;
using Mostlylucid.SegmentCommerce.SampleData.Commands;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console.Cli;

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("SAMPLEDATA_")
    .Build();

// Build generation config
var config = new GenerationConfig
{
    OllamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434",
    OllamaModel = configuration["Ollama:Model"] ?? "llama3.2",
    OllamaTimeoutSeconds = int.TryParse(configuration["Ollama:TimeoutSeconds"], out var ollamaTimeout) ? ollamaTimeout : 120,
    ComfyUIBaseUrl = configuration["ComfyUI:BaseUrl"] ?? "http://localhost:8188",
    ComfyUICheckpointName = configuration["ComfyUI:CheckpointName"] ?? "sd_xl_base_1.0.safetensors",
    ComfyUIRefinerName = configuration["ComfyUI:RefinerName"] ?? "sd_xl_refiner_1.0.safetensors",
    OutputPath = configuration["ComfyUI:OutputPath"] ?? "./Output/images",
    ComfyUITimeoutSeconds = int.TryParse(configuration["ComfyUI:TimeoutSeconds"], out var comfyTimeout) ? comfyTimeout : 300,
    ProductsPerCategory = int.TryParse(configuration["Generation:ProductsPerCategory"], out var ppc) ? ppc : 10,
    ImagesPerProduct = int.TryParse(configuration["Generation:ImagesPerProduct"], out var ipp) ? ipp : 3,
    ImageWidth = int.TryParse(configuration["Generation:ImageWidth"], out var iw) ? iw : 512,
    ImageHeight = int.TryParse(configuration["Generation:ImageHeight"], out var ih) ? ih : 512,
    ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty
};

// Load taxonomy
var taxonomyPath = Path.Combine(AppContext.BaseDirectory, "Data", "gadget-taxonomy.json");
GadgetTaxonomy taxonomy;

try
{
    taxonomy = GadgetTaxonomy.Load(taxonomyPath);
}
catch (FileNotFoundException)
{
    // Try relative path for development
    var devPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "gadget-taxonomy.json");
    taxonomy = GadgetTaxonomy.Load(devPath);
}

// Build CLI app
var app = new CommandApp();

app.Configure(cfg =>
{
    cfg.SetApplicationName("sampledata");
    cfg.SetApplicationVersion("1.0.0");

    cfg.AddCommand<GenerateCommand>("generate")
        .WithDescription("Generate sample product data using taxonomy + Ollama + ComfyUI (v1)")
        .WithExample("generate")
        .WithExample("generate", "--category", "tech", "--count", "20")
        .WithExample("generate", "--no-ollama", "--no-images")
        .WithExample("generate", "--db");

    cfg.AddCommand<GenerateV2Command>("gen")
        .WithDescription("LLM-powered data generation with sellers, products, and customers (v2)")
        .WithExample("gen")
        .WithExample("gen", "--sellers", "50", "--products", "20", "--customers", "1000")
        .WithExample("gen", "--no-llm", "--no-images")
        .WithExample("gen", "--output", "D:\\segmentdata");

    cfg.AddCommand<ListCommand>("list")
        .WithDescription("List available categories and product types from the taxonomy")
        .WithExample("list")
        .WithExample("list", "--category", "tech")
        .WithExample("list", "--json");

    cfg.AddCommand<StatusCommand>("status")
        .WithDescription("Check the status of required services (Ollama, ComfyUI, PostgreSQL)");

    cfg.AddCommand<ClearCommand>("clear")
        .WithDescription("Clear all data from the database")
        .WithExample("clear", "--confirm")
        .WithExample("clear", "--keep-categories")
        .WithExample("clear", "--connection", "Host=localhost;...");

    cfg.AddCommand<ImportCommand>("import")
        .WithDescription("Import generated JSON data into the PostgreSQL database")
        .WithExample("import")
        .WithExample("import", "--input", "D:\\segmentdata")
        .WithExample("import", "--clear", "--connection", "Host=localhost;...")
        .WithExample("import", "--no-profiles", "--no-embeddings");

    cfg.AddCommand<ShopifyTaxonomyCommand>("shopify")
        .WithDescription("Generate products using Shopify taxonomy data")
        .WithExample("shopify", "--stats-only")
        .WithExample("shopify", "--categories", "10", "--products", "5")
        .WithExample("shopify", "--verticals", "Electronics,Apparel & Accessories")
        .WithExample("shopify", "--no-llm");
});

// Register dependencies using type registrar
app.Configure(cfg =>
{
    cfg.Settings.Registrar.RegisterInstance(config);
    cfg.Settings.Registrar.RegisterInstance(taxonomy);
});

return await app.RunAsync(args);
