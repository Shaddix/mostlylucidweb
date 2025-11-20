namespace Mostlylucid.SentimentAnalysis.Models;

/// <summary>
/// Represents the sentiment analysis result for a text
/// </summary>
public class SentimentResult
{
    /// <summary>
    /// Overall sentiment score (-1.0 to 1.0)
    /// Negative values indicate negative sentiment, positive values indicate positive sentiment
    /// </summary>
    public float SentimentScore { get; set; }

    /// <summary>
    /// Sentiment classification (Positive, Negative, Neutral)
    /// </summary>
    public SentimentClass SentimentClass { get; set; }

    /// <summary>
    /// Confidence in the sentiment classification (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Dominant emotional tone detected
    /// </summary>
    public EmotionalTone DominantEmotion { get; set; }

    /// <summary>
    /// All detected emotional tones with scores
    /// </summary>
    public Dictionary<EmotionalTone, float> EmotionalTones { get; set; } = new();

    /// <summary>
    /// Formality level (0.0 = casual, 1.0 = very formal)
    /// </summary>
    public float FormalityScore { get; set; }

    /// <summary>
    /// Subjectivity score (0.0 = objective, 1.0 = subjective)
    /// </summary>
    public float SubjectivityScore { get; set; }

    /// <summary>
    /// Readability score (0.0 to 1.0, higher = easier to read)
    /// </summary>
    public float ReadabilityScore { get; set; }

    /// <summary>
    /// Word count of analyzed text
    /// </summary>
    public int WordCount { get; set; }
}

/// <summary>
/// Sentiment classification
/// </summary>
public enum SentimentClass
{
    Negative,
    Neutral,
    Positive
}

/// <summary>
/// Emotional tone categories
/// </summary>
public enum EmotionalTone
{
    Analytical,     // Data-driven, logical
    Confident,      // Assertive, certain
    Tentative,      // Uncertain, questioning
    Joyful,         // Happy, enthusiastic
    Sad,            // Melancholic, disappointed
    Angry,          // Frustrated, critical
    Fear,           // Worried, anxious
    Neutral         // No strong emotion
}
