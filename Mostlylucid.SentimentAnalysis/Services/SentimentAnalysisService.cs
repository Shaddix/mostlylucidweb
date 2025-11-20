using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.SentimentAnalysis.Config;
using Mostlylucid.SentimentAnalysis.Models;
using System.Text.RegularExpressions;

namespace Mostlylucid.SentimentAnalysis.Services;

/// <summary>
/// Rule-based and lexicon-based sentiment analysis service
/// Uses linguistic patterns and word lists for CPU-efficient analysis
/// </summary>
public class SentimentAnalysisService : ISentimentAnalysisService
{
    private readonly ILogger<SentimentAnalysisService> _logger;
    private readonly SentimentAnalysisConfig _config;

    // Sentiment lexicons
    private readonly HashSet<string> _positiveWords;
    private readonly HashSet<string> _negativeWords;
    private readonly Dictionary<string, EmotionalTone> _emotionalKeywords;

    // Formality indicators
    private readonly HashSet<string> _formalWords;
    private readonly HashSet<string> _casualWords;

    // Subjectivity indicators
    private readonly HashSet<string> _subjectiveWords;
    private readonly HashSet<string> _objectiveWords;

    public SentimentAnalysisService(
        ILogger<SentimentAnalysisService> logger,
        IOptions<SentimentAnalysisConfig> config)
    {
        _logger = logger;
        _config = config.Value;

        // Initialize lexicons
        _positiveWords = LoadPositiveWords();
        _negativeWords = LoadNegativeWords();
        _emotionalKeywords = LoadEmotionalKeywords();
        _formalWords = LoadFormalWords();
        _casualWords = LoadCasualWords();
        _subjectiveWords = LoadSubjectiveWords();
        _objectiveWords = LoadObjectiveWords();
    }

    public async Task<SentimentResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        // Truncate if needed
        if (text.Length > _config.MaxTextLength)
        {
            text = text.Substring(0, _config.MaxTextLength);
        }

        return await Task.Run(() => AnalyzeText(text), cancellationToken);
    }

    public async Task<List<SentimentResult>> AnalyzeBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SentimentResult>();

        foreach (var text in texts)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                results.Add(await AnalyzeAsync(text, cancellationToken));
            }
        }

        return results;
    }

    public float CalculateSentimentSimilarity(SentimentResult sentiment1, SentimentResult sentiment2)
    {
        // Calculate weighted similarity across multiple dimensions
        float sentimentSim = 1.0f - Math.Abs(sentiment1.SentimentScore - sentiment2.SentimentScore) / 2.0f;
        float formalitySim = 1.0f - Math.Abs(sentiment1.FormalityScore - sentiment2.FormalityScore);
        float subjectivitySim = 1.0f - Math.Abs(sentiment1.SubjectivityScore - sentiment2.SubjectivityScore);

        // Calculate emotional tone similarity
        float emotionSim = sentiment1.DominantEmotion == sentiment2.DominantEmotion ? 1.0f : 0.5f;

        // Weighted average
        return (sentimentSim * 0.4f +
                formalitySim * 0.2f +
                subjectivitySim * 0.2f +
                emotionSim * 0.2f);
    }

    public SentimentMetadata ToMetadata(SentimentResult result)
    {
        return SentimentMetadata.FromResult(result);
    }

    private SentimentResult AnalyzeText(string text)
    {
        // Tokenize and clean
        var words = TokenizeText(text);
        var sentences = SplitIntoSentences(text);

        // Calculate sentiment score
        var (sentimentScore, confidence) = CalculateSentimentScore(words);

        // Detect emotional tones
        var emotionalTones = DetectEmotionalTones(text, words);

        // Calculate other metrics
        var formalityScore = CalculateFormalityScore(words, text);
        var subjectivityScore = CalculateSubjectivityScore(words);
        var readabilityScore = CalculateReadabilityScore(words, sentences);

        return new SentimentResult
        {
            SentimentScore = sentimentScore,
            SentimentClass = ClassifySentiment(sentimentScore),
            Confidence = confidence,
            DominantEmotion = GetDominantEmotion(emotionalTones),
            EmotionalTones = emotionalTones,
            FormalityScore = formalityScore,
            SubjectivityScore = subjectivityScore,
            ReadabilityScore = readabilityScore,
            WordCount = words.Count
        };
    }

    private List<string> TokenizeText(string text)
    {
        // Simple word tokenization
        var words = Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length > 1)
            .ToList();

        return words;
    }

    private List<string> SplitIntoSentences(string text)
    {
        return Regex.Split(text, @"[.!?]+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private (float score, float confidence) CalculateSentimentScore(List<string> words)
    {
        if (words.Count == 0)
            return (0, 0);

        int positiveCount = words.Count(w => _positiveWords.Contains(w));
        int negativeCount = words.Count(w => _negativeWords.Contains(w));
        int sentimentWords = positiveCount + negativeCount;

        if (sentimentWords == 0)
            return (0, 0.3f); // Neutral with low confidence

        // Calculate score (-1 to 1)
        float score = (float)(positiveCount - negativeCount) / words.Count * 2;
        score = Math.Clamp(score, -1.0f, 1.0f);

        // Calculate confidence based on proportion of sentiment words
        float confidence = Math.Min(1.0f, (float)sentimentWords / words.Count * 5);

        return (score, confidence);
    }

    private SentimentClass ClassifySentiment(float score)
    {
        if (score > 0.1f) return SentimentClass.Positive;
        if (score < -0.1f) return SentimentClass.Negative;
        return SentimentClass.Neutral;
    }

    private Dictionary<EmotionalTone, float> DetectEmotionalTones(string text, List<string> words)
    {
        var tones = new Dictionary<EmotionalTone, float>();

        foreach (EmotionalTone tone in Enum.GetValues<EmotionalTone>())
        {
            tones[tone] = 0.0f;
        }

        // Count emotional keywords
        foreach (var word in words)
        {
            if (_emotionalKeywords.TryGetValue(word, out var tone))
            {
                tones[tone] += 1.0f;
            }
        }

        // Normalize scores
        float maxScore = tones.Values.Max();
        if (maxScore > 0)
        {
            var normalizedTones = new Dictionary<EmotionalTone, float>();
            foreach (var kvp in tones)
            {
                normalizedTones[kvp.Key] = kvp.Value / words.Count;
            }
            return normalizedTones;
        }

        return tones;
    }

    private EmotionalTone GetDominantEmotion(Dictionary<EmotionalTone, float> tones)
    {
        var maxTone = tones.OrderByDescending(kvp => kvp.Value).First();
        return maxTone.Value > _config.MinimumEmotionConfidence
            ? maxTone.Key
            : EmotionalTone.Neutral;
    }

    private float CalculateFormalityScore(List<string> words, string originalText)
    {
        if (words.Count == 0) return 0.5f;

        int formalCount = words.Count(w => _formalWords.Contains(w));
        int casualCount = words.Count(w => _casualWords.Contains(w));

        // Check for contractions (informal)
        int contractionCount = Regex.Matches(originalText, @"\b\w+'\w+\b").Count;
        casualCount += contractionCount;

        // Check for passive voice (formal)
        int passiveCount = Regex.Matches(originalText, @"\b(is|are|was|were|been|being)\s+\w+ed\b").Count;
        formalCount += passiveCount;

        float totalIndicators = formalCount + casualCount;
        if (totalIndicators == 0) return 0.5f;

        return formalCount / totalIndicators;
    }

    private float CalculateSubjectivityScore(List<string> words)
    {
        if (words.Count == 0) return 0.5f;

        int subjectiveCount = words.Count(w => _subjectiveWords.Contains(w));
        int objectiveCount = words.Count(w => _objectiveWords.Contains(w));

        float totalIndicators = subjectiveCount + objectiveCount;
        if (totalIndicators == 0) return 0.5f;

        return subjectiveCount / totalIndicators;
    }

    private float CalculateReadabilityScore(List<string> words, List<string> sentences)
    {
        if (sentences.Count == 0 || words.Count == 0) return 0.5f;

        // Simple readability metric based on average sentence length and word length
        float avgSentenceLength = (float)words.Count / sentences.Count;
        float avgWordLength = (float)words.Average(w => w.Length);

        // Normalize to 0-1 scale (lower values = easier to read)
        float sentenceComplexity = Math.Clamp(1.0f - (avgSentenceLength / 30.0f), 0, 1);
        float wordComplexity = Math.Clamp(1.0f - (avgWordLength / 10.0f), 0, 1);

        return (sentenceComplexity + wordComplexity) / 2.0f;
    }

    #region Lexicon Initialization

    private HashSet<string> LoadPositiveWords()
    {
        return new HashSet<string>
        {
            "good", "great", "excellent", "amazing", "wonderful", "fantastic", "awesome",
            "love", "best", "perfect", "beautiful", "brilliant", "outstanding", "superb",
            "happy", "joy", "delighted", "pleased", "excited", "enthusiastic", "positive",
            "success", "successful", "effective", "efficient", "helpful", "useful",
            "easy", "simple", "clear", "powerful", "strong", "robust", "reliable",
            "innovative", "creative", "elegant", "impressive", "valuable", "beneficial"
        };
    }

    private HashSet<string> LoadNegativeWords()
    {
        return new HashSet<string>
        {
            "bad", "terrible", "awful", "poor", "worst", "horrible", "dreadful",
            "hate", "disappointing", "disappointed", "frustrating", "frustrated",
            "difficult", "hard", "complex", "complicated", "confusing", "unclear",
            "problem", "issue", "bug", "error", "fail", "failed", "failure",
            "slow", "inefficient", "ineffective", "useless", "weak", "broken",
            "annoying", "irritating", "painful", "difficult", "challenging"
        };
    }

    private Dictionary<string, EmotionalTone> LoadEmotionalKeywords()
    {
        return new Dictionary<string, EmotionalTone>
        {
            // Analytical
            {"data", EmotionalTone.Analytical}, {"analysis", EmotionalTone.Analytical},
            {"research", EmotionalTone.Analytical}, {"study", EmotionalTone.Analytical},
            {"evidence", EmotionalTone.Analytical}, {"statistics", EmotionalTone.Analytical},

            // Confident
            {"definitely", EmotionalTone.Confident}, {"certainly", EmotionalTone.Confident},
            {"clearly", EmotionalTone.Confident}, {"obviously", EmotionalTone.Confident},
            {"undoubtedly", EmotionalTone.Confident}, {"confident", EmotionalTone.Confident},

            // Tentative
            {"maybe", EmotionalTone.Tentative}, {"perhaps", EmotionalTone.Tentative},
            {"possibly", EmotionalTone.Tentative}, {"might", EmotionalTone.Tentative},
            {"could", EmotionalTone.Tentative}, {"uncertain", EmotionalTone.Tentative},

            // Joyful
            {"happy", EmotionalTone.Joyful}, {"joy", EmotionalTone.Joyful},
            {"excited", EmotionalTone.Joyful}, {"delighted", EmotionalTone.Joyful},
            {"wonderful", EmotionalTone.Joyful}, {"amazing", EmotionalTone.Joyful},

            // Sad
            {"sad", EmotionalTone.Sad}, {"disappointed", EmotionalTone.Sad},
            {"unfortunate", EmotionalTone.Sad}, {"regret", EmotionalTone.Sad},
            {"sorry", EmotionalTone.Sad}, {"unhappy", EmotionalTone.Sad},

            // Angry
            {"angry", EmotionalTone.Angry}, {"frustrated", EmotionalTone.Angry},
            {"annoying", EmotionalTone.Angry}, {"terrible", EmotionalTone.Angry},
            {"awful", EmotionalTone.Angry}, {"hate", EmotionalTone.Angry},

            // Fear
            {"worry", EmotionalTone.Fear}, {"anxious", EmotionalTone.Fear},
            {"concerned", EmotionalTone.Fear}, {"afraid", EmotionalTone.Fear},
            {"scared", EmotionalTone.Fear}, {"fear", EmotionalTone.Fear}
        };
    }

    private HashSet<string> LoadFormalWords()
    {
        return new HashSet<string>
        {
            "therefore", "furthermore", "moreover", "consequently", "subsequently",
            "however", "nevertheless", "nonetheless", "additionally", "accordingly",
            "thus", "hence", "whereas", "whereby", "herein", "thereof",
            "utilize", "endeavor", "ascertain", "facilitate", "implement"
        };
    }

    private HashSet<string> LoadCasualWords()
    {
        return new HashSet<string>
        {
            "yeah", "yep", "nope", "gonna", "wanna", "gotta", "kinda", "sorta",
            "cool", "awesome", "stuff", "things", "guys", "folks",
            "pretty", "really", "very", "super", "totally", "basically"
        };
    }

    private HashSet<string> LoadSubjectiveWords()
    {
        return new HashSet<string>
        {
            "believe", "think", "feel", "seems", "appears", "probably",
            "opinion", "perspective", "view", "prefer", "like", "dislike",
            "beautiful", "ugly", "good", "bad", "best", "worst",
            "amazing", "terrible", "wonderful", "awful"
        };
    }

    private HashSet<string> LoadObjectiveWords()
    {
        return new HashSet<string>
        {
            "is", "are", "was", "were", "has", "have", "does", "do",
            "data", "fact", "evidence", "result", "study", "research",
            "measurement", "observation", "experiment", "analysis",
            "number", "amount", "quantity", "percent", "ratio"
        };
    }

    #endregion
}
