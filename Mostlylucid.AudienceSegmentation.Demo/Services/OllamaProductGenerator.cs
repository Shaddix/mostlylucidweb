using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.AudienceSegmentation.Demo.Models;
using OllamaSharp;

namespace Mostlylucid.AudienceSegmentation.Demo.Services;

/// <summary>
/// Generates realistic ecommerce products using Ollama LLM
/// </summary>
public class OllamaProductGenerator
{
    private readonly ILogger<OllamaProductGenerator> _logger;
    private readonly OllamaApiClient _ollama;

    public OllamaProductGenerator(ILogger<OllamaProductGenerator> logger)
    {
        _logger = logger;
        _ollama = new OllamaApiClient(
            new Uri("http://localhost:11434")
        );
        _ollama.SelectedModel = "llama3.2:3b"; // Fast small model for product generation
    }

    /// <summary>
    /// Generate a catalog of diverse products for different audience segments
    /// </summary>
    public async Task<List<Product>> GenerateProductCatalogAsync(
        int numberOfProducts = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} products using Ollama...", numberOfProducts);

        var categories = new[]
        {
            "Electronics",
            "Fashion",
            "Home & Garden",
            "Sports & Outdoors",
            "Books & Media",
            "Food & Beverage",
            "Health & Beauty",
            "Toys & Games"
        };

        var audiences = new[]
        {
            "Budget-conscious families",
            "Tech enthusiasts",
            "Luxury seekers",
            "Eco-conscious consumers",
            "Young professionals",
            "Fitness enthusiasts",
            "Creative professionals",
            "Retirees"
        };

        var products = new List<Product>();
        var productsPerCategory = numberOfProducts / categories.Length;

        foreach (var category in categories)
        {
            for (int i = 0; i < productsPerCategory; i++)
            {
                var audience = audiences[Random.Shared.Next(audiences.Length)];

                var prompt = $@"Generate a realistic product for an ecommerce store.

Category: {category}
Target Audience: {audience}

Respond ONLY with valid JSON in this exact format (no markdown, no extra text):
{{
  ""name"": ""product name"",
  ""description"": ""detailed 2-sentence description"",
  ""price"": 29.99,
  ""tags"": [""tag1"", ""tag2"", ""tag3""]
}}";

                try
                {
                    var response = await _ollama.GetCompletion(prompt, cancellationToken);

                    // Clean response - remove markdown code blocks if present
                    var jsonResponse = response.Response?.Trim() ?? "";
                    jsonResponse = jsonResponse.Replace("```json", "").Replace("```", "").Trim();

                    var productData = JsonSerializer.Deserialize<ProductJsonResponse>(jsonResponse);

                    if (productData != null)
                    {
                        products.Add(new Product
                        {
                            Name = productData.Name,
                            Description = productData.Description,
                            Category = category,
                            Price = productData.Price,
                            Tags = productData.Tags,
                            TargetAudience = audience
                        });

                        _logger.LogDebug("Generated product: {Name} for {Audience}",
                            productData.Name, audience);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate product {Index} for {Category}",
                        i, category);

                    // Fallback: generate a simple product
                    products.Add(new Product
                    {
                        Name = $"{category} Product {i + 1}",
                        Description = $"A quality {category.ToLower()} product for {audience.ToLower()}.",
                        Category = category,
                        Price = Random.Shared.Next(10, 500),
                        Tags = new List<string> { category, audience.Split(' ')[0] },
                        TargetAudience = audience
                    });
                }

                // Delay to avoid overwhelming Ollama
                await Task.Delay(500, cancellationToken);
            }
        }

        _logger.LogInformation("Generated {Count} products successfully", products.Count);
        return products;
    }

    private class ProductJsonResponse
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
