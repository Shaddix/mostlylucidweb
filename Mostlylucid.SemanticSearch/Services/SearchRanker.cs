using Mostlylucid.Shared.Interfaces;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.SemanticSearch.Services;

/// <summary>
/// Enhanced search ranking using RRF (Reciprocal Rank Fusion) from LucidRAG GraphRag.
/// Combines BM25 (PostgreSQL FTS) + Vector Search (Qdrant) with category/freshness/popularity boosts.
/// </summary>
public class SearchRanker
{
    /// <summary>
    /// RRF constant - controls how quickly ranking influence decays.
    /// Higher k = more gradual decay. Standard value is 60.
    /// </summary>
    private const int RRF_K = 60;

    /// <summary>
    /// Weights for different ranking factors.
    /// </summary>
    public record RankingWeights
    {
        /// <summary>
        /// Weight for category match boost.
        /// </summary>
        public double CategoryMatchWeight { get; init; } = 2.0;

        /// <summary>
        /// Weight for freshness (recency) boost.
        /// </summary>
        public double FreshnessWeight { get; init; } = 1.5;

        /// <summary>
        /// Weight for title match boost.
        /// </summary>
        public double TitleMatchWeight { get; init; } = 1.0;

        /// <summary>
        /// Weight for popularity boost (from Umami analytics).
        /// Uses log scaling to prevent mega-popular posts from dominating.
        /// </summary>
        public double PopularityWeight { get; init; } = 1.0;
    }

    private readonly RankingWeights _weights;
    private readonly IPopularityProvider? _popularityProvider;

    public SearchRanker(RankingWeights? weights = null, IPopularityProvider? popularityProvider = null)
    {
        _weights = weights ?? new RankingWeights();
        _popularityProvider = popularityProvider;
    }

    /// <summary>
    /// Fuse BM25 and vector search results using Reciprocal Rank Fusion.
    /// This is the core technique from LucidRAG GraphRag SearchService.
    /// </summary>
    public List<BlogPostDto> FuseResults(
        List<BlogPostDto> bm25Results,
        List<(BlogPostDto Post, float Score)> vectorResults,
        string query)
    {
        // RRF scoring: 1 / (k + rank)
        var rrfScores = new Dictionary<string, (double Score, BlogPostDto Post)>();

        // Add BM25 results (rank 0 is best)
        for (int i = 0; i < bm25Results.Count; i++)
        {
            var post = bm25Results[i];
            var rrfScore = 1.0 / (RRF_K + i + 1);
            rrfScores[post.Slug] = (rrfScore, post);
        }

        // Add vector results (rank 0 is best)
        for (int i = 0; i < vectorResults.Count; i++)
        {
            var (post, _) = vectorResults[i];
            var rrfScore = 1.0 / (RRF_K + i + 1);

            if (rrfScores.TryGetValue(post.Slug, out var existing))
            {
                // Post appears in both results - combine scores
                rrfScores[post.Slug] = (existing.Score + rrfScore, existing.Post);
            }
            else
            {
                rrfScores[post.Slug] = (rrfScore, post);
            }
        }

        // Apply boosts to fused scores
        var boostedResults = rrfScores.Select(kv =>
        {
            var (slug, (rrfScore, post)) = kv;
            var boost = CalculateBoost(post, query);
            var finalScore = rrfScore + boost;

            return (Post: post, Score: finalScore);
        });

        // Return sorted by final score
        return boostedResults
            .OrderByDescending(x => x.Score)
            .Select(x => x.Post)
            .ToList();
    }

    /// <summary>
    /// Calculate ranking boost based on post metadata (categories, freshness, title match, popularity).
    /// </summary>
    private double CalculateBoost(BlogPostDto post, string query)
    {
        var boost = 0.0;

        // Category match boost
        var queryTokens = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var categoryMatch = post.Categories.Any(cat =>
            queryTokens.Any(token => cat.ToLowerInvariant().Contains(token)));

        if (categoryMatch)
        {
            boost += _weights.CategoryMatchWeight;
        }

        // Title match boost
        var titleMatch = queryTokens.Any(token =>
            post.Title.ToLowerInvariant().Contains(token));

        if (titleMatch)
        {
            boost += _weights.TitleMatchWeight;
        }

        // Freshness boost (exponential decay over 1 year)
        var daysSincePublished = (DateTimeOffset.UtcNow - post.PublishedDate).TotalDays;
        var freshnessBoost = Math.Exp(-daysSincePublished / 365.0);
        boost += freshnessBoost * _weights.FreshnessWeight;

        // Popularity boost (from Umami analytics)
        if (_popularityProvider != null)
        {
            var views = _popularityProvider.GetViewCount(post.Slug);
            if (views > 0)
            {
                // Log scaling: log(views + 1) normalized to 0-1 range
                // Assumes max ~10,000 views for normalization (log10(10001) ≈ 4)
                var popularityScore = Math.Log10(views + 1) / 4.0;
                boost += popularityScore * _weights.PopularityWeight;
            }
        }

        return boost;
    }

    /// <summary>
    /// Rank results using just RRF (no separate BM25/vector lists).
    /// Useful when you have a single result set but want to apply boosts.
    /// </summary>
    public List<BlogPostDto> RankWithBoosts(
        List<BlogPostDto> posts,
        string query,
        Func<BlogPostDto, double>? baseScoreExtractor = null)
    {
        var scoredResults = posts.Select((post, index) =>
        {
            // Use provided base score or RRF position
            var baseScore = baseScoreExtractor?.Invoke(post) ?? (1.0 / (RRF_K + index + 1));
            var boost = CalculateBoost(post, query);
            var finalScore = baseScore + boost;

            return (Post: post, Score: finalScore);
        });

        return scoredResults
            .OrderByDescending(x => x.Score)
            .Select(x => x.Post)
            .ToList();
    }
}
