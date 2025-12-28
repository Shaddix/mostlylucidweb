using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Configuration for hierarchical document encoding.
/// All values have sensible defaults - only override if you need to tune behavior.
/// </summary>
public class HierarchicalEncoderConfig
{
    // === Embedding blending ===

    /// <summary>
    /// Weight for section context when blending with segment embedding (0-1).
    /// Higher values = more section influence, lower = more individual segment focus.
    /// Default: 0.15 (15% section context)
    /// </summary>
    public float SectionContextWeight { get; set; } = 0.15f;

    // === Section-type boosts (applied to all segments in matching sections) ===

    /// <summary>Boost for introduction/abstract sections. Default: 1.3</summary>
    public double IntroductionBoost { get; set; } = 1.3;

    /// <summary>Boost for conclusion/summary sections. Default: 1.25</summary>
    public double ConclusionBoost { get; set; } = 1.25;

    /// <summary>Boost for results/findings sections. Default: 1.2</summary>
    public double ResultsBoost { get; set; } = 1.2;

    /// <summary>Boost for methods/approach sections. Default: 1.1</summary>
    public double MethodsBoost { get; set; } = 1.1;

    /// <summary>Boost for related work/background sections (lower = less novel). Default: 0.9</summary>
    public double BackgroundBoost { get; set; } = 0.9;

    // === Position-within-section boosts ===

    /// <summary>Boost for first sentence in section (topic sentence). Default: 1.2</summary>
    public double FirstSentenceBoost { get; set; } = 1.2;

    /// <summary>Boost for last sentence in section (conclusion/transition). Default: 1.1</summary>
    public double LastSentenceBoost { get; set; } = 1.1;

    /// <summary>Boost for second sentence (elaboration). Default: 1.05</summary>
    public double SecondSentenceBoost { get; set; } = 1.05;

    // === Heading level boosts ===

    /// <summary>Boost for H1 level headings. Default: 1.15</summary>
    public double H1Boost { get; set; } = 1.15;

    /// <summary>Boost for H2 level headings. Default: 1.1</summary>
    public double H2Boost { get; set; } = 1.1;

    /// <summary>Boost for H3 level headings. Default: 1.05</summary>
    public double H3Boost { get; set; } = 1.05;
}

/// <summary>
/// Hierarchical document encoder that captures structure at multiple levels:
/// - Token level: Individual word embeddings (handled by base embedder)
/// - Sentence level: Sentence embeddings with positional context
/// - Paragraph/Section level: Aggregated embeddings for document sections
/// - Document level: Overall document representation
///
/// This enables structure-aware retrieval where sentences are understood
/// in the context of their containing section and the overall document.
/// </summary>
/// <remarks>
/// Scoring weights are configurable via <see cref="HierarchicalEncoderConfig"/>.
/// Defaults are tuned empirically for academic/technical documents. Adjust for your domain:
/// - Higher SectionContextWeight = more coherence within sections
/// - Higher IntroductionBoost = more weight on opening content
/// - Adjust heading boosts based on document structure depth
/// </remarks>
public class HierarchicalEncoder
{
    private readonly IEmbeddingService _embedder;
    private readonly HierarchicalEncoderConfig _config;
    private readonly bool _verbose;

    /// <summary>
    /// Create a new hierarchical encoder with default configuration.
    /// </summary>
    /// <param name="embedder">The embedding service to use</param>
    /// <param name="verbose">If true, logs encoding statistics to console</param>
    public HierarchicalEncoder(IEmbeddingService embedder, bool verbose = false)
        : this(embedder, new HierarchicalEncoderConfig(), verbose)
    {
    }

    /// <summary>
    /// Create a new hierarchical encoder with custom configuration.
    /// </summary>
    /// <param name="embedder">The embedding service to use</param>
    /// <param name="config">Configuration with custom weights</param>
    /// <param name="verbose">If true, logs encoding statistics to console</param>
    public HierarchicalEncoder(IEmbeddingService embedder, HierarchicalEncoderConfig config, bool verbose = false)
    {
        _embedder = embedder;
        _config = config ?? new HierarchicalEncoderConfig();
        _verbose = verbose;
    }

    /// <summary>
    /// Encode segments with hierarchical context.
    /// Each segment gets enriched with:
    /// - Its own embedding
    /// - Section context embedding (average of siblings)
    /// - Position-aware weighting
    /// </summary>
    public async Task<HierarchicalEncoding> EncodeAsync(
        List<Segment> segments,
        CancellationToken ct = default)
    {
        if (segments.Count == 0)
            return new HierarchicalEncoding(Array.Empty<float>(), new Dictionary<string, SectionEncoding>());

        // Group segments by section
        var sectionGroups = segments
            .GroupBy(s => s.SectionTitle ?? "")
            .ToDictionary(g => g.Key, g => g.ToList());

        if (_verbose)
        {
            Console.WriteLine($"[Hierarchical] Encoding {segments.Count} segments across {sectionGroups.Count} sections");
        }

        // Encode all segments
        var texts = segments.Select(s => s.Text).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(texts, ct);

        // Assign embeddings to segments
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].Embedding = embeddings[i];
        }

        // Compute section-level embeddings (mean of segment embeddings in each section)
        var sectionEncodings = new Dictionary<string, SectionEncoding>();
        foreach (var (sectionTitle, sectionSegments) in sectionGroups)
        {
            var sectionEmbeddings = sectionSegments
                .Where(s => s.Embedding != null)
                .Select(s => s.Embedding!)
                .ToList();

            if (sectionEmbeddings.Count == 0) continue;

            var sectionEmbedding = MeanPool(sectionEmbeddings);
            var sectionEncoding = new SectionEncoding(
                sectionTitle,
                sectionEmbedding,
                sectionSegments.Count,
                sectionSegments.First().HeadingLevel);

            sectionEncodings[sectionTitle] = sectionEncoding;

            // Enrich segments with section context (blend with section embedding)
            foreach (var segment in sectionSegments)
            {
                if (segment.Embedding != null)
                {
                    segment.Embedding = BlendEmbeddings(
                        segment.Embedding,
                        sectionEmbedding,
                        sectionWeight: _config.SectionContextWeight);
                }
            }
        }

        // Compute document-level embedding (mean of all segment embeddings)
        var allEmbeddings = segments
            .Where(s => s.Embedding != null)
            .Select(s => s.Embedding!)
            .ToList();
        var documentEmbedding = MeanPool(allEmbeddings);

        // Apply hierarchical position weighting
        ApplyHierarchicalPositionWeights(segments, sectionGroups);

        if (_verbose)
        {
            Console.WriteLine($"[Hierarchical] Document embedding: {documentEmbedding.Length}d");
            Console.WriteLine($"[Hierarchical] Section embeddings: {sectionEncodings.Count}");
        }

        return new HierarchicalEncoding(documentEmbedding, sectionEncodings);
    }

    /// <summary>
    /// Apply position weights that consider hierarchical structure:
    /// - First/last segments in sections get boost (topic sentences, conclusions)
    /// - Segments under higher-level headings get boost
    /// - Introduction and conclusion sections get boost
    /// </summary>
    private void ApplyHierarchicalPositionWeights(
        List<Segment> segments,
        Dictionary<string, List<Segment>> sectionGroups)
    {
        foreach (var (sectionTitle, sectionSegments) in sectionGroups)
        {
            var sectionLower = sectionTitle.ToLowerInvariant();

            // Section-level boost (configurable per section type)
            double sectionBoost = 1.0;
            if (sectionLower.Contains("introduction") || sectionLower.Contains("abstract"))
                sectionBoost = _config.IntroductionBoost;
            else if (sectionLower.Contains("conclusion") || sectionLower.Contains("summary"))
                sectionBoost = _config.ConclusionBoost;
            else if (sectionLower.Contains("result") || sectionLower.Contains("finding"))
                sectionBoost = _config.ResultsBoost;
            else if (sectionLower.Contains("method") || sectionLower.Contains("approach"))
                sectionBoost = _config.MethodsBoost;
            else if (sectionLower.Contains("related work") || sectionLower.Contains("background"))
                sectionBoost = _config.BackgroundBoost;

            // Position within section (configurable boosts)
            for (int i = 0; i < sectionSegments.Count; i++)
            {
                var segment = sectionSegments[i];
                double positionBoost = 1.0;

                // First sentence in section (topic sentence)
                if (i == 0)
                    positionBoost = _config.FirstSentenceBoost;
                // Last sentence in section (often conclusion/transition)
                else if (i == sectionSegments.Count - 1 && sectionSegments.Count > 2)
                    positionBoost = _config.LastSentenceBoost;
                // Second sentence (often elaborates on topic)
                else if (i == 1 && sectionSegments.Count > 3)
                    positionBoost = _config.SecondSentenceBoost;

                // Heading level boost (H1 > H2 > H3, configurable)
                double levelBoost = segment.HeadingLevel switch
                {
                    1 => _config.H1Boost,
                    2 => _config.H2Boost,
                    3 => _config.H3Boost,
                    _ => 1.0
                };

                // Combine boosts
                segment.PositionWeight = sectionBoost * positionBoost * levelBoost;
            }
        }
    }

    /// <summary>
    /// Mean pool multiple embeddings into one
    /// </summary>
    private static float[] MeanPool(List<float[]> embeddings)
    {
        if (embeddings.Count == 0) return Array.Empty<float>();
        if (embeddings.Count == 1) return embeddings[0];

        var dim = embeddings[0].Length;
        var result = new float[dim];

        foreach (var emb in embeddings)
        {
            for (int i = 0; i < dim; i++)
                result[i] += emb[i];
        }

        for (int i = 0; i < dim; i++)
            result[i] /= embeddings.Count;

        // L2 normalize
        var norm = MathF.Sqrt(result.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < dim; i++)
                result[i] /= norm;
        }

        return result;
    }

    /// <summary>
    /// Blend two embeddings with weighted average
    /// </summary>
    private static float[] BlendEmbeddings(float[] primary, float[] secondary, float sectionWeight)
    {
        var dim = primary.Length;
        var result = new float[dim];
        var primaryWeight = 1.0f - sectionWeight;

        for (int i = 0; i < dim; i++)
            result[i] = primary[i] * primaryWeight + secondary[i] * sectionWeight;

        // L2 normalize
        var norm = MathF.Sqrt(result.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < dim; i++)
                result[i] /= norm;
        }

        return result;
    }
}

/// <summary>
/// Result of hierarchical encoding
/// </summary>
public record HierarchicalEncoding(
    float[] DocumentEmbedding,
    Dictionary<string, SectionEncoding> SectionEncodings);

/// <summary>
/// Section-level encoding
/// </summary>
public record SectionEncoding(
    string Title,
    float[] Embedding,
    int SegmentCount,
    int HeadingLevel);
