using QdrantMarkdownSearch.Models;

namespace QdrantMarkdownSearch.Services;

/// <summary>
/// Service for interacting with Qdrant vector database
/// </summary>
public interface IVectorSearchService
{
    Task InitializeCollectionAsync(CancellationToken ct = default);
    Task<bool> IndexDocumentAsync(string id, string fileName, string title, string content, CancellationToken ct = default);
    Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
}
