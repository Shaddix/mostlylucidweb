using Mostlylucid.SentimentAnalysis.Models;

namespace Mostlylucid.SentimentAnalysis.Services;

/// <summary>
/// Service for analyzing sentiment and emotional tone of text
/// </summary>
public interface ISentimentAnalysisService
{
    /// <summary>
    /// Analyze the sentiment of a text
    /// </summary>
    /// <param name="text">Text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sentiment analysis result</returns>
    Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze multiple texts in batch
    /// </summary>
    /// <param name="texts">Texts to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sentiment analysis results</returns>
    Task<List<SentimentResult>> AnalyzeBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate similarity between two sentiment profiles
    /// </summary>
    /// <param name="sentiment1">First sentiment result</param>
    /// <param name="sentiment2">Second sentiment result</param>
    /// <returns>Similarity score (0.0 to 1.0)</returns>
    float CalculateSentimentSimilarity(SentimentResult sentiment1, SentimentResult sentiment2);

    /// <summary>
    /// Convert sentiment result to metadata for storage
    /// </summary>
    /// <param name="result">Sentiment analysis result</param>
    /// <returns>JSON-serializable metadata</returns>
    SentimentMetadata ToMetadata(SentimentResult result);
}
