using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates products using the taxonomy, with optional Ollama enhancement.
/// </summary>
public class TaxonomyProductGenerator
{
    private readonly HttpClient _httpClient;
    private readonly GenerationConfig _config;
    private readonly GadgetTaxonomy _taxonomy;
    private readonly Random _random;

    public TaxonomyProductGenerator(
        HttpClient httpClient,
        GenerationConfig config,
        GadgetTaxonomy taxonomy)
    {
        _httpClient = httpClient;
        _config = config;
        _taxonomy = taxonomy;
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
    /// Generate products for a category using taxonomy + optional Ollama enhancement.
    /// </summary>
    public async Task<List<GeneratedProduct>> GenerateProductsAsync(
        string categorySlug,
        int count,
        bool useOllama = true,
        CancellationToken cancellationToken = default)
    {
        if (!_taxonomy.Categories.TryGetValue(categorySlug, out var category))
        {
            throw new ArgumentException($"Unknown category: {categorySlug}");
        }

        var products = new List<GeneratedProduct>();

        // Get all product types for this category
        var productTypes = category.Subcategories.Values
            .SelectMany(s => s.Products)
            .ToList();

        if (productTypes.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No product types defined for category {categorySlug}[/]");
            return products;
        }

        // Generate products using taxonomy
        for (var i = 0; i < count; i++)
        {
            var productType = productTypes[_random.Next(productTypes.Count)];
            var product = productType.GenerateRandom(categorySlug, _random);
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
    /// Generate all products for all categories.
    /// </summary>
    public async Task<Dictionary<string, List<GeneratedProduct>>> GenerateAllProductsAsync(
        bool useOllama = true,
        IProgress<(string category, int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, List<GeneratedProduct>>();
        var categories = _taxonomy.Categories.Keys.ToList();
        var current = 0;

        foreach (var categorySlug in categories)
        {
            current++;
            progress?.Report((categorySlug, current, categories.Count));

            var products = await GenerateProductsAsync(
                categorySlug,
                _config.ProductsPerCategory,
                useOllama,
                cancellationToken);

            result[categorySlug] = products;
        }

        return result;
    }

    /// <summary>
    /// Use Ollama to enhance product descriptions and make them more compelling.
    /// </summary>
    private async Task<List<GeneratedProduct>> EnhanceProductsWithOllamaAsync(
        List<GeneratedProduct> products,
        TaxonomyCategory category,
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
            AnsiConsole.MarkupLine($"[yellow]Ollama enhancement failed, using taxonomy-only: {ex.Message}[/]");
            return products;
        }
    }

    private string BuildEnhancementPrompt(List<GeneratedProduct> products, TaxonomyCategory category)
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
                  "name": "Original Name",
                  "description": "Enhanced description here.",
                  "image_prompt": "Enhanced image prompt here"
                }
              ]
            }
            """;

        return $"""
            You are a product copywriter for an e-commerce marketplace with products ranging from budget to premium. Enhance these {category.DisplayName} product listings with more compelling, unique descriptions that match their quality level.

            Category: {category.DisplayName}
            Description: {category.Description}

            Current products:
            {productList}

            For each product, improve:
            1. Description: Make it more engaging, highlight unique benefits, use sensory language (2-3 sentences)
            2. Image prompt: Make it more specific for AI image generation (studio product photography style)

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
            AnsiConsole.MarkupLine($"[dim]Could not parse Ollama response: {ex.Message}[/]");
        }

        return originals;
    }

    private class OllamaResponse
    {
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
