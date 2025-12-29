using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Data;

/// <summary>
/// Seeds the database with products, categories, and sellers.
/// Supports both generated data from JSON files and fallback sample data.
/// </summary>
public static class DbSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Seeds the database. First tries to load from generated JSON files,
    /// falls back to sample data if files don't exist.
    /// </summary>
    public static async Task SeedAsync(
        SegmentCommerceDbContext context,
        string? dataPath = null,
        ILogger? logger = null)
    {
        await context.Database.MigrateAsync();

        // Try to find generated data
        var searchPaths = new[]
        {
            dataPath,
            Path.Combine(AppContext.BaseDirectory, "SeedData"),
            Path.Combine(Directory.GetCurrentDirectory(), "SeedData"),
            "D:\\segmentdata" // Default output path from SampleData project
        }.Where(p => !string.IsNullOrEmpty(p)).ToArray();

        string? validPath = null;
        foreach (var path in searchPaths)
        {
            if (path != null && Directory.Exists(path) && File.Exists(Path.Combine(path, "sellers.json")))
            {
                validPath = path;
                break;
            }
        }

        if (!await context.Categories.AnyAsync())
        {
            await SeedCategoriesAsync(context);
        }

        if (!await context.SellerProfiles.AnyAsync())
        {
            if (validPath != null)
            {
                logger?.LogInformation("Loading sellers from {Path}", validPath);
                await SeedFromJsonAsync(context, validPath, logger);
            }
            else
            {
                logger?.LogInformation("No generated data found, using sample data");
                await SeedSellersAsync(context);
                await SeedSampleProductsAsync(context);
            }
        }
    }

    /// <summary>
    /// Seeds from generated JSON files with proper image paths.
    /// </summary>
    private static async Task SeedFromJsonAsync(
        SegmentCommerceDbContext context,
        string dataPath,
        ILogger? logger)
    {
        var sellersPath = Path.Combine(dataPath, "sellers.json");
        var json = await File.ReadAllTextAsync(sellersPath);
        var sellers = JsonSerializer.Deserialize<List<GeneratedSeller>>(json, JsonOptions) ?? [];

        logger?.LogInformation("Found {Count} sellers to import", sellers.Count);

        // First, create all sellers (as UserEntity + SellerProfileEntity)
        var sellerUserIds = new Dictionary<string, Guid>();
        foreach (var seller in sellers)
        {
            var userId = Guid.NewGuid();
            var userEntity = new UserEntity
            {
                Id = userId,
                Email = seller.Email ?? $"{GenerateHandle(seller.Name)}@seller.local",
                DisplayName = seller.Name,
                AvatarUrl = seller.LogoUrl,
                IsActive = true,
                EmailVerified = true
            };
            var sellerProfile = new SellerProfileEntity
            {
                UserId = userId,
                BusinessName = seller.Name,
                Description = seller.Description,
                Website = seller.Website,
                LogoUrl = seller.LogoUrl,
                Rating = seller.Rating,
                ReviewCount = seller.ReviewCount,
                IsVerified = seller.IsVerified,
                IsActive = true
            };
            context.Users.Add(userEntity);
            context.SellerProfiles.Add(sellerProfile);
            sellerUserIds[seller.Id] = userId;
        }
        await context.SaveChangesAsync();

        // Now create products with proper image paths
        var productCount = 0;
        var wwwrootImages = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
        Directory.CreateDirectory(wwwrootImages);

        foreach (var seller in sellers)
        {
            var sellerUserId = sellerUserIds[seller.Id];

            foreach (var product in seller.Products)
            {
                // Determine image URL - prefer local images, generate placeholder if needed
                var imageUrl = await ResolveImageUrl(product, dataPath, wwwrootImages, logger);

                var productEntity = new ProductEntity
                {
                    Name = product.Name,
                    Handle = GenerateHandle(product.Name),
                    Description = product.Description,
                    Price = product.Price,
                    OriginalPrice = product.OriginalPrice,
                    ImageUrl = imageUrl,
                    Category = product.Category,
                    CategoryPath = product.Category,
                    Tags = product.Tags ?? [],
                    Status = ProductStatus.Active,
                    PublishedAt = DateTime.UtcNow,
                    IsTrending = product.IsTrending,
                    IsFeatured = product.IsFeatured,
                    SellerId = sellerUserId,
                    Color = product.ColourVariants?.FirstOrDefault() ?? "Default",
                    Size = "Default"
                };

                context.Products.Add(productEntity);
                await context.SaveChangesAsync();

                // Create variations for colour variants
                if (product.ColourVariants?.Count > 0)
                {
                    foreach (var colour in product.ColourVariants.Take(5))
                    {
                        var variationImageUrl = await ResolveVariationImageUrl(
                            product, colour, dataPath, wwwrootImages, logger);

                        context.ProductVariations.Add(new ProductVariationEntity
                        {
                            ProductId = productEntity.Id,
                            Color = colour,
                            Size = "Default",
                            Price = product.Price,
                            OriginalPrice = product.OriginalPrice,
                            ImageUrl = variationImageUrl,
                            StockQuantity = Random.Shared.Next(5, 100),
                            AvailabilityStatus = AvailabilityStatus.InStock,
                            IsActive = true
                        });
                    }
                }

                productCount++;
            }
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("Imported {Count} products from generated data", productCount);
    }

    /// <summary>
    /// Resolves the image URL for a product, copying from generated path to wwwroot if needed.
    /// </summary>
    private static async Task<string> ResolveImageUrl(
        GeneratedProduct product,
        string dataPath,
        string wwwrootImages,
        ILogger? logger)
    {
        // Check if we have generated images
        var primaryImage = product.Images?.FirstOrDefault(i => i.IsPrimary)
            ?? product.Images?.FirstOrDefault();

        if (primaryImage?.FilePath != null && File.Exists(primaryImage.FilePath))
        {
            // Copy to wwwroot and return relative URL
            var destDir = Path.Combine(wwwrootImages, product.Category, SanitizeFileName(product.Name));
            Directory.CreateDirectory(destDir);
            
            var destFile = Path.Combine(destDir, "main.png");
            if (!File.Exists(destFile))
            {
                await CopyFileAsync(primaryImage.FilePath, destFile);
            }
            
            return $"/images/products/{product.Category}/{SanitizeFileName(product.Name)}/main.png";
        }

        // Check for images in the data path structure
        var imagePath = Path.Combine(dataPath, "images", product.Category, SanitizeFileName(product.Name), "main.png");
        if (File.Exists(imagePath))
        {
            var destDir = Path.Combine(wwwrootImages, product.Category, SanitizeFileName(product.Name));
            Directory.CreateDirectory(destDir);
            
            var destFile = Path.Combine(destDir, "main.png");
            if (!File.Exists(destFile))
            {
                await CopyFileAsync(imagePath, destFile);
            }
            
            return $"/images/products/{product.Category}/{SanitizeFileName(product.Name)}/main.png";
        }

        // Generate a deterministic placeholder URL using product hash (not picsum!)
        return GeneratePlaceholderUrl(product.Name, product.Category);
    }

    private static async Task<string> ResolveVariationImageUrl(
        GeneratedProduct product,
        string colour,
        string dataPath,
        string wwwrootImages,
        ILogger? logger)
    {
        var sanitizedColour = SanitizeFileName(colour);
        
        // Check for generated variant image
        var variantImage = product.Images?.FirstOrDefault(i => 
            i.Variant?.Equals(colour, StringComparison.OrdinalIgnoreCase) == true);

        if (variantImage?.FilePath != null && File.Exists(variantImage.FilePath))
        {
            var destDir = Path.Combine(wwwrootImages, product.Category, SanitizeFileName(product.Name));
            Directory.CreateDirectory(destDir);
            
            var destFile = Path.Combine(destDir, $"colour_{sanitizedColour}.png");
            if (!File.Exists(destFile))
            {
                await CopyFileAsync(variantImage.FilePath, destFile);
            }
            
            return $"/images/products/{product.Category}/{SanitizeFileName(product.Name)}/colour_{sanitizedColour}.png";
        }

        // Check for images in the data path structure
        var imagePath = Path.Combine(dataPath, "images", product.Category, 
            SanitizeFileName(product.Name), $"colour_{sanitizedColour}.png");
        if (File.Exists(imagePath))
        {
            var destDir = Path.Combine(wwwrootImages, product.Category, SanitizeFileName(product.Name));
            Directory.CreateDirectory(destDir);
            
            var destFile = Path.Combine(destDir, $"colour_{sanitizedColour}.png");
            if (!File.Exists(destFile))
            {
                await CopyFileAsync(imagePath, destFile);
            }
            
            return $"/images/products/{product.Category}/{SanitizeFileName(product.Name)}/colour_{sanitizedColour}.png";
        }

        // Fall back to main product image or placeholder
        return GeneratePlaceholderUrl($"{product.Name}-{colour}", product.Category);
    }

    private static async Task CopyFileAsync(string source, string dest)
    {
        await using var sourceStream = File.OpenRead(source);
        await using var destStream = File.Create(dest);
        await sourceStream.CopyToAsync(destStream);
    }

    /// <summary>
    /// Generates a placeholder image URL using SVG data URI - no external dependencies!
    /// </summary>
    private static string GeneratePlaceholderUrl(string productName, string category)
    {
        // Use a local placeholder endpoint that generates SVG on the fly
        return $"/api/placeholder/{Uri.EscapeDataString(category)}/{Uri.EscapeDataString(productName)}";
    }

    private static async Task SeedCategoriesAsync(SegmentCommerceDbContext context)
    {
        var categories = new List<CategoryEntity>
        {
            new() { Slug = "tech", DisplayName = "Technology", CssClass = "interest-tech", SortOrder = 1 },
            new() { Slug = "fashion", DisplayName = "Fashion", CssClass = "interest-fashion", SortOrder = 2 },
            new() { Slug = "home", DisplayName = "Home & Garden", CssClass = "interest-home", SortOrder = 3 },
            new() { Slug = "sport", DisplayName = "Sports", CssClass = "interest-sport", SortOrder = 4 },
            new() { Slug = "books", DisplayName = "Books", CssClass = "interest-books", SortOrder = 5 },
            new() { Slug = "food", DisplayName = "Food & Drink", CssClass = "interest-food", SortOrder = 6 }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSellersAsync(SegmentCommerceDbContext context)
    {
        var sellerData = new[]
        {
            ("TechGadgets Pro", "Premium technology and electronics retailer", "hello@techgadgetspro.test", 4.8, 1250, true),
            ("Fashion Forward", "Contemporary fashion and accessories", "hello@fashionforward.test", 4.6, 890, true),
            ("Home Essentials", "Quality home and garden products", "hello@homeessentials.test", 4.7, 560, true),
            ("Active Life Store", "Sports equipment and fitness gear", "hello@activelifestore.test", 4.5, 720, true),
            ("Book Haven", "Books for every reader", "hello@bookhaven.test", 4.9, 1100, true),
            ("Gourmet Delights", "Premium food and beverages", "hello@gourmetdelights.test", 4.7, 430, true)
        };

        foreach (var (name, description, email, rating, reviewCount, isVerified) in sellerData)
        {
            var userId = Guid.NewGuid();
            var user = new UserEntity
            {
                Id = userId,
                Email = email,
                DisplayName = name,
                IsActive = true,
                EmailVerified = true
            };
            var sellerProfile = new SellerProfileEntity
            {
                UserId = userId,
                BusinessName = name,
                Description = description,
                Rating = rating,
                ReviewCount = reviewCount,
                IsVerified = isVerified,
                IsActive = true
            };
            context.Users.Add(user);
            context.SellerProfiles.Add(sellerProfile);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds sample products with generated placeholder URLs (no picsum!).
    /// </summary>
    private static async Task SeedSampleProductsAsync(SegmentCommerceDbContext context)
    {
        var sellerProfiles = await context.SellerProfiles.ToListAsync();
        var now = DateTime.UtcNow;

        var sellersByCategory = new Dictionary<string, Guid>
        {
            ["tech"] = sellerProfiles.First(s => s.BusinessName.Contains("Tech")).UserId,
            ["fashion"] = sellerProfiles.First(s => s.BusinessName.Contains("Fashion")).UserId,
            ["home"] = sellerProfiles.First(s => s.BusinessName.Contains("Home")).UserId,
            ["sport"] = sellerProfiles.First(s => s.BusinessName.Contains("Active")).UserId,
            ["books"] = sellerProfiles.First(s => s.BusinessName.Contains("Book")).UserId,
            ["food"] = sellerProfiles.First(s => s.BusinessName.Contains("Gourmet")).UserId
        };

        var products = new List<ProductEntity>
        {
            // Tech
            CreateProduct("Wireless Noise-Cancelling Headphones", "tech", 249.99m, 299.99m,
                "Premium over-ear headphones with active noise cancellation and 30-hour battery life.",
                ["audio", "wireless", "premium"], sellersByCategory["tech"], true, true, now,
                ["Midnight Black", "Arctic White", "Space Grey"]),
            CreateProduct("Mechanical Keyboard RGB", "tech", 129.99m, null,
                "Full-size mechanical keyboard with hot-swappable switches and per-key RGB lighting.",
                ["gaming", "peripherals", "rgb"], sellersByCategory["tech"], false, false, now,
                ["Black", "White", "Navy"]),
            CreateProduct("4K Webcam Pro", "tech", 179.99m, null,
                "Ultra HD webcam with autofocus and low-light correction for professional video calls.",
                ["video", "streaming", "work-from-home"], sellersByCategory["tech"], false, false, now,
                ["Black", "White"]),
            CreateProduct("Portable SSD 2TB", "tech", 199.99m, 249.99m,
                "Compact external SSD with USB-C and transfer speeds up to 1050MB/s.",
                ["storage", "portable", "fast"], sellersByCategory["tech"], false, false, now,
                ["Black", "Silver", "Blue"]),
            CreateProduct("Smart Home Hub", "tech", 149.99m, null,
                "Central hub for all your smart home devices with voice control support.",
                ["smart-home", "automation", "voice"], sellersByCategory["tech"], true, false, now,
                ["Charcoal", "White"]),

            // Fashion
            CreateProduct("Classic Leather Jacket", "fashion", 299.99m, null,
                "Timeless genuine leather jacket with a modern slim fit.",
                ["leather", "outerwear", "classic"], sellersByCategory["fashion"], false, true, now,
                ["Black", "Brown", "Cognac"]),
            CreateProduct("Premium Cotton T-Shirt Pack", "fashion", 49.99m, null,
                "Set of 3 essential cotton t-shirts in neutral colours.",
                ["basics", "cotton", "essentials"], sellersByCategory["fashion"], false, false, now,
                ["White", "Black", "Grey"]),
            CreateProduct("Designer Sunglasses", "fashion", 189.99m, 229.99m,
                "UV400 polarised sunglasses with titanium frames.",
                ["accessories", "summer", "uv-protection"], sellersByCategory["fashion"], true, false, now,
                ["Black", "Tortoise", "Gold"]),
            CreateProduct("Minimalist Watch", "fashion", 349.99m, null,
                "Elegant watch with sapphire crystal and Swiss movement.",
                ["accessories", "luxury", "minimalist"], sellersByCategory["fashion"], false, false, now,
                ["Silver", "Gold", "Rose Gold"]),
            CreateProduct("Canvas Weekender Bag", "fashion", 129.99m, null,
                "Durable canvas bag perfect for short trips and daily use.",
                ["bags", "travel", "casual"], sellersByCategory["fashion"], false, true, now,
                ["Navy", "Olive", "Tan"]),

            // Home
            CreateProduct("Smart LED Bulb Kit", "home", 79.99m, null,
                "Set of 4 colour-changing smart bulbs compatible with all major assistants.",
                ["smart-home", "lighting", "energy-efficient"], sellersByCategory["home"], false, false, now,
                ["Warm White", "Cool White", "RGB"]),
            CreateProduct("Ergonomic Office Chair", "home", 449.99m, 549.99m,
                "Fully adjustable mesh office chair with lumbar support.",
                ["office", "ergonomic", "comfort"], sellersByCategory["home"], false, true, now,
                ["Black", "Grey", "Blue"]),
            CreateProduct("Indoor Plant Collection", "home", 59.99m, null,
                "Curated set of 3 low-maintenance indoor plants with ceramic pots.",
                ["plants", "decor", "wellness"], sellersByCategory["home"], false, false, now,
                ["Green", "Terracotta", "White Pot"]),
            CreateProduct("Aromatherapy Diffuser", "home", 39.99m, null,
                "Ultrasonic essential oil diffuser with ambient lighting.",
                ["wellness", "relaxation", "aromatherapy"], sellersByCategory["home"], false, false, now,
                ["White", "Wood Grain", "Black"]),
            CreateProduct("Minimalist Desk Lamp", "home", 89.99m, null,
                "Adjustable LED desk lamp with wireless charging base.",
                ["lighting", "office", "modern"], sellersByCategory["home"], true, false, now,
                ["Black", "White", "Gold"]),

            // Sport
            CreateProduct("Running Shoes Pro", "sport", 159.99m, null,
                "Lightweight running shoes with responsive cushioning and breathable mesh.",
                ["running", "fitness", "performance"], sellersByCategory["sport"], true, false, now,
                ["Black/White", "Grey/Neon", "Navy/Red"]),
            CreateProduct("Yoga Mat Premium", "sport", 69.99m, null,
                "Non-slip yoga mat with alignment lines and carrying strap.",
                ["yoga", "fitness", "home-workout"], sellersByCategory["sport"], false, false, now,
                ["Purple", "Teal", "Grey"]),
            CreateProduct("Fitness Tracker Band", "sport", 89.99m, 119.99m,
                "Water-resistant fitness tracker with heart rate and sleep monitoring.",
                ["wearable", "health", "tracking"], sellersByCategory["sport"], false, false, now,
                ["Black", "White", "Coral"]),
            CreateProduct("Resistance Band Set", "sport", 29.99m, null,
                "Complete set of 5 resistance bands with different tension levels.",
                ["strength", "home-workout", "portable"], sellersByCategory["sport"], false, false, now,
                ["Multi-colour"]),
            CreateProduct("Insulated Water Bottle", "sport", 34.99m, null,
                "Double-wall insulated bottle keeps drinks cold for 24 hours.",
                ["hydration", "outdoor", "eco-friendly"], sellersByCategory["sport"], false, true, now,
                ["Matte Black", "Arctic White", "Forest Green"]),

            // Books
            CreateProduct("The Pragmatic Programmer", "books", 39.99m, null,
                "Classic software development book covering best practices and career advice.",
                ["programming", "career", "classic"], sellersByCategory["books"], false, true, now, []),
            CreateProduct("Designing Data-Intensive Applications", "books", 44.99m, null,
                "Comprehensive guide to building reliable and scalable data systems.",
                ["programming", "data", "architecture"], sellersByCategory["books"], false, false, now, []),
            CreateProduct("Atomic Habits", "books", 16.99m, null,
                "Practical strategies for building good habits and breaking bad ones.",
                ["self-help", "productivity", "bestseller"], sellersByCategory["books"], true, false, now, []),
            CreateProduct("Clean Code", "books", 29.99m, null,
                "Essential guide to writing maintainable and readable code.",
                ["programming", "software-development", "reference"], sellersByCategory["books"], false, false, now, []),
            CreateProduct("Deep Work", "books", 18.99m, null,
                "Rules for focused success in a distracted world.",
                ["productivity", "self-help", "business"], sellersByCategory["books"], false, true, now, []),

            // Food
            CreateProduct("Artisan Coffee Beans", "food", 24.99m, null,
                "Single-origin arabica beans, medium roast, 1kg bag.",
                ["coffee", "organic", "fairtrade"], sellersByCategory["food"], false, false, now,
                ["Medium Roast", "Dark Roast", "Light Roast"]),
            CreateProduct("Matcha Green Tea", "food", 19.99m, null,
                "Premium organic green tea from Japanese gardens.",
                ["tea", "organic", "premium"], sellersByCategory["food"], false, false, now,
                ["Ceremonial Grade", "Culinary Grade"]),
            CreateProduct("Dark Chocolate Selection", "food", 34.99m, null,
                "Assorted single-origin dark chocolate bars with 70-85% cacao.",
                ["chocolate", "snacks", "gourmet"], sellersByCategory["food"], false, false, now,
                ["70% Cacao", "85% Cacao", "Mixed"]),
            CreateProduct("Organic Honey Collection", "food", 29.99m, null,
                "Set of 3 raw honey varieties from sustainable beekeepers.",
                ["pantry", "organic", "natural-sweetener"], sellersByCategory["food"], false, false, now,
                ["Wildflower", "Manuka", "Clover"]),
            CreateProduct("Premium Olive Oil", "food", 32.99m, null,
                "Cold-pressed extra virgin olive oil from Tuscany, 750ml.",
                ["cooking", "italian", "organic"], sellersByCategory["food"], true, true, now,
                ["Classic", "Garlic Infused", "Chili Infused"])
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        // Create variations for products with colours
        foreach (var product in products.Where(p => p.Variations?.Count > 0))
        {
            foreach (var variation in product.Variations!)
            {
                variation.ProductId = product.Id;
                context.ProductVariations.Add(variation);
            }
        }

        await context.SaveChangesAsync();
    }

    private static ProductEntity CreateProduct(
        string name,
        string category,
        decimal price,
        decimal? originalPrice,
        string description,
        List<string> tags,
        Guid sellerId,
        bool isTrending,
        bool isFeatured,
        DateTime now,
        List<string> colours)
    {
        var product = new ProductEntity
        {
            Name = name,
            Handle = GenerateHandle(name),
            Category = category,
            CategoryPath = category,
            Price = price,
            OriginalPrice = originalPrice,
            Description = description,
            ImageUrl = GeneratePlaceholderUrl(name, category),
            Tags = tags,
            SellerId = sellerId,
            IsTrending = isTrending,
            IsFeatured = isFeatured,
            PublishedAt = now.AddDays(-Random.Shared.Next(1, 30)),
            UpdatedAt = now,
            CreatedAt = now,
            Color = colours.FirstOrDefault() ?? "Default",
            Size = "Default"
        };

        // Create variations list (will be added after main product is saved)
        if (colours.Count > 0)
        {
            product.Variations = colours.Select(c => new ProductVariationEntity
            {
                Color = c,
                Size = "Default",
                Price = price,
                OriginalPrice = originalPrice,
                ImageUrl = GeneratePlaceholderUrl($"{name}-{c}", category),
                StockQuantity = Random.Shared.Next(10, 100),
                AvailabilityStatus = AvailabilityStatus.InStock,
                IsActive = true
            }).ToList();
        }

        return product;
    }

    private static string GenerateHandle(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("&", "and")
            .Replace("--", "-");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_")
            .ToLowerInvariant();
    }

    #region Generated Data Models

    private class GeneratedSeller
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? LogoUrl { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsVerified { get; set; }
        public List<GeneratedProduct> Products { get; set; } = [];
    }

    private class GeneratedProduct
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? ColourVariants { get; set; }
        public bool IsTrending { get; set; }
        public bool IsFeatured { get; set; }
        public List<GeneratedImage>? Images { get; set; }
    }

    private class GeneratedImage
    {
        public string? FilePath { get; set; }
        public string? Variant { get; set; }
        public bool IsPrimary { get; set; }
    }

    #endregion
}
