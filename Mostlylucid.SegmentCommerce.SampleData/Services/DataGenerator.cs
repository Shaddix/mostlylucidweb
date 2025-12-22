using System.Security.Cryptography;
using System.Text;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Main data generator that orchestrates LLM, embeddings, and image generation.
/// Generates sellers -> products -> customers with proper relationships.
/// </summary>
public class DataGenerator : IDisposable
{
    private readonly LlmService _llm;
    private readonly EmbeddingService _embeddings;
    private readonly ComfyUIImageGenerator _imageGenerator;
    private readonly GadgetTaxonomy _taxonomy;
    private readonly GenerationSettings _settings;
    private readonly Random _random = new();
    private bool _disposed;

    public DataGenerator(
        LlmService llm,
        EmbeddingService embeddings,
        ComfyUIImageGenerator imageGenerator,
        GadgetTaxonomy taxonomy,
        GenerationSettings settings)
    {
        _llm = llm;
        _embeddings = embeddings;
        _imageGenerator = imageGenerator;
        _taxonomy = taxonomy;
        _settings = settings;
    }

    /// <summary>
    /// Generate a complete dataset with sellers, products, and customers.
    /// </summary>
    public async Task<GeneratedDataset> GenerateAsync(CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var dataset = new GeneratedDataset();

        // Check LLM availability (only if enabled)
        var llmAvailable = _settings.EnableLlm && await _llm.IsAvailableAsync(ct);
        if (!_settings.EnableLlm)
        {
            AnsiConsole.MarkupLine("[yellow]LLM disabled - using fallback generation[/]");
        }
        else if (!llmAvailable)
        {
            AnsiConsole.MarkupLine("[yellow]LLM not available - using fallback generation[/]");
        }

        // Check ComfyUI availability
        var imagesAvailable = await _imageGenerator.IsAvailableAsync(ct);
        if (!imagesAvailable)
        {
            AnsiConsole.MarkupLine("[yellow]ComfyUI not available - skipping image generation[/]");
        }

        // Initialize embeddings
        await _embeddings.InitializeAsync(ct);

        // 1. Generate Sellers
        AnsiConsole.MarkupLine("\n[bold cyan]Phase 1: Generating Sellers[/]");
        dataset.Sellers = await GenerateSellersAsync(llmAvailable, ct);

        // 2. Generate Products for each seller
        AnsiConsole.MarkupLine("\n[bold cyan]Phase 2: Generating Products[/]");
        await GenerateProductsForSellersAsync(dataset.Sellers, llmAvailable, imagesAvailable, ct);

        // 3. Generate Customers
        AnsiConsole.MarkupLine("\n[bold cyan]Phase 3: Generating Customers[/]");
        dataset.Customers = await GenerateCustomersAsync(llmAvailable, ct);

        // 4. Generate embeddings for all entities
        AnsiConsole.MarkupLine("\n[bold cyan]Phase 4: Computing Embeddings[/]");
        await GenerateEmbeddingsAsync(dataset, ct);

        // Compute stats
        dataset.Stats = new GenerationStats
        {
            TotalSellers = dataset.Sellers.Count,
            TotalProducts = dataset.Sellers.Sum(s => s.Products.Count),
            TotalCustomers = dataset.Customers.Count,
            TotalImages = dataset.Sellers.Sum(s => s.Products.Sum(p => p.Images.Count)),
            TotalEmbeddings = dataset.Sellers.Count(s => s.Embedding != null)
                + dataset.Sellers.Sum(s => s.Products.Count(p => p.Embedding != null))
                + dataset.Customers.Count(c => c.Embedding != null),
            Duration = DateTime.UtcNow - startTime
        };

        return dataset;
    }

    #region Seller Generation

    private async Task<List<GeneratedSeller>> GenerateSellersAsync(bool useLlm, CancellationToken ct)
    {
        var sellers = new List<GeneratedSeller>();
        var categories = _taxonomy.Categories.Keys.ToList();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating sellers[/]", maxValue: _settings.SellersCount);

                for (int i = 0; i < _settings.SellersCount; i++)
                {
                    // Assign 1-3 categories to each seller
                    var sellerCategories = categories
                        .OrderBy(_ => _random.Next())
                        .Take(_random.Next(1, Math.Min(4, categories.Count + 1)))
                        .ToArray();

                    var seller = useLlm
                        ? await GenerateSellerWithLlmAsync(sellerCategories, i, ct)
                        : GenerateSellerFallback(sellerCategories, i);

                    sellers.Add(seller);
                    task.Increment(1);
                }
            });

        return sellers;
    }

    private async Task<GeneratedSeller> GenerateSellerWithLlmAsync(
        string[] categories, int index, CancellationToken ct)
    {
        var categoryNames = categories.Select(c => _taxonomy.Categories[c].DisplayName);
        var prompt = $$"""
            Generate a unique e-commerce seller persona for these categories: {{string.Join(", ", categoryNames)}}.
            
            Return JSON only:
            {
              "name": "Creative business name (2-4 words)",
              "tagline": "Short catchy tagline",
              "description": "2-3 sentence business description",
              "specialties": ["specialty1", "specialty2", "specialty3"]
            }
            """;

        var response = await _llm.GenerateAsync<LlmSellerResponse>(prompt, ct);

        var seller = new GeneratedSeller
        {
            Id = $"seller_{index:D4}",
            Categories = categories,
            Rating = Math.Round(3.5 + _random.NextDouble() * 1.5, 1),
            ReviewCount = _random.Next(50, 5000),
            IsVerified = _random.NextDouble() > 0.3
        };

        if (response != null && !string.IsNullOrEmpty(response.Name))
        {
            seller.Name = response.Name;
            seller.Description = response.Description ?? $"Quality {string.Join(" and ", categories)} products.";
            seller.Bio = response.Tagline ?? seller.Name;
            seller.Specialties = response.Specialties?.ToArray() ?? Array.Empty<string>();
        }
        else
        {
            // Fallback
            var fallback = GenerateSellerFallback(categories, int.Parse(seller.Id.Split('_')[1]));
            seller.Name = fallback.Name;
            seller.Description = fallback.Description;
            seller.Bio = fallback.Bio;
            seller.Specialties = fallback.Specialties;
        }

        seller.Email = $"contact@{seller.Name.ToLower().Replace(" ", "").Replace("'", "")}.com";
        seller.Website = $"https://{seller.Name.ToLower().Replace(" ", "-").Replace("'", "")}.com";

        return seller;
    }

    private GeneratedSeller GenerateSellerFallback(string[] categories, int index)
    {
        var prefixes = new[] { "Prime", "Elite", "Modern", "Urban", "Classic", "Tech", "Pure", "Fresh", "Bold", "Swift" };
        var suffixes = new[] { "Store", "Shop", "Goods", "Direct", "Hub", "Market", "Outlet", "Supply", "Co", "Trading" };
        var category = _taxonomy.Categories[categories[0]];

        var name = $"{prefixes[_random.Next(prefixes.Length)]} {category.DisplayName} {suffixes[_random.Next(suffixes.Length)]}";

        return new GeneratedSeller
        {
            Id = $"seller_{index:D4}",
            Name = name,
            Description = $"Your trusted source for quality {category.DisplayName.ToLower()} products.",
            Bio = $"Specializing in {category.DisplayName.ToLower()} since 2020",
            Categories = categories,
            Specialties = new[] { category.DisplayName, "Fast Shipping", "Quality Products" },
            Email = $"contact@{name.ToLower().Replace(" ", "")}.com",
            Rating = Math.Round(3.5 + _random.NextDouble() * 1.5, 1),
            ReviewCount = _random.Next(50, 5000),
            IsVerified = _random.NextDouble() > 0.3
        };
    }

    #endregion

    #region Product Generation

    private async Task GenerateProductsForSellersAsync(
        List<GeneratedSeller> sellers, bool useLlm, bool generateImages, CancellationToken ct)
    {
        var totalProducts = sellers.Count * _settings.ProductsPerSeller;

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating products[/]", maxValue: totalProducts);

                foreach (var seller in sellers)
                {
                    for (int i = 0; i < _settings.ProductsPerSeller; i++)
                    {
                        // Pick a category this seller specializes in
                        var category = seller.Categories[_random.Next(seller.Categories.Length)];
                        var productType = _taxonomy.GetRandomProductType(category, _random);

                        if (productType == null)
                        {
                            task.Increment(1);
                            continue;
                        }

                        var product = useLlm
                            ? await GenerateProductWithLlmAsync(seller, category, productType, ct)
                            : GenerateProductFallback(seller, category, productType);

                        product.SellerId = seller.Id;

                        // Generate images if available
                        if (generateImages)
                        {
                            try
                            {
                                product.Images = await _imageGenerator.GenerateProductImagesAsync(product, ct);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Image gen failed: {ex.Message}[/]");
                            }
                        }

                        seller.Products.Add(product);
                        task.Increment(1);
                    }
                }
            });
    }

    private async Task<GeneratedProduct> GenerateProductWithLlmAsync(
        GeneratedSeller seller, string category, ProductType productType, CancellationToken ct)
    {
        var variant = productType.Variants.Count > 0
            ? productType.Variants[_random.Next(productType.Variants.Count)]
            : "";
        var features = productType.Features.OrderBy(_ => _random.Next()).Take(3).ToList();
        var colour = productType.Colours.Count > 0
            ? productType.Colours[_random.Next(productType.Colours.Count)]
            : "Black";

        var prompt = $$"""
            Create a unique product listing for: {{variant}} {{productType.Type}}
            
            Seller: {{seller.Name}} (specializes in {{string.Join(", ", seller.Specialties)}})
            Features to highlight: {{string.Join(", ", features)}}
            Primary colour: {{colour}}
            
            Return JSON only:
            {
              "name": "Creative product name (3-5 words, include brand-style prefix)",
              "description": "2-3 sentence compelling product description highlighting benefits",
              "short_description": "One sentence summary",
              "image_prompt": "Detailed prompt for product photography (studio lighting, white background, etc)",
              "tags": ["tag1", "tag2", "tag3"],
              "features": ["feature1", "feature2"]
            }
            """;

        var response = await _llm.GenerateAsync<LlmProductResponse>(prompt, ct);

        var priceRange = productType.PriceRange ?? _taxonomy.Categories[category].PriceRange;
        var price = Math.Round((decimal)(_random.NextDouble() * (double)(priceRange.Max - priceRange.Min) + (double)priceRange.Min), 2);

        var product = new GeneratedProduct
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Category = category,
            Price = price,
            OriginalPrice = _random.NextDouble() < 0.25
                ? Math.Round(price * (1.1m + (decimal)_random.NextDouble() * 0.3m), 2)
                : null,
            ColourVariants = productType.Colours.OrderBy(_ => _random.Next()).Take(3).ToList(),
            IsTrending = _random.NextDouble() < 0.15,
            IsFeatured = _random.NextDouble() < 0.1
        };

        if (response != null && !string.IsNullOrEmpty(response.Name))
        {
            product.Name = response.Name;
            product.Description = response.Description ?? product.Description;
            product.ImagePrompt = response.ImagePrompt ?? product.ImagePrompt;
            product.Tags = response.Tags ?? new List<string>();
        }
        else
        {
            // Fallback
            var fallback = GenerateProductFallback(seller, category, productType);
            product.Name = fallback.Name;
            product.Description = fallback.Description;
            product.ImagePrompt = fallback.ImagePrompt;
            product.Tags = fallback.Tags;
        }

        return product;
    }

    private GeneratedProduct GenerateProductFallback(
        GeneratedSeller seller, string category, ProductType productType)
    {
        var variant = productType.Variants.Count > 0
            ? productType.Variants[_random.Next(productType.Variants.Count)]
            : "";
        var brand = productType.Brands.Count > 0
            ? productType.Brands[_random.Next(productType.Brands.Count)]
            : seller.Name.Split(' ')[0];
        var features = productType.Features.OrderBy(_ => _random.Next()).Take(3).ToList();
        var colour = productType.Colours.Count > 0
            ? productType.Colours[_random.Next(productType.Colours.Count)]
            : "Black";

        var suffixes = new[] { "Pro", "Elite", "Plus", "Max", "Ultra", "X", "Series" };
        var suffix = _random.NextDouble() < 0.5 ? $" {suffixes[_random.Next(suffixes.Length)]}" : "";

        var priceRange = productType.PriceRange ?? _taxonomy.Categories[category].PriceRange;
        var price = Math.Round((decimal)(_random.NextDouble() * (double)(priceRange.Max - priceRange.Min) + (double)priceRange.Min), 2);

        return new GeneratedProduct
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = $"{brand} {variant} {productType.Type}{suffix}".Trim(),
            Description = $"{variant} {productType.Type} featuring {string.Join(", ", features)}. Designed for quality and performance.",
            Category = category,
            Price = price,
            OriginalPrice = _random.NextDouble() < 0.25
                ? Math.Round(price * (1.1m + (decimal)_random.NextDouble() * 0.3m), 2)
                : null,
            Tags = new List<string> { productType.Type }.Concat(features.Take(2).Select(f => f.ToLower().Replace(" ", "-"))).ToList(),
            ImagePrompt = $"Professional product photography of {variant} {productType.Type}, {colour} colour, studio lighting, white background, 8k quality",
            ColourVariants = productType.Colours.OrderBy(_ => _random.Next()).Take(3).ToList(),
            IsTrending = _random.NextDouble() < 0.15,
            IsFeatured = _random.NextDouble() < 0.1
        };
    }

    #endregion

    #region Customer Generation

    private async Task<List<GeneratedCustomer>> GenerateCustomersAsync(bool useLlm, CancellationToken ct)
    {
        var customers = new List<GeneratedCustomer>();
        var categories = _taxonomy.Categories.Keys.ToList();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating customers[/]", maxValue: _settings.CustomersCount);

                // Generate in batches for efficiency
                var batchSize = _settings.BatchSize;
                for (int i = 0; i < _settings.CustomersCount; i += batchSize)
                {
                    var batchCount = Math.Min(batchSize, _settings.CustomersCount - i);

                    for (int j = 0; j < batchCount; j++)
                    {
                        var customerCategories = categories
                            .OrderBy(_ => _random.Next())
                            .Take(_random.Next(1, Math.Min(4, categories.Count + 1)))
                            .ToList();

                        var customer = useLlm
                            ? await GenerateCustomerWithLlmAsync(customerCategories, ct)
                            : GenerateCustomerFallback(customerCategories);

                        customers.Add(customer);
                        task.Increment(1);
                    }
                }
            });

        return customers;
    }

    private async Task<GeneratedCustomer> GenerateCustomerWithLlmAsync(
        List<string> categories, CancellationToken ct)
    {
        var categoryNames = categories.Select(c => _taxonomy.Categories[c].DisplayName);

        var prompt = $$"""
            Generate a shopper persona interested in: {{string.Join(", ", categoryNames)}}.
            
            Return JSON only:
            {
              "persona": "Brief persona description (e.g. 'Tech enthusiast who values quality')",
              "name": "Realistic first name",
              "bio": "One sentence about their shopping habits",
              "age": 25,
              "shopping_style": "budget|value|premium|luxury",
              "preferred_categories": ["category1", "category2"]
            }
            """;

        var response = await _llm.GenerateAsync<LlmCustomerResponse>(prompt, ct);

        var customer = new GeneratedCustomer
        {
            ProfileKey = Hash($"customer-{Guid.NewGuid():N}"),
            RecentCategories = categories
        };

        // Generate interest scores
        foreach (var cat in categories)
        {
            customer.Interests[cat] = Math.Round(0.3 + _random.NextDouble() * 0.7, 2);
        }

        // Generate behavioral signals
        customer.Signals = GenerateCustomerSignals(categories);

        if (response != null)
        {
            customer.Persona = response.Persona;
            customer.DisplayName = response.Name;
            customer.Bio = response.Bio;
            customer.Age = response.Age ?? _random.Next(18, 65);

            var style = response.ShoppingStyle?.ToLower() ?? "value";
            customer.PricePreference = style switch
            {
                "budget" => new PricePreference { Min = 0, Max = 50, PrefersDeals = true },
                "value" => new PricePreference { Min = 20, Max = 150, PrefersDeals = true },
                "premium" => new PricePreference { Min = 50, Max = 500, PrefersDeals = false },
                "luxury" => new PricePreference { Min = 100, Max = 2000, PrefersLuxury = true },
                _ => new PricePreference { Min = 20, Max = 200 }
            };
        }
        else
        {
            var fallback = GenerateCustomerFallback(categories);
            customer.Persona = fallback.Persona;
            customer.DisplayName = fallback.DisplayName;
            customer.Bio = fallback.Bio;
            customer.Age = fallback.Age;
            customer.PricePreference = fallback.PricePreference;
        }

        return customer;
    }

    private GeneratedCustomer GenerateCustomerFallback(List<string> categories)
    {
        var personas = new[]
        {
            ("Tech Enthusiast", "Early adopter who loves the latest gadgets", "premium"),
            ("Budget Shopper", "Always looking for the best deals", "budget"),
            ("Quality Seeker", "Values durability and craftsmanship", "value"),
            ("Trend Follower", "Keeps up with the latest trends", "value"),
            ("Practical Buyer", "Focuses on functionality over style", "budget"),
            ("Luxury Lover", "Appreciates premium brands and experiences", "luxury"),
            ("Fitness Fanatic", "Dedicated to health and wellness", "value"),
            ("Home Improver", "Always working on home projects", "value")
        };

        var (persona, bio, style) = personas[_random.Next(personas.Length)];
        var firstNames = new[] { "Alex", "Jordan", "Taylor", "Morgan", "Casey", "Riley", "Quinn", "Avery", "Sam", "Jamie" };

        var customer = new GeneratedCustomer
        {
            ProfileKey = Hash($"customer-{Guid.NewGuid():N}"),
            Persona = persona,
            DisplayName = firstNames[_random.Next(firstNames.Length)],
            Bio = bio,
            Age = _random.Next(18, 65),
            RecentCategories = categories,
            PricePreference = style switch
            {
                "budget" => new PricePreference { Min = 0, Max = 50, PrefersDeals = true },
                "value" => new PricePreference { Min = 20, Max = 150, PrefersDeals = true },
                "premium" => new PricePreference { Min = 50, Max = 500, PrefersDeals = false },
                "luxury" => new PricePreference { Min = 100, Max = 2000, PrefersLuxury = true },
                _ => new PricePreference { Min = 20, Max = 200 }
            }
        };

        foreach (var cat in categories)
        {
            customer.Interests[cat] = Math.Round(0.3 + _random.NextDouble() * 0.7, 2);
        }

        customer.Signals = GenerateCustomerSignals(categories);

        return customer;
    }

    private List<GeneratedSignal> GenerateCustomerSignals(List<string> categories)
    {
        var signals = new List<GeneratedSignal>();

        foreach (var cat in categories)
        {
            // Views
            var viewCount = _random.Next(3, 15);
            for (int i = 0; i < viewCount; i++)
            {
                signals.Add(new GeneratedSignal
                {
                    SignalType = "view",
                    Category = cat,
                    Weight = 0.1 + _random.NextDouble() * 0.2,
                    Timestamp = DateTime.UtcNow.AddDays(-_random.Next(1, 30))
                });
            }

            // Cart adds
            if (_random.NextDouble() > 0.5)
            {
                var cartCount = _random.Next(1, 5);
                for (int i = 0; i < cartCount; i++)
                {
                    signals.Add(new GeneratedSignal
                    {
                        SignalType = "cart_add",
                        Category = cat,
                        Weight = 0.3 + _random.NextDouble() * 0.3,
                        Timestamp = DateTime.UtcNow.AddDays(-_random.Next(1, 14))
                    });
                }
            }

            // Purchases
            if (_random.NextDouble() > 0.7)
            {
                signals.Add(new GeneratedSignal
                {
                    SignalType = "purchase",
                    Category = cat,
                    Weight = 0.8 + _random.NextDouble() * 0.2,
                    Timestamp = DateTime.UtcNow.AddDays(-_random.Next(1, 60))
                });
            }
        }

        return signals;
    }

    #endregion

    #region Embeddings

    private async Task GenerateEmbeddingsAsync(GeneratedDataset dataset, CancellationToken ct)
    {
        if (!_settings.EnableEmbeddings) return;

        var totalItems = dataset.Sellers.Count
            + dataset.Sellers.Sum(s => s.Products.Count)
            + dataset.Customers.Count;

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Computing embeddings[/]", maxValue: totalItems);

                // Seller embeddings
                foreach (var seller in dataset.Sellers)
                {
                    var text = $"{seller.Name}. {seller.Description}. Specializes in {string.Join(", ", seller.Specialties)}";
                    seller.Embedding = await _embeddings.GenerateAsync(text, ct);
                    task.Increment(1);
                }

                // Product embeddings
                foreach (var seller in dataset.Sellers)
                {
                    foreach (var product in seller.Products)
                    {
                        var text = $"{product.Name}. {product.Description}. Tags: {string.Join(", ", product.Tags)}";
                        product.Embedding = await _embeddings.GenerateAsync(text, ct);
                        task.Increment(1);
                    }
                }

                // Customer embeddings
                foreach (var customer in dataset.Customers)
                {
                    var interests = string.Join(", ", customer.Interests.OrderByDescending(kv => kv.Value).Select(kv => kv.Key));
                    var text = $"{customer.Persona}. Interested in {interests}. {customer.Bio}";
                    customer.Embedding = await _embeddings.GenerateAsync(text, ct);
                    task.Increment(1);
                }
            });
    }

    #endregion

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _llm.Dispose();
        _embeddings.Dispose();
        _imageGenerator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Generation settings from appsettings.
/// </summary>
public class GenerationSettings
{
    public string OutputPath { get; set; } = "./Output";
    public int SellersCount { get; set; } = 20;
    public int ProductsPerSeller { get; set; } = 10;
    public int CustomersCount { get; set; } = 500;
    public int BatchSize { get; set; } = 5;
    public bool EnableLlm { get; set; } = true;
    public bool EnableEmbeddings { get; set; } = true;
    public bool EnableImages { get; set; } = true;
}
