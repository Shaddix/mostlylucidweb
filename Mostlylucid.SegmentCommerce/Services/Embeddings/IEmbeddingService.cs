using Pgvector;

namespace Mostlylucid.SegmentCommerce.Services.Embeddings;

/// <summary>
/// Service for generating and querying vector embeddings.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate an embedding vector for the given text.
    /// </summary>
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts.
    /// </summary>
    Task<Vector[]> GenerateEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find similar products by embedding similarity.
    /// </summary>
    Task<IEnumerable<ProductSimilarityResult>> FindSimilarProductsAsync(
        Vector queryEmbedding,
        int limit = 10,
        float minSimilarity = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find products similar to a text query.
    /// </summary>
    Task<IEnumerable<ProductSimilarityResult>> SearchProductsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate and store embedding for a product.
    /// </summary>
    Task<bool> IndexProductAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-index all products.
    /// </summary>
    Task<int> ReindexAllProductsAsync(CancellationToken cancellationToken = default);
}

public record ProductSimilarityResult
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public float Similarity { get; init; }
}
