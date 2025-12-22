using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Mostlylucid.SegmentCommerce.SampleData.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class GenerateSettings : CommandSettings
{
    [CommandOption("-c|--category <CATEGORY>")]
    [Description("Generate products for a specific category only (tech, fashion, home, sport, books, food)")]
    public string? Category { get; set; }

    [CommandOption("-n|--count <COUNT>")]
    [Description("Number of products to generate per category")]
    [DefaultValue(10)]
    public int Count { get; set; } = 10;

    [CommandOption("--no-ollama")]
    [Description("Skip Ollama enhancement (use taxonomy-only generation)")]
    public bool NoOllama { get; set; }

    [CommandOption("--no-images")]
    [Description("Skip image generation with ComfyUI")]
    public bool NoImages { get; set; }

    [CommandOption("-o|--output <PATH>")]
    [Description("Output directory for generated data")]
    [DefaultValue("./Output")]
    public string OutputPath { get; set; } = "./Output";

    [CommandOption("--db")]
    [Description("Write products directly to the database")]
    public bool WriteToDatabase { get; set; }

    [CommandOption("--connection <STRING>")]
    [Description("Database connection string (overrides appsettings)")]
    public string? ConnectionString { get; set; }

    [CommandOption("--dry-run")]
    [Description("Preview what would be generated without writing anything")]
    public bool DryRun { get; set; }
}

    public class GenerateCommand : AsyncCommand<GenerateSettings>
    {
        private readonly GenerationConfig _config;
        private readonly GadgetTaxonomy _taxonomy;
        private readonly ProfileGenerator _profileGenerator;

        public GenerateCommand(GenerationConfig config, GadgetTaxonomy taxonomy)
        {
            _config = config;
            _taxonomy = taxonomy;
            _profileGenerator = new ProfileGenerator(taxonomy);
        }


    public override async Task<int> ExecuteAsync(CommandContext context, GenerateSettings settings)
    {
        AnsiConsole.Write(new FigletText("SampleData").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[bold]SegmentCommerce Product Generator[/]");
        AnsiConsole.WriteLine();

        // Validate category if specified
        if (!string.IsNullOrEmpty(settings.Category) && !_taxonomy.Categories.ContainsKey(settings.Category))
        {
            AnsiConsole.MarkupLine($"[red]Unknown category: {settings.Category}[/]");
            AnsiConsole.MarkupLine($"Available: {string.Join(", ", _taxonomy.Categories.Keys)}");
            return 1;
        }

        // Show configuration
        var configTable = new Table().Border(TableBorder.Rounded);
        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");
        configTable.AddRow("Categories", settings.Category ?? "All");
        configTable.AddRow("Products per category", settings.Count.ToString());
        configTable.AddRow("Profiles", (settings.Count * 2).ToString());
        configTable.AddRow("Use Ollama", (!settings.NoOllama).ToString());
        configTable.AddRow("Generate images", (!settings.NoImages).ToString());
        configTable.AddRow("Write to database", settings.WriteToDatabase.ToString());
        configTable.AddRow("Output path", settings.OutputPath);
        configTable.AddRow("Dry run", settings.DryRun.ToString());
        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Dry run mode - no data will be written[/]");
            await PreviewGenerationAsync(settings);
            return 0;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(settings.OutputPath);

        // Generate products
        var products = await GenerateProductsAsync(settings);

        // Generate synthetic profiles tied to categories
        var profiles = GenerateProfiles(settings);

        if (products.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No products generated[/]");
            return 1;
        }

        // Generate images if requested
        if (!settings.NoImages)
        {
            await GenerateImagesAsync(products, settings);
        }

        // Save to JSON
        await SaveToJsonAsync(products, profiles, settings);

        // Write to database if requested
        if (settings.WriteToDatabase)
        {
            await WriteToDatabaseAsync(products, profiles, settings);
        }

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Generated {products.Values.Sum(p => p.Count)} products across {products.Count} categories[/]");

        return 0;
    }

    private async Task PreviewGenerationAsync(GenerateSettings settings)
    {
        var categories = string.IsNullOrEmpty(settings.Category)
            ? _taxonomy.Categories.Keys.ToList()
            : new List<string> { settings.Category };

        foreach (var categorySlug in categories)
        {
            var category = _taxonomy.Categories[categorySlug];
            var productTypes = category.Subcategories.Values
                .SelectMany(s => s.Products)
                .ToList();

            AnsiConsole.MarkupLine($"\n[bold]{category.DisplayName}[/]");
            AnsiConsole.MarkupLine($"  Product types: {productTypes.Count}");
            AnsiConsole.MarkupLine($"  Would generate: {settings.Count} products");

            var table = new Table().Border(TableBorder.Simple);
            table.AddColumn("Type");
            table.AddColumn("Variants");
            table.AddColumn("Price Range");

            foreach (var pt in productTypes.Take(5))
            {
                var range = pt.PriceRange ?? category.PriceRange;
                table.AddRow(
                    pt.Type,
                    string.Join(", ", pt.Variants.Take(3)) + (pt.Variants.Count > 3 ? "..." : ""),
                    $"£{range.Min:F2} - £{range.Max:F2}");
            }

            if (productTypes.Count > 5)
            {
                table.AddRow($"... and {productTypes.Count - 5} more", "", "");
            }

            AnsiConsole.Write(table);
        }
    }

    private async Task<Dictionary<string, List<GeneratedProduct>>> GenerateProductsAsync(GenerateSettings settings)
    {
        var httpClient = new HttpClient();
        var generator = new TaxonomyProductGenerator(httpClient, _config, _taxonomy);

        var categories = string.IsNullOrEmpty(settings.Category)
            ? _taxonomy.Categories.Keys.ToList()
            : new List<string> { settings.Category };

        var result = new Dictionary<string, List<GeneratedProduct>>();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating products[/]", maxValue: categories.Count);

                foreach (var categorySlug in categories)
                {
                    task.Description = $"[green]Generating {categorySlug}[/]";

                    var products = await generator.GenerateProductsAsync(
                        categorySlug,
                        settings.Count,
                        useOllama: !settings.NoOllama);

                    result[categorySlug] = products;
                    task.Increment(1);
                }
            });

        return result;
    }

    private List<GeneratedProfile> GenerateProfiles(GenerateSettings settings)
    {
        var categories = string.IsNullOrEmpty(settings.Category)
            ? _taxonomy.Categories.Keys.ToArray()
            : new[] { settings.Category };

        // simple heuristic: 2 profiles per product requested per category
        var profileCount = Math.Max(10, settings.Count * 2 * (categories.Count()));
        return _profileGenerator.GenerateProfiles(profileCount, categories);
    }

    private async Task GenerateImagesAsync(
        Dictionary<string, List<GeneratedProduct>> products,
        GenerateSettings settings)
    {
        var httpClient = new HttpClient();
        var imageConfig = _config with { OutputPath = Path.Combine(settings.OutputPath, "images") };
        var imageGenerator = new ComfyUIImageGenerator(httpClient, imageConfig);

        if (!await imageGenerator.IsAvailableAsync())
        {
            AnsiConsole.MarkupLine("[yellow]ComfyUI not available - skipping image generation[/]");
            AnsiConsole.MarkupLine("[dim]Start ComfyUI with: docker compose -f docker-compose.comfyui.yml up[/]");
            return;
        }

        var allProducts = products.Values.SelectMany(p => p).ToList();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[blue]Generating images[/]", maxValue: allProducts.Count);

                foreach (var product in allProducts)
                {
                    task.Description = $"[blue]Imaging: {product.Name.Truncate(30)}[/]";

                    try
                    {
                        var images = await imageGenerator.GenerateProductImagesAsync(product);
                        product.Images = images;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed: {product.Name}: {ex.Message}[/]");
                    }

                    task.Increment(1);
                }
            });
    }

    private async Task SaveToJsonAsync(
        Dictionary<string, List<GeneratedProduct>> products,
        List<GeneratedProfile> profiles,
        GenerateSettings settings)
    {
        var productsPath = Path.Combine(settings.OutputPath, "products.json");
        var profilesPath = Path.Combine(settings.OutputPath, "profiles.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        await File.WriteAllTextAsync(productsPath, JsonSerializer.Serialize(products, options));
        await File.WriteAllTextAsync(profilesPath, JsonSerializer.Serialize(profiles, options));

        AnsiConsole.MarkupLine($"[dim]Saved products to {productsPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Saved profiles to {profilesPath}[/]");
    }

    private async Task WriteToDatabaseAsync(
        Dictionary<string, List<GeneratedProduct>> products,
        List<GeneratedProfile> profiles,
        GenerateSettings settings)
    {
        var connectionString = settings.ConnectionString ?? _config.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            AnsiConsole.MarkupLine("[red]No database connection string provided[/]");
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<SegmentCommerceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var context = new SegmentCommerceDbContext(optionsBuilder.Options);

        // Ensure database exists
        await context.Database.MigrateAsync();

        var count = 0;

        await AnsiConsole.Status()
            .StartAsync("Writing to database...", async ctx =>
            {
                foreach (var (categorySlug, categoryProducts) in products)
                {
                    foreach (var product in categoryProducts)
                    {
                        var entity = new ProductEntity
                        {
                            Name = product.Name,
                            Description = product.Description,
                            Category = categorySlug,
                            Price = product.Price,
                            OriginalPrice = product.OriginalPrice,
                            ImageUrl = product.Images.FirstOrDefault(i => i.IsPrimary)?.FilePath
                                       ?? product.Images.FirstOrDefault()?.FilePath
                                       ?? $"https://picsum.photos/seed/{product.Name.GetHashCode()}/400/400",
                            Tags = product.Tags,
                            IsTrending = product.IsTrending,
                            IsFeatured = product.IsFeatured
                        };

                        context.Products.Add(entity);
                        count++;
                    }
                }

                // store profiles as interest scores
                foreach (var profile in profiles)
                {
                    var anonProfile = new Mostlylucid.SegmentCommerce.Data.Entities.Profiles.AnonymousProfileEntity
                    {
                        ProfileKey = profile.ProfileKey,
                        TotalWeight = profile.Signals.Sum(s => s.Weight),
                        SignalCount = profile.Signals.Count,
                        CreatedAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.AnonymousProfiles.Add(anonProfile);

                    foreach (var kvp in profile.Interests)
                    {
                        context.InterestScores.Add(new Mostlylucid.SegmentCommerce.Data.Entities.Profiles.InterestScoreEntity
                        {
                            Profile = anonProfile,
                            Category = kvp.Key,
                            Score = kvp.Value,
                            DecayRate = 0.02,
                            LastUpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                await context.SaveChangesAsync();
            });

        AnsiConsole.MarkupLine($"[green]Wrote {count} products and {profiles.Count} profiles to database[/]");
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
