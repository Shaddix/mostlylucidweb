using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Configuration for the cross-encoder reranker.
/// All values have sensible defaults - only override if you need to tune behavior.
/// </summary>
public class RerankerConfig
{
    /// <summary>Weight for term overlap ratio (0-1 scaled to this max). Default: 3.0</summary>
    public double TermOverlapWeight { get; set; } = 3.0;

    /// <summary>Bonus per early match (term in first N chars). Default: 0.1</summary>
    public double EarlyMatchBonus { get; set; } = 0.1;

    /// <summary>Max score contribution from query term density. Default: 2.0</summary>
    public double MaxDensityScore { get; set; } = 2.0;

    /// <summary>Bonus for exact phrase match. Default: 5.0</summary>
    public double ExactPhraseBonus { get; set; } = 5.0;

    /// <summary>Bonus for heading segments matching query. Default: 2.0</summary>
    public double HeadingMatchBonus { get; set; } = 2.0;

    /// <summary>Weight per section title term match. Default: 0.5</summary>
    public double SectionTermWeight { get; set; } = 0.5;

    /// <summary>Multiplier for existing embedding similarity. Default: 2.0</summary>
    public double EmbeddingSimilarityWeight { get; set; } = 2.0;

    /// <summary>Weight for position boost (intro/conclusion). Default: 0.5</summary>
    public double PositionWeight { get; set; } = 0.5;

    /// <summary>Max length bonus contribution. Default: 0.3</summary>
    public double MaxLengthBonus { get; set; } = 0.3;

    /// <summary>Characters for "optimal" length bonus. Default: 200</summary>
    public double OptimalLength { get; set; } = 200.0;

    /// <summary>Minimum query length for exact phrase matching. Default: 5</summary>
    public int MinPhraseLength { get; set; } = 5;

    /// <summary>Character position threshold for early match bonus. Default: 50</summary>
    public int EarlyMatchThreshold { get; set; } = 50;
}

/// <summary>
/// Cross-encoder style reranker for precision boost on retrieved segments.
///
/// <para>
/// Unlike bi-encoders (which embed query and document separately), cross-encoders
/// jointly encode query + document pairs, enabling deeper interaction modeling.
/// </para>
///
/// <para>
/// This implementation uses a lightweight proxy approach combining multiple signals:
/// </para>
/// <list type="number">
///   <item>Exact term overlap (like a mini BM25)</item>
///   <item>Semantic similarity boost from existing embeddings</item>
///   <item>Structural signals (heading proximity, section relevance)</item>
///   <item>Query term density</item>
/// </list>
///
/// <para>
/// For production with higher accuracy, you could swap this with a real cross-encoder
/// model (e.g., ms-marco-MiniLM-L-6-v2) via ONNX.
/// </para>
/// </summary>
/// <remarks>
/// Scoring weights are configurable via <see cref="RerankerConfig"/>.
/// Defaults are tuned empirically for general documents. Adjust for your domain:
/// - Higher TermOverlapWeight = more lexical matching (BM25-like)
/// - Higher EmbeddingSimilarityWeight = more semantic matching
/// - Higher ExactPhraseBonus = stricter exact matching
/// </remarks>
public class CrossEncoderReranker
{
    private readonly RerankerConfig _config;
    private readonly bool _verbose;

    /// <summary>
    /// Create a new cross-encoder reranker with default configuration.
    /// </summary>
    /// <param name="verbose">If true, logs scoring statistics to console</param>
    public CrossEncoderReranker(bool verbose = false)
        : this(new RerankerConfig(), verbose)
    {
    }

    /// <summary>
    /// Create a new cross-encoder reranker with custom configuration.
    /// </summary>
    /// <param name="config">Configuration with custom weights</param>
    /// <param name="verbose">If true, logs scoring statistics to console</param>
    public CrossEncoderReranker(RerankerConfig config, bool verbose = false)
    {
        _config = config ?? new RerankerConfig();
        _verbose = verbose;
    }

    /// <summary>
    /// Rerank segments for a given query, using multiple signals for precision.
    /// </summary>
    public List<Segment> Rerank(
        List<Segment> candidates,
        string query,
        int topK,
        float? queryEmbedding = null)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(query))
            return candidates;

        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
            return candidates;

        // Score each candidate with cross-encoder proxy
        var scored = candidates.Select(segment =>
        {
            var score = ComputeCrossEncoderScore(segment, queryTerms, query);
            return (Segment: segment, Score: score);
        }).ToList();

        // Sort by cross-encoder score
        var reranked = scored
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Segment)
            .ToList();

        if (_verbose && reranked.Count > 0)
        {
            var topScore = scored.Max(x => x.Score);
            var avgScore = scored.Average(x => x.Score);
            Console.WriteLine($"[Reranker] Top score: {topScore:F3}, Avg: {avgScore:F3}");
        }

        return reranked;
    }

    /// <summary>
    /// Compute cross-encoder proxy score combining multiple signals.
    /// </summary>
    /// <param name="segment">The segment to score</param>
    /// <param name="queryTerms">Tokenized query terms (lowercased, stop words removed)</param>
    /// <param name="fullQuery">Original query string for exact phrase matching</param>
    /// <returns>Composite relevance score (higher = more relevant)</returns>
    private double ComputeCrossEncoderScore(Segment segment, List<string> queryTerms, string fullQuery)
    {
        var text = segment.Text;
        var textLower = text.ToLowerInvariant();

        double score = 0;

        // === Signal 1: Exact term overlap (like BM25 term frequency) ===
        // Terms appearing early in text get a bonus (often more important)
        var overlapCount = 0;
        var earlyBonus = 0.0;

        foreach (var queryTerm in queryTerms)
        {
            if (textLower.Contains(queryTerm))
            {
                overlapCount++;

                // Early match bonus (term appears in first N chars = more prominent)
                var firstOccurrence = textLower.IndexOf(queryTerm, StringComparison.Ordinal);
                if (firstOccurrence < _config.EarlyMatchThreshold)
                    earlyBonus += _config.EarlyMatchBonus;
            }
        }

        var overlapRatio = queryTerms.Count > 0 ? (double)overlapCount / queryTerms.Count : 0;
        score += overlapRatio * _config.TermOverlapWeight;
        score += earlyBonus;

        // === Signal 2: Query term density (prevents keyword stuffing via cap) ===
        var matchCount = queryTerms.Sum(t => CountOccurrences(textLower, t));
        var density = text.Length > 0 ? (matchCount * 100.0) / text.Length : 0;
        score += Math.Min(density, _config.MaxDensityScore);

        // === Signal 3: Exact phrase match (strongest signal of relevance) ===
        var queryLower = fullQuery.ToLowerInvariant();
        if (queryLower.Length > _config.MinPhraseLength && textLower.Contains(queryLower))
        {
            score += _config.ExactPhraseBonus;
        }

        // === Signal 4: Structural signals (document structure matters) ===
        // Headings matching query are highly relevant
        if (segment.Type == SegmentType.Heading && overlapCount > 0)
        {
            score += _config.HeadingMatchBonus;
        }

        // Section title matches indicate topical relevance
        if (!string.IsNullOrEmpty(segment.SectionTitle))
        {
            var sectionLower = segment.SectionTitle.ToLowerInvariant();
            var sectionOverlap = queryTerms.Count(t => sectionLower.Contains(t));
            score += sectionOverlap * _config.SectionTermWeight;
        }

        // === Signal 5: Existing embedding similarity (semantic signal) ===
        // Leverage pre-computed dense retrieval scores
        if (segment.QuerySimilarity > 0)
        {
            score += segment.QuerySimilarity * _config.EmbeddingSimilarityWeight;
        }

        // === Signal 6: Position weight (intro/conclusion segments) ===
        // Position weights set by HierarchicalEncoder or SegmentExtractor
        score += (segment.PositionWeight - 1.0) * _config.PositionWeight;

        // === Signal 7: Length bonus (longer segments provide more context) ===
        // Capped to avoid over-weighting very long segments
        var lengthBonus = Math.Min(text.Length / _config.OptimalLength, 1.0);
        score += lengthBonus * _config.MaxLengthBonus;

        return score;
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
        "this", "that", "these", "those", "it", "its", "as", "by", "from", "can", "will",
        "what", "how", "why", "when", "where", "which", "who", "whom", "do", "does", "did"
    };

    private static List<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .Distinct()
            .ToList();
    }
}
