using System.ComponentModel;
using System.Text.Json;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Mostlylucid.SegmentCommerce.SampleData.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class GenerateV2Settings : CommandSettings
{
    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory for generated data")]
    [DefaultValue("D:\\segmentdata")]
    public string OutputPath { get; set; } = "D:\\segmentdata";

    [CommandOption("-s|--sellers <COUNT>")]
    [Description("Number of sellers to generate")]
    [DefaultValue(20)]
    public int SellersCount { get; set; } = 20;

    [CommandOption("-p|--products <COUNT>")]
    [Description("Products per seller")]
    [DefaultValue(10)]
    public int ProductsPerSeller { get; set; } = 10;

    [CommandOption("-c|--customers <COUNT>")]
    [Description("Number of customer profiles to generate")]
    [DefaultValue(500)]
    public int CustomersCount { get; set; } = 500;

    [CommandOption("--no-llm")]
    [Description("Skip LLM generation (use fallback templates)")]
    public bool NoLlm { get; set; }

    [CommandOption("--no-images")]
    [Description("Skip image generation")]
    public bool NoImages { get; set; }

    [CommandOption("--no-embeddings")]
    [Description("Skip embedding generation")]
    public bool NoEmbeddings { get; set; }

    [CommandOption("--llm-model <MODEL>")]
    [Description("Ollama model to use (default: llama3.2:3b)")]
    [DefaultValue("llama3.2:3b")]
    public string LlmModel { get; set; } = "llama3.2:3b";

    [CommandOption("--batch-size <SIZE>")]
    [Description("Batch size for generation")]
    [DefaultValue(5)]
    public int BatchSize { get; set; } = 5;
}

public class GenerateV2Command : AsyncCommand<GenerateV2Settings>
{
    private readonly GadgetTaxonomy _taxonomy;

    public GenerateV2Command(GadgetTaxonomy taxonomy)
    {
        _taxonomy = taxonomy;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, GenerateV2Settings settings)
    {
        AnsiConsole.Write(new FigletText("DataGen v2").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold]SegmentCommerce LLM-Powered Data Generator[/]");
        AnsiConsole.WriteLine();

        // Show configuration
        var configTable = new Table().Border(TableBorder.Rounded);
        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");
        configTable.AddRow("Output path", settings.OutputPath);
        configTable.AddRow("Sellers", settings.SellersCount.ToString());
        configTable.AddRow("Products per seller", settings.ProductsPerSeller.ToString());
        configTable.AddRow("Total products", (settings.SellersCount * settings.ProductsPerSeller).ToString());
        configTable.AddRow("Customers", settings.CustomersCount.ToString());
        configTable.AddRow("LLM Model", settings.NoLlm ? "[dim]disabled[/]" : settings.LlmModel);
        configTable.AddRow("Images", settings.NoImages ? "[dim]disabled[/]" : "enabled");
        configTable.AddRow("Embeddings", settings.NoEmbeddings ? "[dim]disabled[/]" : "enabled");
        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        // Ensure output directory exists
        Directory.CreateDirectory(settings.OutputPath);
        Directory.CreateDirectory(Path.Combine(settings.OutputPath, "images"));
        Directory.CreateDirectory(Path.Combine(settings.OutputPath, "Models"));

        // Create services
        var httpClient = new HttpClient();
        
        var llmConfig = new LlmConfig
        {
            BaseUrl = "http://localhost:11434",
            Model = settings.LlmModel,
            TimeoutSeconds = 60,
            MaxTokens = 512,
            Temperature = 0.8
        };

        var embeddingConfig = new EmbeddingConfig
        {
            Enabled = !settings.NoEmbeddings,
            ModelPath = Path.Combine(settings.OutputPath, "Models", "model.onnx"),
            VocabPath = Path.Combine(settings.OutputPath, "Models", "vocab.txt"),
            VectorSize = 384
        };

        var imageConfig = new GenerationConfig
        {
            ComfyUIBaseUrl = "http://localhost:8188",
            OutputPath = Path.Combine(settings.OutputPath, "images"),
            ComfyUITimeoutSeconds = 300,
            ImagesPerProduct = 3,
            ImageWidth = 512,
            ImageHeight = 512
        };

        var genSettings = new GenerationSettings
        {
            OutputPath = settings.OutputPath,
            SellersCount = settings.SellersCount,
            ProductsPerSeller = settings.ProductsPerSeller,
            CustomersCount = settings.CustomersCount,
            BatchSize = settings.BatchSize,
            EnableLlm = !settings.NoLlm,
            EnableEmbeddings = !settings.NoEmbeddings,
            EnableImages = !settings.NoImages
        };

        using var llmService = new LlmService(new HttpClient(), llmConfig);
        using var embeddingService = new EmbeddingService(embeddingConfig);
        using var imageGenerator = new ComfyUIImageGenerator(new HttpClient(), imageConfig);

        using var generator = new DataGenerator(
            llmService,
            embeddingService,
            imageGenerator,
            _taxonomy,
            genSettings);

        try
        {
            // Generate dataset
            var dataset = await generator.GenerateAsync();

            // Save to JSON
            await SaveDatasetAsync(dataset, settings.OutputPath);

            // Print summary
            PrintSummary(dataset);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task SaveDatasetAsync(GeneratedDataset dataset, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        // Save complete dataset
        var datasetPath = Path.Combine(outputPath, "dataset.json");
        await File.WriteAllTextAsync(datasetPath, JsonSerializer.Serialize(dataset, options));
        AnsiConsole.MarkupLine($"[dim]Saved dataset to {datasetPath}[/]");

        // Save sellers separately (for easier import)
        var sellersPath = Path.Combine(outputPath, "sellers.json");
        await File.WriteAllTextAsync(sellersPath, JsonSerializer.Serialize(dataset.Sellers, options));
        AnsiConsole.MarkupLine($"[dim]Saved sellers to {sellersPath}[/]");

        // Save products flat list
        var products = dataset.Sellers.SelectMany(s => s.Products).ToList();
        var productsPath = Path.Combine(outputPath, "products.json");
        await File.WriteAllTextAsync(productsPath, JsonSerializer.Serialize(products, options));
        AnsiConsole.MarkupLine($"[dim]Saved products to {productsPath}[/]");

        // Save customers
        var customersPath = Path.Combine(outputPath, "customers.json");
        await File.WriteAllTextAsync(customersPath, JsonSerializer.Serialize(dataset.Customers, options));
        AnsiConsole.MarkupLine($"[dim]Saved customers to {customersPath}[/]");

        // Save orders (with fake checkout data)
        var ordersPath = Path.Combine(outputPath, "orders.json");
        await File.WriteAllTextAsync(ordersPath, JsonSerializer.Serialize(dataset.Orders, options));
        AnsiConsole.MarkupLine($"[dim]Saved orders to {ordersPath}[/]");
    }

    private void PrintSummary(GeneratedDataset dataset)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Generation Complete[/]"));

        var summaryTable = new Table().Border(TableBorder.Rounded);
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn("Value");
        summaryTable.AddRow("Sellers", dataset.Stats.TotalSellers.ToString());
        summaryTable.AddRow("Products", dataset.Stats.TotalProducts.ToString());
        summaryTable.AddRow("Customers", dataset.Stats.TotalCustomers.ToString());
        summaryTable.AddRow("Orders", dataset.Stats.TotalOrders.ToString());
        summaryTable.AddRow("Images", dataset.Stats.TotalImages.ToString());
        summaryTable.AddRow("Embeddings", dataset.Stats.TotalEmbeddings.ToString());
        summaryTable.AddRow("Duration", dataset.Stats.Duration.ToString(@"hh\:mm\:ss"));
        AnsiConsole.Write(summaryTable);

        // Show sample seller
        if (dataset.Sellers.Any())
        {
            var sample = dataset.Sellers.First();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Sample Seller:[/] {sample.Name}");
            AnsiConsole.MarkupLine($"[dim]{sample.Description}[/]");
            AnsiConsole.MarkupLine($"Products: {sample.Products.Count}, Categories: {string.Join(", ", sample.Categories)}");
        }
    }
}
