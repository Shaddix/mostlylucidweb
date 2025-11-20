using Mostlylucid.SemanticSearch.Models;

namespace Mostlylucid.SemanticSearch.Services;

/// <summary>
/// Hybrid search service combining PostgreSQL full-text search with semantic vector search
/// </summary>
public interface IHybridSearchService
{
    /// <summary>
    /// Search using both full-text and semantic search, combining results
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="language">Language filter</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined search results ranked by relevance</returns>
    Task<List<SearchResult>> SearchAsync(
        string query, 
        string language = "en", 
        int limit = 10, 
        CancellationToken cancellationToken = default);
}
