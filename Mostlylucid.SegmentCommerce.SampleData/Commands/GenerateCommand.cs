using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
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

        // Enrich personas (Ollama with fallback)
        await EnrichPersonasAsync(profiles);

        // Generate profile images if ComfyUI is available (only if images are requested)
        if (!settings.NoImages)
        {
            await GenerateProfileImagesAsync(profiles);
        }

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
        var profileCount = Math.Max(20, settings.Count * 2 * (categories.Count()));
        return _profileGenerator.GenerateProfiles(profileCount, categories);
    }

    private async Task EnrichPersonasAsync(List<GeneratedProfile> profiles)
    {
        var httpClient = new HttpClient();
        var personaGenerator = new PersonaGenerator(httpClient, _config);
        await personaGenerator.EnrichAsync(profiles);
    }

    private async Task GenerateProfileImagesAsync(List<GeneratedProfile> profiles)
    {
        var httpClient = new HttpClient();
        var imageGenerator = new ComfyUIImageGenerator(httpClient, _config);

        if (!await imageGenerator.IsAvailableAsync())
        {
            AnsiConsole.MarkupLine("[yellow]ComfyUI not available - skipping profile portraits[/]");
            return;
        }

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[blue]Generating profile portraits[/]", maxValue: profiles.Count);

                var profileIndex = 0;
                foreach (var profile in profiles)
                {
                    var prompt = GeneratePhotoRealisticPortraitPrompt(profileIndex);
                    profileIndex++;

                    try
                    {
                        var image = await imageGenerator.GeneratePortraitAsync(prompt, profile.ProfileKey);
                        if (image != null)
                        {
                            profile.ProfileImagePath = image.FilePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.WriteLine($"Failed portrait for {profile.ProfileKey}: {ex.Message}");
                    }

                    task.Increment(1);
                }
            });
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
            AnsiConsole.MarkupLine("[yellow]ComfyUI not available - using placeholder images[/]");
            await GeneratePlaceholderImages(products, settings);
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
                        AnsiConsole.WriteLine($"Failed: {product.Name}: {ex.Message}");
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
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        await using var context = new SegmentCommerceDbContext(optionsBuilder.Options);

        // Ensure database exists
        await context.Database.MigrateAsync();

        var count = 0;

        await AnsiConsole.Status()
            .StartAsync("Writing to database...", async ctx =>
            {
                // Create sellers from customer profiles (10% of profiles become sellers)
                var sellers = new List<SellerEntity>();
                var sellerProfiles = profiles.Take((int)(profiles.Count * 0.1)).ToList();
                
                foreach (var profile in sellerProfiles)
                {
                    var seller = new SellerEntity
                    {
                        Name = profile.DisplayName ?? $"Seller {profile.ProfileKey[..8]}",
                        Description = profile.Bio ?? "Trusted marketplace seller",
                        Email = $"seller{profile.ProfileKey[..8]}@marketplace.com",
                        Phone = $"+1{Random.Shared.Next(2000000000, int.MaxValue)}",
                        Rating = Math.Round(Random.Shared.NextDouble() * 2 + 3, 1), // 3.0-5.0
                        ReviewCount = Random.Shared.Next(10, 5000),
                        IsVerified = Random.Shared.NextDouble() > 0.3, // 70% verified
                        Website = Random.Shared.NextDouble() > 0.5 ? $"https://shop{profile.ProfileKey[..8]}.com" : null,
                        LogoUrl = profile.ProfileImagePath
                    };
                    context.Sellers.Add(seller);
                    sellers.Add(seller);
                }
                
                await context.SaveChangesAsync(); // Save sellers to get IDs
                
                // If no sellers created, create a default one
                if (sellers.Count == 0)
                {
                    var defaultSeller = new SellerEntity
                    {
                        Name = "Default Marketplace",
                        Description = "Primary marketplace seller",
                        Email = "seller@marketplace.com",
                        Phone = "+1234567890",
                        Rating = 4.5,
                        ReviewCount = 1000,
                        IsVerified = true
                    };
                    context.Sellers.Add(defaultSeller);
                    await context.SaveChangesAsync();
                    sellers.Add(defaultSeller);
                }

                var sizes = new[] { "XS", "S", "M", "L", "XL", "XXL" };
                var random = new Random();
                var sellerIndex = 0;

                foreach (var (categorySlug, categoryProducts) in products)
                {
                    foreach (var product in categoryProducts)
                    {
                        // Create the main product entity and assign to a seller (round-robin)
                        var assignedSeller = sellers[sellerIndex % sellers.Count];
                        sellerIndex++;
                        
                        var defaultColor = product.ColourVariants.FirstOrDefault() ?? "Default";
                        var entity = new ProductEntity
                        {
                            Name = product.Name,
                            Description = product.Description,
                            Category = categorySlug,
                            CategoryPath = categorySlug,
                            Price = product.Price,
                            OriginalPrice = product.OriginalPrice,
                            ImageUrl = product.Images.FirstOrDefault(i => i.IsPrimary)?.FilePath
                                       ?? product.Images.FirstOrDefault()?.FilePath
                                       ?? $"/api/placeholder/{Uri.EscapeDataString(categorySlug)}/{Uri.EscapeDataString(product.Name)}",
                            Tags = product.Tags,
                            IsTrending = product.IsTrending,
                            IsFeatured = product.IsFeatured,
                            SellerId = assignedSeller.Id,
                            Color = defaultColor,
                            Size = "M"
                        };

                        context.Products.Add(entity);
                        await context.SaveChangesAsync(); // Save to get the ID

                        // Create product variations for each color and size combination
                        var variationCount = 0;
                        foreach (var color in product.ColourVariants.Take(4)) // Limit to 4 colors
                        {
                            // Determine which sizes apply based on category
                            var applicableSizes = categorySlug.ToLower() switch
                            {
                                "fashion" => sizes,
                                "sport" => sizes,
                                _ => new[] { "S", "M", "L" } // Default sizes for other categories
                            };

                            foreach (var size in applicableSizes)
                            {
                                var colorImage = product.Images.FirstOrDefault(i => i.Variant == color)?.FilePath
                                               ?? product.Images.FirstOrDefault(i => i.IsPrimary)?.FilePath
                                               ?? $"/api/placeholder/{Uri.EscapeDataString(categorySlug)}/{Uri.EscapeDataString(product.Name + "-" + color)}";

                                var variation = new ProductVariationEntity
                                {
                                    ProductId = entity.Id,
                                    Color = color,
                                    Size = size,
                                    Price = product.Price + (decimal)(random.NextDouble() * 5 - 2.5), // Small price variation
                                    OriginalPrice = product.OriginalPrice,
                                    ImageUrl = colorImage,
                                    StockQuantity = random.Next(5, 50),
                                    IsActive = true
                                };

                                context.ProductVariations.Add(variation);
                                variationCount++;
                            }
                        }

                        count++;
                        
                        if (variationCount > 0)
                        {
                            await context.SaveChangesAsync();
                        }
                    }
                }

                // store profiles using the new PersistentProfileEntity structure
                foreach (var profile in profiles)
                {
                    var persistentProfile = new PersistentProfileEntity
                    {
                        ProfileKey = profile.ProfileKey,
                        IdentificationMode = ProfileIdentificationMode.Fingerprint,
                        Interests = profile.Interests,
                        TotalSignals = profile.Signals.Count,
                        CreatedAt = DateTime.UtcNow,
                        LastSeenAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.PersistentProfiles.Add(persistentProfile);
                }

                await context.SaveChangesAsync();
            });

        AnsiConsole.MarkupLine($"[green]Wrote {count} products and {profiles.Count} profiles to database[/]");
    }

    private async Task GeneratePlaceholderImages(
        Dictionary<string, List<GeneratedProduct>> products,
        GenerateSettings settings)
    {
        var allProducts = products.Values.SelectMany(p => p).ToList();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[blue]Generating placeholder images[/]", maxValue: allProducts.Count);

                foreach (var product in allProducts)
                {
                    task.Description = $"[blue]Placeholder: {product.Name.Truncate(30)}[/]";
                    await CreatePlaceholderImagesForProduct(product, settings);
                    task.Increment(1);
                }
            });
    }

    private async Task CreatePlaceholderImagesForProduct(GeneratedProduct product, GenerateSettings settings)
    {
        var outputDir = Path.Combine(settings.OutputPath, "images", product.Category, SanitizeFileName(product.Name));
        Directory.CreateDirectory(outputDir);

        product.Images = new List<GeneratedImage>();

        // Generate main image
        var mainImage = await CreatePlaceholderImage(product.Name, outputDir, "main");
        if (mainImage != null)
        {
            mainImage.IsPrimary = true;
            product.Images.Add(mainImage);
        }

        // Generate colour variant images
        foreach (var colour in product.ColourVariants.Take(3))
        {
            var variantImage = await CreatePlaceholderImage(
                $"{product.Name}-{colour}", 
                outputDir, 
                $"colour_{SanitizeFileName(colour)}");
            
            if (variantImage != null)
            {
                variantImage.Variant = colour;
                product.Images.Add(variantImage);
            }
        }
    }

    private Task<GeneratedImage?> CreatePlaceholderImage(string seed, string outputDir, string variant)
    {
        // Instead of downloading from picsum, we'll just record the path
        // The actual image will be served by the PlaceholderController at runtime
        var fileName = $"{variant}.svg";
        var filePath = Path.Combine(outputDir, fileName);
        
        // Create a simple SVG placeholder file
        var svgContent = GenerateSvgPlaceholder(seed, variant);
        File.WriteAllText(filePath, svgContent);

        return Task.FromResult<GeneratedImage?>(new GeneratedImage
        {
            FilePath = filePath,
            Variant = variant,
            IsPrimary = false
        });
    }

    private static string GenerateSvgPlaceholder(string productName, string variant)
    {
        var hash = Math.Abs(productName.GetHashCode());
        var hue = hash % 360;
        
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="400" viewBox="0 0 400 400">
              <rect width="100%" height="100%" fill="hsl({hue}, 30%, 20%)"/>
              <text x="50%" y="50%" text-anchor="middle" fill="hsl({hue}, 50%, 70%)" font-family="system-ui" font-size="16" dy=".3em">
                {System.Security.SecurityElement.Escape(productName.Length > 30 ? productName[..27] + "..." : productName)}
              </text>
              <text x="50%" y="60%" text-anchor="middle" fill="hsl({hue}, 50%, 60%)" font-family="system-ui" font-size="12" opacity="0.7">
                {System.Security.SecurityElement.Escape(variant)}
              </text>
            </svg>
            """;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_")
            .ToLowerInvariant();
    }

    private static string GeneratePhotoRealisticPortraitPrompt(int index)
    {
        var rand = new Random(index + Environment.TickCount);
        
        // Much more diverse options
        var genders = new[] { "man", "woman", "non-binary person", "male", "female", "person" };
        var ages = new[] { "18 year old", "22 year old", "25 year old", "28 year old", "30 year old", "35 year old", "40 year old", "45 year old", "50 year old", "55 year old", "60 year old" };
        var ethnicities = new[] { "East Asian", "South Asian", "Southeast Asian", "African", "African American", "Caucasian", "Hispanic", "Latino", "Middle Eastern", "Mediterranean", "Pacific Islander", "Indigenous", "multiracial", "Indian", "Chinese", "Japanese", "Korean", "Vietnamese", "Filipino", "Nigerian", "Ethiopian", "Egyptian", "Brazilian", "Mexican", "Italian", "Greek", "Turkish", "Persian" };
        var expressions = new[] { "genuine warm smile", "slight smile", "friendly expression", "professional demeanor", "confident look", "natural smile", "approachable expression", "thoughtful expression", "relaxed smile", "bright smile", "subtle grin", "serene expression" };
        var hairstyles = new[] { "short cropped hair", "long flowing hair", "shoulder-length hair", "styled wavy hair", "curly hair", "straight hair", "braided hair", "natural afro", "buzz cut", "bob cut", "pixie cut", "man bun", "ponytail", "dreadlocks", "cornrows", "messy hair", "slicked back hair", "side-parted hair" };
        var hairColors = new[] { "black hair", "brown hair", "blonde hair", "red hair", "auburn hair", "gray hair", "silver hair", "dark brown hair", "light brown hair", "jet black hair", "chestnut hair" };
        var clothing = new[] { "business casual attire", "smart casual clothing", "professional suit", "casual t-shirt", "sweater", "blouse", "dress shirt", "polo shirt", "turtleneck", "button-down shirt", "casual blazer", "knit top" };
        var clothingColors = new[] { "navy blue", "black", "white", "gray", "charcoal", "light blue", "burgundy", "olive", "beige", "cream", "dark green", "maroon" };
        var backgrounds = new[] { "soft gray background", "warm beige background", "cool white background", "subtle blue background", "neutral tan background", "light cream background", "professional gray background", "muted green background", "soft pink background", "pale yellow background", "dusty blue background" };
        var lighting = new[] { "soft natural window light", "studio softbox lighting", "golden hour lighting", "diffused overhead lighting", "Rembrandt lighting", "butterfly lighting", "split lighting", "broad lighting", "rim lighting with soft fill" };
        var facialFeatures = new[] { "defined cheekbones", "soft facial features", "strong jawline", "round face", "oval face", "heart-shaped face", "square face", "angular features", "gentle features" };
        var eyeColors = new[] { "brown eyes", "blue eyes", "green eyes", "hazel eyes", "amber eyes", "gray eyes", "dark brown eyes" };
        var skinTones = new[] { "fair skin", "light skin", "medium skin", "tan skin", "olive skin", "brown skin", "dark skin", "deep skin", "warm skin tone", "cool skin tone" };

        var gender = genders[rand.Next(genders.Length)];
        var age = ages[rand.Next(ages.Length)];
        var ethnicity = ethnicities[rand.Next(ethnicities.Length)];
        var expression = expressions[rand.Next(expressions.Length)];
        var hairstyle = hairstyles[rand.Next(hairstyles.Length)];
        var hairColor = hairColors[rand.Next(hairColors.Length)];
        var clothingStyle = clothing[rand.Next(clothing.Length)];
        var clothingColor = clothingColors[rand.Next(clothingColors.Length)];
        var background = backgrounds[rand.Next(backgrounds.Length)];
        var lightingStyle = lighting[rand.Next(lighting.Length)];
        var facialFeature = facialFeatures[rand.Next(facialFeatures.Length)];
        var eyeColor = eyeColors[rand.Next(eyeColors.Length)];
        var skinTone = skinTones[rand.Next(skinTones.Length)];

        // Add random details for more uniqueness
        var details = new List<string>();
        if (rand.Next(2) == 0) details.Add("wearing glasses");
        if (rand.Next(3) == 0) details.Add("light facial hair");
        if (rand.Next(3) == 0) details.Add("freckles");
        if (rand.Next(4) == 0) details.Add("dimples");
        if (rand.Next(5) == 0) details.Add("subtle makeup");
        var additionalDetails = details.Count > 0 ? ", " + string.Join(", ", details) : "";

        return $"photorealistic portrait photograph of a {age} {ethnicity} {gender}, {skinTone}, {eyeColor}, {facialFeature}, {hairColor}, {hairstyle}, {expression}, {clothingColor} {clothingStyle}{additionalDetails}, professional headshot, {lightingStyle}, {background}, 85mm portrait lens, f/1.4 aperture, bokeh, sharp focus on face, natural skin texture with pores, professional photography, magazine quality, 4K resolution, extremely detailed, realistic human face, candid portrait, NO illustration, NO cartoon, NO anime, NO drawing, NO digital art, NO CGI, NO 3D render, NO artificial, pure photography";
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
