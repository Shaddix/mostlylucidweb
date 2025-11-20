using System.Text.Json.Serialization;

namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Sentiment metadata stored in database for semantic search and filtering
/// </summary>
public class SentimentMetadata
{
    /// <summary>
    /// Overall sentiment score (-1.0 to 1.0)
    /// </summary>
    [JsonPropertyName("sentiment_score")]
    public float SentimentScore { get; set; }

    /// <summary>
    /// Sentiment classification
    /// </summary>
    [JsonPropertyName("sentiment_class")]
    public string SentimentClass { get; set; } = string.Empty;

    /// <summary>
    /// Dominant emotional tone
    /// </summary>
    [JsonPropertyName("dominant_emotion")]
    public string DominantEmotion { get; set; } = string.Empty;

    /// <summary>
    /// Emotional tone scores
    /// </summary>
    [JsonPropertyName("emotional_tones")]
    public Dictionary<string, float> EmotionalTones { get; set; } = new();

    /// <summary>
    /// Formality level (0.0 = casual, 1.0 = very formal)
    /// </summary>
    [JsonPropertyName("formality")]
    public float Formality { get; set; }

    /// <summary>
    /// Subjectivity score (0.0 = objective, 1.0 = subjective)
    /// </summary>
    [JsonPropertyName("subjectivity")]
    public float Subjectivity { get; set; }

    /// <summary>
    /// Readability score (0.0 to 1.0)
    /// </summary>
    [JsonPropertyName("readability")]
    public float Readability { get; set; }

    /// <summary>
    /// When the sentiment was analyzed
    /// </summary>
    [JsonPropertyName("analyzed_at")]
    public DateTime AnalyzedAt { get; set; }

    /// <summary>
    /// Version of the sentiment analyzer used
    /// </summary>
    [JsonPropertyName("analyzer_version")]
    public string AnalyzerVersion { get; set; } = "1.0";

    /// <summary>
    /// Create metadata from sentiment result
    /// </summary>
    public static SentimentMetadata FromResult(SentimentResult result)
    {
        return new SentimentMetadata
        {
            SentimentScore = result.SentimentScore,
            SentimentClass = result.SentimentClass.ToString(),
            DominantEmotion = result.DominantEmotion.ToString(),
            EmotionalTones = result.EmotionalTones.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value
            ),
            Formality = result.FormalityScore,
            Subjectivity = result.SubjectivityScore,
            Readability = result.ReadabilityScore,
            AnalyzedAt = DateTime.UtcNow
        };
    }
}
