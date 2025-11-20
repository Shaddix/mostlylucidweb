namespace Mostlylucid.SemanticSearch.Models;

/// <summary>
/// Represents a semantic search result
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Blog post slug
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Blog post title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Language code
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Categories
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Similarity score (0-1, higher is more similar)
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Published date
    /// </summary>
    public DateTime PublishedDate { get; set; }

    /// <summary>
    /// Content preview/summary
    /// </summary>
    public string? Summary { get; set; }
}
