using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Pgvector;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class ImportSettings : CommandSettings
{
    [CommandOption("-i|--input <PATH>")]
    [Description("Input directory containing generated JSON files")]
    [DefaultValue("D:\\segmentdata")]
    public string InputPath { get; set; } = "D:\\segmentdata";

    [CommandOption("-c|--connection <CONNECTION>")]
    [Description("PostgreSQL connection string (overrides appsettings)")]
    public string? ConnectionString { get; set; }

    [CommandOption("--clear")]
    [Description("Clear existing data before import")]
    public bool ClearExisting { get; set; }

    [CommandOption("--no-profiles")]
    [Description("Skip profile import")]
    public bool NoProfiles { get; set; }

    [CommandOption("--no-embeddings")]
    [Description("Skip embedding generation during import")]
    public bool NoEmbeddings { get; set; }

    [CommandOption("--batch-size <SIZE>")]
    [Description("Batch size for database inserts")]
    [DefaultValue(100)]
    public int BatchSize { get; set; } = 100;

    [CommandOption("--store <STORE>")]
    [Description("Store slug to assign products to")]
    [DefaultValue("demo-store")]
    public string StoreSlug { get; set; } = "demo-store";
}

public class ImportCommand : AsyncCommand<ImportSettings>
{
    private readonly GenerationConfig _config;

    public ImportCommand(GenerationConfig config)
    {
        _config = config;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings)
    {
        AnsiConsole.Write(new FigletText("Import").Color(Color.Green));
        AnsiConsole.MarkupLine("[bold]SegmentCommerce Database Import[/]");
        AnsiConsole.WriteLine();

        // Validate input path
        if (!Directory.Exists(settings.InputPath))
        {
            AnsiConsole.MarkupLine($"[red]Input directory not found: {settings.InputPath}[/]");
            return 1;
        }

        // Show configuration
        var configTable = new Table().Border(TableBorder.Rounded);
        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");
        configTable.AddRow("Input path", settings.InputPath);
        configTable.AddRow("Clear existing", settings.ClearExisting ? "[yellow]Yes[/]" : "No");
        configTable.AddRow("Import profiles", settings.NoProfiles ? "[dim]No[/]" : "Yes");
        configTable.AddRow("Generate embeddings", settings.NoEmbeddings ? "[dim]No[/]" : "Yes");
        configTable.AddRow("Batch size", settings.BatchSize.ToString());
        configTable.AddRow("Store", settings.StoreSlug);
        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        // Get connection string
        var connectionString = settings.ConnectionString ?? _config.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
        {
            AnsiConsole.MarkupLine("[red]No connection string provided. Use --connection or set in appsettings.json[/]");
            return 1;
        }

        // Create DbContext
        var optionsBuilder = new DbContextOptionsBuilder<SegmentCommerceDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());
        
        await using var db = new SegmentCommerceDbContext(optionsBuilder.Options);

        // Test connection
        try
        {
            await db.Database.CanConnectAsync();
            AnsiConsole.MarkupLine("[green]Database connection successful[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Database connection failed: {ex.Message}[/]");
            return 1;
        }

        // Load data files
        var sellersPath = Path.Combine(settings.InputPath, "sellers.json");
        var customersPath = Path.Combine(settings.InputPath, "customers.json");

        if (!File.Exists(sellersPath))
        {
            AnsiConsole.MarkupLine($"[red]Sellers file not found: {sellersPath}[/]");
            return 1;
        }

        try
        {
            // Clear existing data if requested
            if (settings.ClearExisting)
            {
                await ClearDataAsync(db);
            }

            // Load and import data
            var sellers = await LoadSellersAsync(sellersPath);
            AnsiConsole.MarkupLine($"[dim]Loaded {sellers.Count} sellers from JSON[/]");

            // Import categories first
            await ImportCategoriesAsync(db, sellers);

            // Ensure store exists
            var store = await EnsureStoreAsync(db, settings.StoreSlug);

            // Import sellers and products
            var stats = await ImportSellersAsync(db, sellers, store, settings);

            // Import customers/profiles
            if (!settings.NoProfiles && File.Exists(customersPath))
            {
                var customers = await LoadCustomersAsync(customersPath);
                AnsiConsole.MarkupLine($"[dim]Loaded {customers.Count} customers from JSON[/]");
                await ImportProfilesAsync(db, customers, settings);
                stats.ProfilesImported = customers.Count;
            }

            // Print summary
            PrintSummary(stats);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Import failed: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private async Task ClearDataAsync(SegmentCommerceDbContext db)
    {
        await AnsiConsole.Status()
            .StartAsync("Clearing existing data...", async ctx =>
            {
                // Clear in correct order (respecting FK constraints)
                ctx.Status("Clearing product embeddings...");
                await db.ProductEmbeddings.ExecuteDeleteAsync();

                ctx.Status("Clearing store products...");
                await db.StoreProducts.ExecuteDeleteAsync();

                ctx.Status("Clearing product variations...");
                await db.ProductVariations.ExecuteDeleteAsync();

                ctx.Status("Clearing product taxonomy...");
                await db.ProductTaxonomy.ExecuteDeleteAsync();

                ctx.Status("Clearing products...");
                await db.Products.ExecuteDeleteAsync();

                ctx.Status("Clearing sellers...");
                await db.Sellers.ExecuteDeleteAsync();

                ctx.Status("Clearing profiles...");
                await db.Signals.ExecuteDeleteAsync();
                await db.ProfileKeys.ExecuteDeleteAsync();
                await db.SessionProfiles.ExecuteDeleteAsync();
                await db.PersistentProfiles.ExecuteDeleteAsync();

                AnsiConsole.MarkupLine("[yellow]Cleared existing data[/]");
            });
    }

    private async Task<List<GeneratedSeller>> LoadSellersAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<GeneratedSeller>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    private async Task<List<GeneratedCustomer>> LoadCustomersAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<GeneratedCustomer>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    private async Task ImportCategoriesAsync(SegmentCommerceDbContext db, List<GeneratedSeller> sellers)
    {
        // Extract unique categories from all products
        var categories = sellers
            .SelectMany(s => s.Products)
            .Select(p => p.Category)
            .Distinct()
            .ToList();

        var existingCategories = await db.Categories
            .Select(c => c.Slug)
            .ToListAsync();

        var newCategories = categories.Except(existingCategories).ToList();

        if (newCategories.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]All categories already exist[/]");
            return;
        }

        foreach (var slug in newCategories)
        {
            db.Categories.Add(new CategoryEntity
            {
                Slug = slug,
                DisplayName = ToTitleCase(slug.Replace("-", " ")),
                Description = $"Products in the {slug} category",
                CssClass = $"category-{slug}",
                SortOrder = 0
            });
        }

        await db.SaveChangesAsync();
        AnsiConsole.MarkupLine($"[green]Imported {newCategories.Count} categories[/]");
    }

    private async Task<StoreEntity> EnsureStoreAsync(SegmentCommerceDbContext db, string slug)
    {
        var store = await db.Stores.FirstOrDefaultAsync(s => s.Slug == slug);
        
        if (store == null)
        {
            store = new StoreEntity
            {
                Name = ToTitleCase(slug.Replace("-", " ")),
                Slug = slug,
                Description = "Demo store for sample data",
                IsActive = true
            };
            db.Stores.Add(store);
            await db.SaveChangesAsync();
            AnsiConsole.MarkupLine($"[dim]Created store: {store.Name}[/]");
        }

        return store;
    }

    private async Task<ImportStats> ImportSellersAsync(
        SegmentCommerceDbContext db,
        List<GeneratedSeller> sellers,
        StoreEntity store,
        ImportSettings settings)
    {
        var stats = new ImportStats();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Importing sellers & products[/]", maxValue: sellers.Count);

                foreach (var batch in sellers.Chunk(settings.BatchSize))
                {
                    foreach (var genSeller in batch)
                    {
                        // Check if seller already exists
                        var existingSeller = await db.Sellers
                            .FirstOrDefaultAsync(s => s.Name == genSeller.Name);

                        SellerEntity seller;
                        if (existingSeller != null)
                        {
                            seller = existingSeller;
                            stats.SellersSkipped++;
                        }
                        else
                        {
                            seller = new SellerEntity
                            {
                                Name = genSeller.Name,
                                Description = genSeller.Description,
                                Email = genSeller.Email,
                                Phone = genSeller.Phone,
                                Website = genSeller.Website,
                                LogoUrl = genSeller.LogoUrl,
                                Rating = genSeller.Rating,
                                ReviewCount = genSeller.ReviewCount,
                                IsVerified = genSeller.IsVerified,
                                IsActive = true
                            };
                            db.Sellers.Add(seller);
                            await db.SaveChangesAsync();
                            stats.SellersImported++;
                        }

                        // Import products for this seller
                        foreach (var genProduct in genSeller.Products)
                        {
                            var handle = GenerateHandle(genProduct.Name);

                            // Check if product already exists
                            var existingProduct = await db.Products
                                .FirstOrDefaultAsync(p => p.Handle == handle);

                            if (existingProduct != null)
                            {
                                stats.ProductsSkipped++;
                                continue;
                            }

                            // Determine image URL - check if generated image exists
                            var sanitizedName = SanitizeProductName(genProduct.Name);
                            var imageUrl = GetProductImageUrl(settings.InputPath, genProduct.Category, sanitizedName);

                            var product = new ProductEntity
                            {
                                Name = genProduct.Name,
                                Handle = handle,
                                Description = genProduct.Description,
                                Price = genProduct.Price,
                                OriginalPrice = genProduct.OriginalPrice,
                                ImageUrl = imageUrl,
                                Category = genProduct.Category,
                                CategoryPath = genProduct.Category,
                                Tags = genProduct.Tags,
                                Status = ProductStatus.Active,
                                PublishedAt = DateTime.UtcNow,
                                IsTrending = genProduct.IsTrending,
                                IsFeatured = genProduct.IsFeatured,
                                SellerId = seller.Id,
                                Color = genProduct.ColourVariants.FirstOrDefault(),
                                Size = "Default"
                            };

                            db.Products.Add(product);
                            await db.SaveChangesAsync();

                            // Add to store
                            db.StoreProducts.Add(new StoreProductEntity
                            {
                                StoreId = store.Id,
                                ProductId = product.Id
                            });

                            // Create variations for color variants
                            foreach (var color in genProduct.ColourVariants)
                            {
                                db.ProductVariations.Add(new ProductVariationEntity
                                {
                                    ProductId = product.Id,
                                    Color = color,
                                    Size = "Default",
                                    Price = genProduct.Price,
                                    OriginalPrice = genProduct.OriginalPrice,
                                    StockQuantity = Random.Shared.Next(0, 100),
                                    AvailabilityStatus = AvailabilityStatus.InStock,
                                    IsActive = true
                                });
                            }

                            // Generate embedding if enabled
                            if (!settings.NoEmbeddings && genProduct.Embedding != null)
                            {
                                db.ProductEmbeddings.Add(new ProductEmbeddingEntity
                                {
                                    ProductId = product.Id,
                                    Embedding = new Vector(genProduct.Embedding),
                                    Model = "all-MiniLM-L6-v2",
                                    SourceText = $"{product.Name}. {product.Description}"
                                });
                            }

                            stats.ProductsImported++;
                            stats.VariationsImported += genProduct.ColourVariants.Count;
                        }

                        task.Increment(1);
                    }

                    await db.SaveChangesAsync();
                }
            });

        return stats;
    }

    private async Task ImportProfilesAsync(
        SegmentCommerceDbContext db,
        List<GeneratedCustomer> customers,
        ImportSettings settings)
    {
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Importing customer profiles[/]", maxValue: customers.Count);

                foreach (var batch in customers.Chunk(settings.BatchSize))
                {
                    foreach (var customer in batch)
                    {
                        // Check if profile already exists
                        var existing = await db.PersistentProfiles
                            .FirstOrDefaultAsync(p => p.ProfileKey == customer.ProfileKey);

                        if (existing != null)
                        {
                            task.Increment(1);
                            continue;
                        }

                        var profile = new PersistentProfileEntity
                        {
                            ProfileKey = customer.ProfileKey,
                            IdentificationMode = ProfileIdentificationMode.Fingerprint,
                            Interests = customer.Interests,
                            BrandAffinities = customer.BrandAffinities,
                            PricePreferences = customer.PricePreference != null
                                ? new PricePreferences
                                {
                                    MinObserved = customer.PricePreference.Min,
                                    MaxObserved = customer.PricePreference.Max,
                                    PrefersDeals = customer.PricePreference.PrefersDeals,
                                    PrefersLuxury = customer.PricePreference.PrefersLuxury
                                }
                                : null,
                            Traits = new Dictionary<string, bool>(),
                            Segments = ComputeSegments(customer),
                            TotalSessions = Random.Shared.Next(1, 50),
                            TotalSignals = customer.Signals.Count,
                            TotalPurchases = customer.Signals.Count(s => s.SignalType == "purchase"),
                            TotalCartAdds = customer.Signals.Count(s => s.SignalType == "cart_add"),
                            Embedding = customer.Embedding != null ? new Vector(customer.Embedding) : null,
                            EmbeddingComputedAt = customer.Embedding != null ? DateTime.UtcNow : null,
                            SegmentsComputedAt = DateTime.UtcNow
                        };

                        db.PersistentProfiles.Add(profile);
                        task.Increment(1);
                    }

                    await db.SaveChangesAsync();
                }
            });
    }

    private ProfileSegments ComputeSegments(GeneratedCustomer customer)
    {
        var segments = ProfileSegments.None;

        // Based on persona
        if (customer.Persona.Contains("Tech", StringComparison.OrdinalIgnoreCase))
            segments |= ProfileSegments.TechEnthusiast;

        if (customer.Persona.Contains("Fashion", StringComparison.OrdinalIgnoreCase))
            segments |= ProfileSegments.FashionFocused;

        if (customer.Persona.Contains("Budget", StringComparison.OrdinalIgnoreCase))
            segments |= ProfileSegments.Bargain;

        if (customer.Persona.Contains("Luxury", StringComparison.OrdinalIgnoreCase))
            segments |= ProfileSegments.HighValue;

        // Based on price preference
        if (customer.PricePreference?.PrefersDeals == true)
            segments |= ProfileSegments.Bargain;

        if (customer.PricePreference?.PrefersLuxury == true)
            segments |= ProfileSegments.HighValue;

        // Based on signals
        var purchaseCount = customer.Signals.Count(s => s.SignalType == "purchase");
        if (purchaseCount > 5)
            segments |= ProfileSegments.ReturningVisitor;
        else if (purchaseCount == 0)
            segments |= ProfileSegments.NewVisitor;

        return segments;
    }

    private void PrintSummary(ImportStats stats)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[green]Import Complete[/]"));

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Entity");
        table.AddColumn("Imported");
        table.AddColumn("Skipped");
        table.AddRow("Sellers", stats.SellersImported.ToString(), stats.SellersSkipped.ToString());
        table.AddRow("Products", stats.ProductsImported.ToString(), stats.ProductsSkipped.ToString());
        table.AddRow("Variations", stats.VariationsImported.ToString(), "-");
        table.AddRow("Profiles", stats.ProfilesImported.ToString(), "-");
        AnsiConsole.Write(table);
    }

    private static string GenerateHandle(string name)
    {
        var handle = name.ToLowerInvariant();
        handle = Regex.Replace(handle, @"[^a-z0-9\s-]", "");
        handle = Regex.Replace(handle, @"\s+", "-");
        handle = Regex.Replace(handle, @"-+", "-");
        handle = handle.Trim('-');

        // Add uniqueness suffix if needed
        if (handle.Length > 100)
            handle = handle[..100];

        return $"{handle}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    private static string ToTitleCase(string input)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }

    /// <summary>
    /// Sanitize product name to match folder naming convention used by image generator.
    /// </summary>
    private static string SanitizeProductName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_")
            .ToLowerInvariant();
    }

    /// <summary>
    /// Get the image URL for a product. Returns API endpoint URL if generated image exists,
    /// otherwise returns placeholder URL.
    /// </summary>
    private static string GetProductImageUrl(string inputPath, string category, string sanitizedName)
    {
        // Check if generated image exists
        var imagePath = Path.Combine(inputPath, "images", category, sanitizedName, "main.png");
        
        if (File.Exists(imagePath))
        {
            // Return URL to our ImageController endpoint
            return $"/api/images/products/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(sanitizedName)}/main.png";
        }

        // Fallback to placeholder (will be handled by PlaceholderController)
        return "/images/placeholder.jpg";
    }

    private class ImportStats
    {
        public int SellersImported { get; set; }
        public int SellersSkipped { get; set; }
        public int ProductsImported { get; set; }
        public int ProductsSkipped { get; set; }
        public int VariationsImported { get; set; }
        public int ProfilesImported { get; set; }
    }
}
