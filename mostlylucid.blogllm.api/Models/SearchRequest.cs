namespace Mostlylucid.BlogLLM.Api.Models;

/// <summary>
/// Request for semantic search
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// The search query
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Maximum number of results to return (default: 10)
    /// </summary>
    public int Limit { get; set; } = 10;

    /// <summary>
    /// Similarity score threshold (0.0-1.0, default: 0.7)
    /// </summary>
    public float ScoreThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Optional language filter (e.g., "en", "es")
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional category filters
    /// </summary>
    public List<string>? Categories { get; set; }
}
