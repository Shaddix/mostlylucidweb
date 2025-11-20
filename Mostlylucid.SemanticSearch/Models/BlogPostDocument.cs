namespace Mostlylucid.SemanticSearch.Models;

/// <summary>
/// Represents a blog post document for indexing in the vector database
/// </summary>
public class BlogPostDocument
{
    /// <summary>
    /// Unique identifier (typically slug + language)
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Blog post slug
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Blog post title
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Plain text content (for embedding)
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Language code (e.g., "en", "es")
    /// </summary>
    public required string Language { get; set; }

    /// <summary>
    /// Categories
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Published date
    /// </summary>
    public DateTime PublishedDate { get; set; }

    /// <summary>
    /// Content hash (to detect changes)
    /// </summary>
    public string? ContentHash { get; set; }

    /// <summary>
    /// Sentiment score (-1.0 to 1.0)
    /// </summary>
    public float? SentimentScore { get; set; }

    /// <summary>
    /// Dominant emotional tone
    /// </summary>
    public string? DominantEmotion { get; set; }

    /// <summary>
    /// Formality level (0.0 to 1.0)
    /// </summary>
    public float? Formality { get; set; }

    /// <summary>
    /// Subjectivity score (0.0 to 1.0)
    /// </summary>
    public float? Subjectivity { get; set; }
}
