using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates products using Shopify taxonomy categories with Ollama enhancement.
/// </summary>
public class ShopifyProductGenerator
{
    private readonly HttpClient _httpClient;
    private readonly GenerationConfig _config;
    private readonly ShopifyTaxonomyReader _taxonomyReader;
    private readonly Random _random;

    // Common product attributes for various categories
    private static readonly string[] CommonColors = 
        ["Black", "White", "Gray", "Navy", "Red", "Blue", "Green", "Brown", "Beige", "Pink", "Purple", "Orange", "Yellow", "Silver", "Gold"];
    
    private static readonly string[] CommonMaterials = 
        ["Cotton", "Polyester", "Leather", "Metal", "Plastic", "Wood", "Glass", "Ceramic", "Silicone", "Bamboo", "Stainless Steel", "Aluminum"];
    
    private static readonly string[] CommonSizes = 
        ["XS", "S", "M", "L", "XL", "XXL", "One Size", "Small", "Medium", "Large"];

    private static readonly string[] QualityTiers = 
        ["Premium", "Professional", "Essential", "Budget", "Luxury", "Standard", "Economy", "Deluxe"];

    public ShopifyProductGenerator(
        HttpClient httpClient,
        GenerationConfig config,
        ShopifyTaxonomyReader taxonomyReader)
    {
        _httpClient = httpClient;
        _config = config;
        _taxonomyReader = taxonomyReader;
        _random = new Random();

        _httpClient.BaseAddress = new Uri(config.OllamaBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.OllamaTimeoutSeconds);
    }

    /// <summary>
    /// Check if Ollama is available.
    /// </summary>
    public async Task<bool> IsOllamaAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generate products for random Shopify categories.
    /// </summary>
    public async Task<List<GeneratedProduct>> GenerateProductsAsync(
        int categoryCount,
        int productsPerCategory,
        bool useOllama = true,
        CancellationToken cancellationToken = default)
    {
        var categories = await _taxonomyReader.GetRandomCategoriesAsync(categoryCount, _random, cancellationToken);
        var products = new List<GeneratedProduct>();

        AnsiConsole.MarkupLine($"[blue]Generating products for {categories.Count} categories[/]");

        foreach (var category in categories)
        {
            AnsiConsole.MarkupLine($"  [dim]Category:[/] {Markup.Escape(category.FullName)}");
            
            var categoryProducts = await GenerateProductsForCategoryAsync(
                category, 
                productsPerCategory, 
                useOllama, 
                cancellationToken);
            
            products.AddRange(categoryProducts);
        }

        return products;
    }

    /// <summary>
    /// Generate products for a specific Shopify category.
    /// </summary>
    public async Task<List<GeneratedProduct>> GenerateProductsForCategoryAsync(
        ShopifyCategory category,
        int count,
        bool useOllama = true,
        CancellationToken cancellationToken = default)
    {
        var products = new List<GeneratedProduct>();

        // Generate base products using category info
        for (var i = 0; i < count; i++)
        {
            var product = GenerateBaseProduct(category);
            products.Add(product);
        }

        // Optionally enhance with Ollama for better descriptions
        if (useOllama && await IsOllamaAvailableAsync(cancellationToken))
        {
            products = await EnhanceProductsWithOllamaAsync(products, category, cancellationToken);
        }

        return products;
    }

    /// <summary>
    /// Generate a base product from a Shopify category.
    /// </summary>
    private GeneratedProduct GenerateBaseProduct(ShopifyCategory category)
    {
        var topCategory = category.GetTopCategory();
        var priceRange = GetPriceRangeForCategory(topCategory);
        var price = Math.Round((decimal)(_random.NextDouble() * (double)(priceRange.max - priceRange.min) + (double)priceRange.min), 2);

        // Occasionally add a sale price
        decimal? originalPrice = null;
        if (_random.NextDouble() < 0.25)
        {
            originalPrice = Math.Round(price * (decimal)(1 + _random.NextDouble() * 0.4 + 0.1), 2);
        }

        var color = CommonColors[_random.Next(CommonColors.Length)];
        var quality = _random.NextDouble() < 0.5 ? "" : QualityTiers[_random.Next(QualityTiers.Length)];
        
        var name = GenerateProductName(category, quality, color);
        var description = GenerateBasicDescription(category);
        var imagePrompt = GenerateImagePrompt(category, color);

        return new GeneratedProduct
        {
            Name = name,
            Description = description,
            Category = category.GetSlug(),
            Price = price,
            OriginalPrice = originalPrice,
            Tags = GenerateTags(category),
            IsTrending = _random.NextDouble() < 0.15,
            IsFeatured = _random.NextDouble() < 0.1,
            ImagePrompt = imagePrompt,
            ColourVariants = CommonColors.OrderBy(_ => _random.Next()).Take(3).ToList()
        };
    }

    private string GenerateProductName(ShopifyCategory category, string quality, string color)
    {
        var baseName = category.Name;
        var nameFormat = _random.Next(5);
        
        return nameFormat switch
        {
            0 => $"{quality} {baseName} in {color}".Trim(),
            1 => $"{color} {baseName}".Trim(),
            2 => $"{quality} {baseName}".Trim(),
            3 => $"{baseName} - {color}".Trim(),
            _ => $"{quality} {color} {baseName}".Trim(),
        };
    }

    private string GenerateBasicDescription(ShopifyCategory category)
    {
        var pathSegments = category.GetPathSegments();
        var categoryPath = string.Join(" > ", pathSegments.Take(3));
        
        return $"High-quality {category.Name.ToLowerInvariant()} in the {categoryPath} category. " +
               "Designed for exceptional performance and reliability.";
    }

    private string GenerateImagePrompt(ShopifyCategory category, string color)
    {
        return $"Professional product photography of a {category.Name.ToLowerInvariant()}, {color} color, " +
               "studio lighting, white background, high detail, commercial product shot, 8k quality";
    }

    private List<string> GenerateTags(ShopifyCategory category)
    {
        var tags = new List<string>();
        
        // Add category path as tags
        foreach (var segment in category.GetPathSegments().Take(3))
        {
            tags.Add(segment.ToLowerInvariant().Replace(" & ", "-").Replace(" ", "-"));
        }
        
        // Add attribute-based tags
        foreach (var attr in category.Attributes.Take(2))
        {
            tags.Add(attr.Handle);
        }

        return tags.Distinct().Take(5).ToList();
    }

    private (decimal min, decimal max) GetPriceRangeForCategory(string topCategory)
    {
        // Default price ranges based on top-level category
        return topCategory.ToLowerInvariant() switch
        {
            "electronics" => (29.99m, 999.99m),
            "apparel & accessories" or "clothing" => (19.99m, 299.99m),
            "home & garden" => (14.99m, 499.99m),
            "sporting goods" => (9.99m, 399.99m),
            "toys & games" => (9.99m, 149.99m),
            "health & beauty" => (4.99m, 199.99m),
            "food, beverages & tobacco" => (2.99m, 79.99m),
            "animals & pet supplies" => (4.99m, 199.99m),
            "vehicles & parts" => (19.99m, 999.99m),
            "office supplies" => (4.99m, 299.99m),
            "software" => (9.99m, 499.99m),
            "hardware" => (9.99m, 799.99m),
            _ => (9.99m, 199.99m)
        };
    }

    /// <summary>
    /// Use Ollama to enhance product descriptions.
    /// </summary>
    private async Task<List<GeneratedProduct>> EnhanceProductsWithOllamaAsync(
        List<GeneratedProduct> products,
        ShopifyCategory category,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = BuildEnhancementPrompt(products, category);
            var response = await CallOllamaAsync(prompt, cancellationToken);
            var enhanced = ParseEnhancedProducts(response, products);

            return enhanced;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Ollama enhancement failed, using basic descriptions: {Markup.Escape(ex.Message)}[/]");
            return products;
        }
    }

    private string BuildEnhancementPrompt(List<GeneratedProduct> products, ShopifyCategory category)
    {
        var productList = JsonSerializer.Serialize(products.Select(p => new
        {
            p.Name,
            p.Description,
            p.Tags,
            p.ImagePrompt
        }), new JsonSerializerOptions { WriteIndented = true });

        var jsonExample = """
            {
              "products": [
                {
                  "name": "Enhanced Product Name",
                  "description": "Compelling product description here.",
                  "image_prompt": "Detailed image generation prompt here"
                }
              ]
            }
            """;

        return $"""
            You are a product copywriter for an e-commerce marketplace. Enhance these product listings with compelling, unique descriptions.

            Category: {category.FullName}
            Category Attributes: {string.Join(", ", category.Attributes.Select(a => a.Name))}

            Current products:
            {productList}

            For each product, improve:
            1. Name: Make it catchy and descriptive (keep brand if present)
            2. Description: Make it engaging, highlight benefits, use sensory language (2-3 sentences)
            3. Image prompt: Make it specific for AI image generation (studio product photography style)

            IMPORTANT: Return ONLY valid JSON with no markdown, no code blocks.
            Keep the same structure but with enhanced text:
            
            {jsonExample}

            Enhance now:
            """;
    }

    private async Task<string> CallOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.OllamaModel,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9,
                num_predict = 2048
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

        return responseObj?.Response ?? string.Empty;
    }

    private List<GeneratedProduct> ParseEnhancedProducts(string response, List<GeneratedProduct> originals)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var enhanced = JsonSerializer.Deserialize<EnhancedProductsResponse>(jsonStr);

                if (enhanced?.Products != null)
                {
                    for (var i = 0; i < Math.Min(originals.Count, enhanced.Products.Count); i++)
                    {
                        var original = originals[i];
                        var update = enhanced.Products[i];

                        if (!string.IsNullOrWhiteSpace(update.Name))
                            original.Name = update.Name;
                        
                        if (!string.IsNullOrWhiteSpace(update.Description))
                            original.Description = update.Description;

                        if (!string.IsNullOrWhiteSpace(update.ImagePrompt))
                            original.ImagePrompt = update.ImagePrompt;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[dim]Could not parse Ollama response: {Markup.Escape(ex.Message)}[/]");
        }

        return originals;
    }

    private class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;
    }

    private class EnhancedProductsResponse
    {
        [JsonPropertyName("products")]
        public List<EnhancedProduct> Products { get; set; } = [];
    }

    private class EnhancedProduct
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("image_prompt")]
        public string ImagePrompt { get; set; } = string.Empty;
    }
}
