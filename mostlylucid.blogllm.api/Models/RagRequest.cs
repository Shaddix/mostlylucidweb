namespace Mostlylucid.BlogLLM.Api.Models;

/// <summary>
/// Request for RAG-based question answering
/// </summary>
public class RagRequest
{
    /// <summary>
    /// The user's question
    /// </summary>
    public required string Question { get; set; }

    /// <summary>
    /// Maximum number of context chunks to retrieve (default: 5)
    /// </summary>
    public int MaxContextChunks { get; set; } = 5;

    /// <summary>
    /// Similarity score threshold (0.0-1.0, default: 0.7)
    /// </summary>
    public float ScoreThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Maximum tokens to generate in response (default: 512)
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Temperature for LLM generation (0.0-1.0, default: 0.7)
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Optional language filter (e.g., "en", "es")
    /// </summary>
    public string? Language { get; set; }
}
