namespace Mostlylucid.BlogLLM.Api.Models;

/// <summary>
/// Response from RAG-based question answering
/// </summary>
public class RagResponse
{
    /// <summary>
    /// The generated answer
    /// </summary>
    public required string Answer { get; set; }

    /// <summary>
    /// Context chunks used to generate the answer
    /// </summary>
    public required List<ContextChunk> Context { get; set; }

    /// <summary>
    /// Time taken to process the request (milliseconds)
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of tokens generated
    /// </summary>
    public int TokensGenerated { get; set; }
}

/// <summary>
/// A chunk of context retrieved from the knowledge base
/// </summary>
public class ContextChunk
{
    /// <summary>
    /// The text content of the chunk
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// The document title this chunk belongs to
    /// </summary>
    public required string DocumentTitle { get; set; }

    /// <summary>
    /// The section heading within the document
    /// </summary>
    public string? SectionHeading { get; set; }

    /// <summary>
    /// Similarity score (0.0-1.0)
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Categories/tags for this content
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Language code (e.g., "en", "es")
    /// </summary>
    public string? Language { get; set; }
}
