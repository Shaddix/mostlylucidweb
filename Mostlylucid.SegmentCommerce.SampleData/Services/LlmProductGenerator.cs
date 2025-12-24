using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.SegmentCommerce.SampleData.Models;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates unique products using LLM, constrained by taxonomy rules.
/// Uses taxonomy for valid colors, materials, features - LLM only generates unique names/descriptions.
/// </summary>
public class LlmProductGenerator
{
    private readonly LlmService _llm;
    private readonly GadgetTaxonomy? _taxonomy;
    private readonly HashSet<string> _usedNames = new(StringComparer.OrdinalIgnoreCase);

    public LlmProductGenerator(LlmService llm, GadgetTaxonomy? taxonomy = null)
    {
        _llm = llm;
        _taxonomy = taxonomy;
    }

    /// <summary>
    /// Generate a batch of unique products for a category using LLM.
    /// </summary>
    public async Task<List<GeneratedProduct>> GenerateProductsAsync(
        string category,
        string productType,
        int count,
        CancellationToken ct = default)
    {
        var products = new List<GeneratedProduct>();
        var batchSize = Math.Min(5, count); // Generate in small batches for better LLM output
        var failureCount = 0;
        const int maxFailures = 3;
        
        while (products.Count < count && failureCount < maxFailures)
        {
            var remaining = count - products.Count;
            var toGenerate = Math.Min(batchSize, remaining);
            
            var prompt = BuildProductPrompt(category, productType, toGenerate);
            var response = await _llm.GenerateAsync<LlmProductBatch>(prompt, ct);
            
            if (response?.Products != null && response.Products.Count > 0)
            {
                failureCount = 0; // Reset on success
                
                foreach (var p in response.Products)
                {
                    // Ensure unique names
                    var uniqueName = EnsureUniqueName(p.Name);
                    if (uniqueName == null) continue;
                    
                    // Get valid colors from taxonomy for this product
                    var productDef = GetProductDefinition(category, productType);
                    var validColors = productDef?.Colours ?? new List<string> { "Black", "White", "Grey" };
                    
                    // Build tags from attributes - clean up any comma-separated values
                    var tags = new List<string> { productType.ToLower().Replace(" ", "-") };
                    if (!string.IsNullOrEmpty(p.Variant))
                    {
                        foreach (var v in p.Variant.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            tags.Add(v.Trim().ToLower().Replace(" ", "-"));
                    }
                    if (!string.IsNullOrEmpty(p.Feature))
                    {
                        foreach (var f in p.Feature.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            tags.Add(f.Trim().ToLower().Replace(" ", "-"));
                    }
                    
                    products.Add(new GeneratedProduct
                    {
                        Id = Guid.NewGuid().ToString("N")[..12],
                        Name = uniqueName,
                        Description = p.Description,
                        Category = category,
                        Price = p.Price,
                        OriginalPrice = p.OriginalPrice,
                        Tags = tags.Distinct().Take(5).ToList(),
                        ColourVariants = validColors.Take(4).ToList(), // Use taxonomy colors
                        IsTrending = Random.Shared.NextDouble() < 0.15,
                        IsFeatured = Random.Shared.NextDouble() < 0.1,
                        ImagePrompt = BuildImagePrompt(p)
                    });
                    
                    if (products.Count >= count) break;
                }
            }
            else
            {
                // LLM failed - increment failure counter
                failureCount++;
            }
        }
        
        // If LLM failed too many times, fill remaining with fallback products
        while (products.Count < count)
        {
            var fallback = GenerateFallbackProduct(category, productType);
            products.Add(fallback);
        }
        
        return products;
    }

    private string BuildProductPrompt(string category, string productType, int count)
    {
        // Get constraints from taxonomy if available
        var productDef = GetProductDefinition(category, productType);
        
        var validColors = productDef?.Colours ?? new List<string> { "Black", "White", "Grey", "Navy", "Brown" };
        var validMaterials = productDef?.Materials ?? new List<string> { "plastic", "metal", "fabric" };
        var validFeatures = productDef?.Features ?? new List<string> { "durable", "lightweight", "premium" };
        var validVariants = productDef?.Variants ?? new List<string> { "standard", "compact", "large" };
        var priceRange = productDef?.PriceRange ?? new PriceRange { Min = 29.99m, Max = 299.99m };
        var existingBrands = productDef?.Brands ?? new List<string>();

        var jsonExample = """
            {
              "products": [
                {
                  "name": "BrandName VariantType ProductType",
                  "description": "Brief description with key feature.",
                  "price": 79.99,
                  "original_price": null,
                  "color": "Black",
                  "material": "leather",
                  "variant": "standard",
                  "feature": "wireless"
                }
              ]
            }
            """;
            
        return $"""
            Generate {count} unique {productType} products for the {category} category.

            CONSTRAINTS (you MUST use these exact values):
            - Colors: {string.Join(", ", validColors)}
            - Materials: {string.Join(", ", validMaterials)}
            - Features: {string.Join(", ", validFeatures)}
            - Variants: {string.Join(", ", validVariants)}
            - Price range: £{priceRange.Min:F2} to £{priceRange.Max:F2}
            
            BRAND NAME RULES:
            - Create unique, realistic brand names (1-2 words)
            - Similar style to: {string.Join(", ", existingBrands.Take(3))}
            - Do NOT reuse these exact names
            
            PRODUCT NAME FORMAT:
            "[Brand] [variant] [product-type]" - e.g. "AudioPulse wireless headphones"
            
            Return ONLY valid JSON:
            {jsonExample}
            
            Generate exactly {count} unique products:
            """;
    }

    private string? EnsureUniqueName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        
        var baseName = name.Trim();
        if (!_usedNames.Contains(baseName))
        {
            _usedNames.Add(baseName);
            return baseName;
        }
        
        // Try adding a unique suffix
        for (int i = 2; i <= 10; i++)
        {
            var variant = $"{baseName} #{i}";
            if (!_usedNames.Contains(variant))
            {
                _usedNames.Add(variant);
                return variant;
            }
        }
        
        // Last resort: add random suffix
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..4].ToUpper();
        var uniqueName = $"{baseName} {uniqueSuffix}";
        _usedNames.Add(uniqueName);
        return uniqueName;
    }

    private string BuildImagePrompt(LlmProduct p)
    {
        var parts = new List<string>
        {
            "Professional product photography",
            "studio lighting",
            "white background",
            "high detail",
            "commercial product shot",
            "8k quality"
        };
        
        if (!string.IsNullOrEmpty(p.Material))
            parts.Insert(2, $"{p.Material} texture");
            
        if (!string.IsNullOrEmpty(p.Color))
            parts.Insert(2, $"{p.Color} color");
            
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Get product type definition from taxonomy for constraints.
    /// </summary>
    private ProductType? GetProductDefinition(string category, string productType)
    {
        if (_taxonomy == null) return null;
        
        if (!_taxonomy.Categories.TryGetValue(category, out var cat)) return null;
        
        return cat.Subcategories.Values
            .SelectMany(s => s.Products)
            .FirstOrDefault(p => p.Type.Equals(productType, StringComparison.OrdinalIgnoreCase));
    }

    private GeneratedProduct GenerateFallbackProduct(string category, string productType)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var name = $"{category.ToUpperInvariant()[..3]}-{productType}-{id}";
        
        _usedNames.Add(name);
        
        return new GeneratedProduct
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = name,
            Description = $"Quality {productType} for everyday use.",
            Category = category,
            Price = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 20), 2),
            Tags = [productType.ToLower()],
            ColourVariants = ["Black", "White", "Grey"],
            ImagePrompt = "Professional product photography, studio lighting, white background"
        };
    }

    #region LLM Response Models

    private class LlmProductBatch
    {
        [JsonPropertyName("products")]
        public List<LlmProduct>? Products { get; set; }
    }

    private class LlmProduct
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("original_price")]
        public decimal? OriginalPrice { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("material")]
        public string? Material { get; set; }

        [JsonPropertyName("variant")]
        public string? Variant { get; set; }

        [JsonPropertyName("feature")]
        public string? Feature { get; set; }
    }

    #endregion
}
