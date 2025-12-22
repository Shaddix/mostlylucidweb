using System.Text;
using System.Text.Json;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates realistic product definitions using Ollama.
/// </summary>
public class OllamaProductGenerator
{
    private readonly HttpClient _httpClient;
    private readonly GenerationConfig _config;

    public OllamaProductGenerator(HttpClient httpClient, GenerationConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(config.OllamaBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.OllamaTimeoutSeconds);
    }

    /// <summary>
    /// Generate products for a specific category.
    /// </summary>
    public async Task<List<GeneratedProduct>> GenerateProductsAsync(
        string categorySlug,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (!ProductCategories.All.TryGetValue(categorySlug, out var category))
        {
            throw new ArgumentException($"Unknown category: {categorySlug}");
        }

        var prompt = BuildProductGenerationPrompt(category, count);
        var response = await CallOllamaAsync(prompt, cancellationToken);

        return ParseProductResponse(response, categorySlug);
    }

    /// <summary>
    /// Generate all products for all categories.
    /// </summary>
    public async Task<Dictionary<string, List<GeneratedProduct>>> GenerateAllProductsAsync(
        IProgress<(string category, int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, List<GeneratedProduct>>();
        var categories = ProductCategories.All.Keys.ToList();
        var current = 0;

        foreach (var categorySlug in categories)
        {
            current++;
            progress?.Report((categorySlug, current, categories.Count));

            var products = await GenerateProductsAsync(
                categorySlug,
                _config.ProductsPerCategory,
                cancellationToken);

            result[categorySlug] = products;
        }

        return result;
    }

    private string BuildProductGenerationPrompt(CategoryDefinition category, int count)
    {
        var jsonExample = """
            {
              "products": [
                {
                  "name": "Product Name",
                  "description": "Product description here.",
                  "price": 99.99,
                  "original_price": null,
                  "tags": ["tag1", "tag2", "tag3"],
                  "is_trending": false,
                  "is_featured": false,
                  "image_prompt": "Professional product photography of [product], studio lighting, white background, high detail, commercial photography style",
                  "colour_variants": ["Black", "White", "Blue"]
                }
              ]
            }
            """;

        return $"""
            You are a product catalog generator for an e-commerce store. Generate {count} unique, realistic product listings for the "{category.DisplayName}" category.

            Category description: {category.Description}
            Example products in this category: {category.ExampleProducts}
            Price range: £{category.PriceRange.Min:F2} - £{category.PriceRange.Max:F2}

            For each product, provide:
            1. A compelling product name (realistic brand-style naming)
            2. A detailed description (2-3 sentences, highlighting key features and benefits)
            3. A realistic price within the range
            4. Optional original price if on sale (20-40% higher than current price)
            5. 3-5 relevant tags
            6. Whether it's trending (about 20% should be trending)
            7. Whether it's featured (about 15% should be featured)
            8. An image prompt for AI image generation (detailed, product photography style)
            9. 2-3 colour variants for the product (e.g., "Midnight Black", "Arctic White", "Ocean Blue")

            IMPORTANT: Respond with ONLY valid JSON, no markdown formatting, no code blocks, no explanations.
            
            Use this exact JSON structure:
            {jsonExample}

            Generate {count} diverse products now:
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
                temperature = 0.8,
                top_p = 0.9,
                num_predict = 4096
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

    private List<GeneratedProduct> ParseProductResponse(string response, string categorySlug)
    {
        try
        {
            // Try to extract JSON from the response (in case there's extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var batch = JsonSerializer.Deserialize<ProductBatchResponse>(jsonStr);

                if (batch?.Products != null)
                {
                    foreach (var product in batch.Products)
                    {
                        product.Category = categorySlug;
                    }

                    return batch.Products;
                }
            }
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Failed to parse JSON response: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[dim]Response was: {response.Substring(0, Math.Min(500, response.Length))}...[/]");
        }

        return [];
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }

    private class ProductBatchResponse
    {
        public List<GeneratedProduct> Products { get; set; } = [];
    }
}
