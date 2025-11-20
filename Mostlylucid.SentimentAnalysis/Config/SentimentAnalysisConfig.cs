namespace Mostlylucid.SentimentAnalysis.Config;

/// <summary>
/// Configuration for sentiment analysis service
/// </summary>
public class SentimentAnalysisConfig
{
    /// <summary>
    /// Enable or disable sentiment analysis
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the sentiment analysis ONNX model
    /// </summary>
    public string ModelPath { get; set; } = "models/sentiment-analysis.onnx";

    /// <summary>
    /// Path to vocabulary file for tokenization
    /// </summary>
    public string VocabPath { get; set; } = "models/vocab.txt";

    /// <summary>
    /// Maximum text length to analyze (characters)
    /// </summary>
    public int MaxTextLength { get; set; } = 10000;

    /// <summary>
    /// Minimum confidence threshold for emotional tone detection
    /// </summary>
    public float MinimumEmotionConfidence { get; set; } = 0.3f;

    /// <summary>
    /// Enable automatic re-analysis when content changes
    /// </summary>
    public bool AutoReanalyze { get; set; } = true;

    /// <summary>
    /// Cache sentiment results to avoid re-analysis
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache expiration in hours
    /// </summary>
    public int CacheExpirationHours { get; set; } = 24;
}
