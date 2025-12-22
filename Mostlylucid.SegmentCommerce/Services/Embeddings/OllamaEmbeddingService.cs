using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Mostlylucid.SegmentCommerce.Services.Embeddings;

/// <summary>
/// Embedding service using Ollama for local embedding generation.
/// Uses nomic-embed-text or all-minilm models.
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly string _model;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        SegmentCommerceDbContext context,
        ILogger<OllamaEmbeddingService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
        _model = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync(new[] { text }, cancellationToken);
        return embeddings[0];
    }

    public async Task<Vector[]> GenerateEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default)
    {
        var results = new List<Vector>();

        foreach (var text in texts)
        {
            var request = new
            {
                model = _model,
                prompt = text
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embeddings", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseObj = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson);

            if (responseObj?.Embedding == null || responseObj.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Ollama returned empty embedding");
            }

            results.Add(new Vector(responseObj.Embedding));
        }

        return results.ToArray();
    }

    public async Task<IEnumerable<ProductSimilarityResult>> FindSimilarProductsAsync(
        Vector queryEmbedding,
        int limit = 10,
        float minSimilarity = 0.5f,
        CancellationToken cancellationToken = default)
    {
        // Use pgvector's cosine distance operator
        // Lower distance = more similar, so we use 1 - distance for similarity
        var results = await _context.ProductEmbeddings
            .Include(e => e.Product)
            .Select(e => new
            {
                e.ProductId,
                e.Product.Name,
                e.Product.Category,
                e.Product.Price,
                Distance = e.Embedding.CosineDistance(queryEmbedding)
            })
            .Where(x => (1 - x.Distance) >= minSimilarity)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return results.Select(r => new ProductSimilarityResult
        {
            ProductId = r.ProductId,
            ProductName = r.Name,
            Category = r.Category,
            Price = r.Price,
            Similarity = 1 - (float)r.Distance
        });
    }

    public async Task<IEnumerable<ProductSimilarityResult>> SearchProductsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        return await FindSimilarProductsAsync(queryEmbedding, limit, minSimilarity: 0.3f, cancellationToken);
    }

    public async Task<bool> IndexProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product == null)
        {
            _logger.LogWarning("Product {ProductId} not found for indexing", productId);
            return false;
        }

        // Combine product text for embedding
        var text = $"{product.Name}. {product.Description}. Category: {product.Category}. Tags: {string.Join(", ", product.Tags)}";

        try
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);

            // Upsert the embedding
            var existing = await _context.ProductEmbeddings
                .FirstOrDefaultAsync(e => e.ProductId == productId, cancellationToken);

            if (existing != null)
            {
                existing.Embedding = embedding;
                existing.SourceText = text;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ProductEmbeddings.Add(new ProductEmbeddingEntity
                {
                    ProductId = productId,
                    Embedding = embedding,
                    SourceText = text,
                    Model = _model
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Indexed product {ProductId}", productId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index product {ProductId}", productId);
            return false;
        }
    }

    public async Task<int> ReindexAllProductsAsync(CancellationToken cancellationToken = default)
    {
        var productIds = await _context.Products
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var indexed = 0;

        foreach (var productId in productIds)
        {
            if (await IndexProductAsync(productId, cancellationToken))
            {
                indexed++;
            }

            // Small delay to avoid overwhelming Ollama
            await Task.Delay(50, cancellationToken);
        }

        _logger.LogInformation("Reindexed {Indexed}/{Total} products", indexed, productIds.Count);
        return indexed;
    }

    private class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }
}
