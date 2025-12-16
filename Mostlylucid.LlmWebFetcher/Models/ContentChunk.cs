namespace Mostlylucid.LlmWebFetcher.Models;

/// <summary>
/// Represents a chunk of content extracted from a web page.
/// </summary>
public class ContentChunk
{
    /// <summary>
    /// Section heading (if available).
    /// </summary>
    public string? Heading { get; set; }
    
    /// <summary>
    /// The text content of this chunk.
    /// </summary>
    public string Content { get; set; } = "";
    
    /// <summary>
    /// Estimated token count for this chunk.
    /// </summary>
    public int EstimatedTokens { get; set; }
    
    /// <summary>
    /// Relevance score when filtered by keywords or embeddings.
    /// </summary>
    public double RelevanceScore { get; set; }
}
