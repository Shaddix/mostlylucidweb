using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Cross-encoder style reranker for precision boost on retrieved segments.
///
/// Unlike bi-encoders (which embed query and document separately), cross-encoders
/// jointly encode query + document pairs, enabling deeper interaction modeling.
///
/// This implementation uses a lightweight proxy approach:
/// 1. Exact term overlap (like a mini BM25)
/// 2. Semantic similarity boost from existing embeddings
/// 3. Structural signals (heading proximity, section relevance)
/// 4. Query term density
///
/// For production, you could swap this with a real cross-encoder model via ONNX.
/// </summary>
public class CrossEncoderReranker
{
    private readonly bool _verbose;

    public CrossEncoderReranker(bool verbose = false)
    {
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
    private double ComputeCrossEncoderScore(Segment segment, List<string> queryTerms, string fullQuery)
    {
        var text = segment.Text;
        var textLower = text.ToLowerInvariant();
        var textTerms = Tokenize(text);

        double score = 0;

        // === Signal 1: Exact term overlap (weighted by position and rarity) ===
        var overlapCount = 0;
        var earlyOverlapBonus = 0.0;

        foreach (var queryTerm in queryTerms)
        {
            if (textLower.Contains(queryTerm))
            {
                overlapCount++;

                // Early match bonus (term appears in first 50 chars)
                var firstOccurrence = textLower.IndexOf(queryTerm, StringComparison.Ordinal);
                if (firstOccurrence < 50)
                    earlyOverlapBonus += 0.1;
            }
        }

        var overlapRatio = queryTerms.Count > 0 ? (double)overlapCount / queryTerms.Count : 0;
        score += overlapRatio * 3.0; // Strong weight for term overlap
        score += earlyOverlapBonus;

        // === Signal 2: Query term density (matches per 100 chars) ===
        var matchCount = queryTerms.Sum(t => CountOccurrences(textLower, t));
        var density = text.Length > 0 ? (matchCount * 100.0) / text.Length : 0;
        score += Math.Min(density, 2.0); // Cap to avoid spam

        // === Signal 3: Exact phrase match (huge bonus) ===
        var queryLower = fullQuery.ToLowerInvariant();
        if (queryLower.Length > 5 && textLower.Contains(queryLower))
        {
            score += 5.0; // Exact phrase match is strong signal
        }

        // === Signal 4: Structural signals ===
        // Headings that match query terms are highly relevant
        if (segment.Type == SegmentType.Heading && overlapCount > 0)
        {
            score += 2.0;
        }

        // Section title matches
        if (!string.IsNullOrEmpty(segment.SectionTitle))
        {
            var sectionLower = segment.SectionTitle.ToLowerInvariant();
            var sectionOverlap = queryTerms.Count(t => sectionLower.Contains(t));
            score += sectionOverlap * 0.5;
        }

        // === Signal 5: Existing embedding similarity (if available) ===
        if (segment.QuerySimilarity > 0)
        {
            score += segment.QuerySimilarity * 2.0;
        }

        // === Signal 6: Position weight (intro/conclusion) ===
        score += (segment.PositionWeight - 1.0) * 0.5;

        // === Signal 7: Length bonus (longer = more context, up to a point) ===
        var lengthBonus = Math.Min(text.Length / 200.0, 1.0);
        score += lengthBonus * 0.3;

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
