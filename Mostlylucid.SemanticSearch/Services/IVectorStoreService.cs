using Mostlylucid.SemanticSearch.Models;

namespace Mostlylucid.SemanticSearch.Services;

/// <summary>
/// Service for interacting with the vector database (Qdrant)
/// </summary>
public interface IVectorStoreService
{
    /// <summary>
    /// Initialize the collection in Qdrant (create if it doesn't exist)
    /// </summary>
    Task InitializeCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Index a blog post document
    /// </summary>
    Task IndexDocumentAsync(BlogPostDocument document, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index multiple blog post documents
    /// </summary>
    Task IndexDocumentsAsync(IEnumerable<(BlogPostDocument Document, float[] Embedding)> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar documents using a query embedding
    /// </summary>
    Task<List<SearchResult>> SearchAsync(float[] queryEmbedding, int limit = 10, float scoreThreshold = 0.5f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find related posts for a given blog post
    /// </summary>
    Task<List<SearchResult>> FindRelatedPostsAsync(string slug, int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a document from the index
    /// </summary>
    Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a document exists and get its content hash
    /// </summary>
    Task<string?> GetDocumentHashAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the languages array for an existing document (full replacement)
    /// </summary>
    Task UpdateLanguagesAsync(string slug, string[] languages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a single language to the existing languages array (called immediately when translation is created)
    /// </summary>
    Task AddLanguageAsync(string slug, string language, CancellationToken cancellationToken = default);
}
