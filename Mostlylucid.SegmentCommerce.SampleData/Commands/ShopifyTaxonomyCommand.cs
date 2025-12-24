using System.ComponentModel;
using System.Text.Json;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Mostlylucid.SegmentCommerce.SampleData.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class ShopifyTaxonomySettings : CommandSettings
{
    [CommandOption("-d|--data-path <PATH>")]
    [Description("Path to Shopify taxonomy data directory")]
    [DefaultValue(@"D:\segmentdata")]
    public string DataPath { get; set; } = @"D:\segmentdata";

    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory for generated products")]
    [DefaultValue(@"D:\segmentdata")]
    public string OutputPath { get; set; } = @"D:\segmentdata";

    [CommandOption("-c|--categories <COUNT>")]
    [Description("Number of random categories to use")]
    [DefaultValue(10)]
    public int CategoryCount { get; set; } = 10;

    [CommandOption("-p|--products <COUNT>")]
    [Description("Products per category")]
    [DefaultValue(5)]
    public int ProductsPerCategory { get; set; } = 5;

    [CommandOption("--no-llm")]
    [Description("Skip LLM enhancement (faster but less creative)")]
    public bool NoLlm { get; set; }

    [CommandOption("--llm-model <MODEL>")]
    [Description("Ollama model to use")]
    [DefaultValue("llama3.2:3b")]
    public string LlmModel { get; set; } = "llama3.2:3b";

    [CommandOption("--verticals <NAMES>")]
    [Description("Comma-separated list of verticals to use (empty = all)")]
    public string? Verticals { get; set; }

    [CommandOption("--stats-only")]
    [Description("Only show taxonomy statistics, don't generate")]
    public bool StatsOnly { get; set; }
}

public class ShopifyTaxonomyCommand : AsyncCommand<ShopifyTaxonomySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ShopifyTaxonomySettings settings)
    {
        AnsiConsole.Write(new FigletText("Shopify Tax").Color(Color.Green));
        AnsiConsole.MarkupLine("[bold]Shopify Taxonomy Product Generator[/]");
        AnsiConsole.WriteLine();

        try
        {
            var reader = new ShopifyTaxonomyReader(settings.DataPath);

            if (settings.StatsOnly)
            {
                return await ShowStatsAsync(reader);
            }

            return await GenerateProductsAsync(reader, settings);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task<int> ShowStatsAsync(ShopifyTaxonomyReader reader)
    {
        var stats = await reader.GetStatsAsync();

        AnsiConsole.MarkupLine($"[bold]Taxonomy Version:[/] {stats.Version}");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Vertical");
        table.AddColumn("Prefix");
        table.AddColumn("Total Categories");
        table.AddColumn("Leaf Categories");

        foreach (var vertical in stats.VerticalStats.OrderBy(v => v.Name))
        {
            table.AddRow(
                Markup.Escape(vertical.Name),
                vertical.Prefix,
                vertical.TotalCategories.ToString(),
                vertical.LeafCategories.ToString()
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Total Verticals:[/] {stats.TotalVerticals}");
        AnsiConsole.MarkupLine($"[bold]Total Categories:[/] {stats.TotalCategories}");
        AnsiConsole.MarkupLine($"[bold]Leaf Categories:[/] {stats.LeafCategories}");
        AnsiConsole.MarkupLine($"[bold]Max Depth:[/] {stats.MaxDepth}");

        return 0;
    }

    private async Task<int> GenerateProductsAsync(ShopifyTaxonomyReader reader, ShopifyTaxonomySettings settings)
    {
        // Show configuration
        var configTable = new Table().Border(TableBorder.Rounded);
        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");
        configTable.AddRow("Data path", settings.DataPath);
        configTable.AddRow("Output path", settings.OutputPath);
        configTable.AddRow("Categories", settings.CategoryCount.ToString());
        configTable.AddRow("Products per category", settings.ProductsPerCategory.ToString());
        configTable.AddRow("Total products", (settings.CategoryCount * settings.ProductsPerCategory).ToString());
        configTable.AddRow("LLM Model", settings.NoLlm ? "[dim]disabled[/]" : settings.LlmModel);
        configTable.AddRow("Verticals filter", string.IsNullOrEmpty(settings.Verticals) ? "[dim]all[/]" : settings.Verticals);
        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        // Create generator
        var genConfig = new GenerationConfig
        {
            OllamaBaseUrl = "http://localhost:11434",
            OllamaModel = settings.LlmModel,
            OllamaTimeoutSeconds = 60,
            ProductsPerCategory = settings.ProductsPerCategory
        };

        var httpClient = new HttpClient();
        var generator = new ShopifyProductGenerator(httpClient, genConfig, reader);

        // Check Ollama
        if (!settings.NoLlm)
        {
            var ollamaAvailable = await generator.IsOllamaAvailableAsync();
            if (!ollamaAvailable)
            {
                AnsiConsole.MarkupLine("[yellow]Warning: Ollama not available, using basic descriptions[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Ollama connected[/]");
            }
        }

        // Get categories
        List<ShopifyCategory> categories;
        if (!string.IsNullOrEmpty(settings.Verticals))
        {
            var verticalNames = settings.Verticals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            categories = await reader.GetRandomCategoriesFromVerticalsAsync(
                verticalNames,
                settings.CategoryCount / verticalNames.Length + 1);
            categories = categories.Take(settings.CategoryCount).ToList();
        }
        else
        {
            categories = await reader.GetRandomCategoriesAsync(settings.CategoryCount);
        }

        AnsiConsole.MarkupLine($"[blue]Selected {categories.Count} categories for generation[/]");
        AnsiConsole.WriteLine();

        // Show selected categories
        var categoryTable = new Table().Border(TableBorder.Simple);
        categoryTable.AddColumn("#");
        categoryTable.AddColumn("Category Path");
        categoryTable.AddColumn("Attributes");

        for (int i = 0; i < categories.Count; i++)
        {
            var cat = categories[i];
            var attrCount = cat.Attributes.Count;
            categoryTable.AddRow(
                (i + 1).ToString(),
                Markup.Escape(cat.FullName),
                attrCount > 0 ? attrCount.ToString() : "[dim]none[/]"
            );
        }

        AnsiConsole.Write(categoryTable);
        AnsiConsole.WriteLine();

        // Generate products with progress
        var products = new List<GeneratedProduct>();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating products[/]", maxValue: categories.Count);

                foreach (var category in categories)
                {
                    task.Description = $"[green]Generating:[/] {Markup.Escape(category.Name)}";

                    var categoryProducts = await generator.GenerateProductsForCategoryAsync(
                        category,
                        settings.ProductsPerCategory,
                        !settings.NoLlm);

                    products.AddRange(categoryProducts);
                    task.Increment(1);
                }

                task.Description = "[green]Generation complete![/]";
            });

        // Save products
        Directory.CreateDirectory(settings.OutputPath);
        var outputFile = Path.Combine(settings.OutputPath, "shopify-products.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        await File.WriteAllTextAsync(outputFile, JsonSerializer.Serialize(products, options));
        AnsiConsole.MarkupLine($"[dim]Saved {products.Count} products to {outputFile}[/]");

        // Print summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Generation Complete[/]"));

        var summaryTable = new Table().Border(TableBorder.Rounded);
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn("Value");
        summaryTable.AddRow("Total Products", products.Count.ToString());
        summaryTable.AddRow("Categories Used", categories.Count.ToString());
        summaryTable.AddRow("Avg Price", products.Average(p => p.Price).ToString("C"));
        summaryTable.AddRow("Trending", products.Count(p => p.IsTrending).ToString());
        summaryTable.AddRow("Featured", products.Count(p => p.IsFeatured).ToString());
        summaryTable.AddRow("On Sale", products.Count(p => p.OriginalPrice.HasValue).ToString());
        AnsiConsole.Write(summaryTable);

        // Show sample products
        if (products.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Sample Products:[/]");

            foreach (var sample in products.Take(3))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(sample.Name)}[/]");
                AnsiConsole.MarkupLine($"  [dim]Category:[/] {sample.Category}");
                AnsiConsole.MarkupLine($"  [dim]Price:[/] {sample.Price:C}" +
                    (sample.OriginalPrice.HasValue ? $" [strikethrough]{sample.OriginalPrice:C}[/]" : ""));
                AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(sample.Description.Length > 100 ? sample.Description[..100] + "..." : sample.Description)}[/]");
            }
        }

        return 0;
    }
}
