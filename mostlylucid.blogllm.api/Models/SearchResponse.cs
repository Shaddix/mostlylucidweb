namespace Mostlylucid.BlogLLM.Api.Models;

/// <summary>
/// Response from semantic search
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// The search results
    /// </summary>
    public required List<ContextChunk> Results { get; set; }

    /// <summary>
    /// Total number of results found
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Time taken to search (milliseconds)
    /// </summary>
    public long SearchTimeMs { get; set; }
}
