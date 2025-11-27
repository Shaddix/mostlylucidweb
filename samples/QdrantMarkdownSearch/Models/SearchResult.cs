namespace QdrantMarkdownSearch.Models;

/// <summary>
/// Represents a single search result with its metadata
/// </summary>
public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public float Score { get; set; }
}

/// <summary>
/// Response wrapper for search API
/// </summary>
public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResult> Results { get; set; } = new();
    public int Count { get; set; }
    public long SearchTimeMs { get; set; }
}
