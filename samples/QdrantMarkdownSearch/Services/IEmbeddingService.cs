namespace QdrantMarkdownSearch.Services;

/// <summary>
/// Service for generating text embeddings
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
}
