using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.Postgre;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.Services.Blog;

/// <summary>
/// Service for suggesting alternative blog post slugs using fuzzy string matching
/// with machine learning capabilities
/// </summary>
public class SlugSuggestionService : ISlugSuggestionService
{
    private readonly MostlylucidDbContext _context;
    private readonly ILogger<SlugSuggestionService> _logger;
    private const int MaxLevenshteinDistance = 5; // Maximum edit distance for suggestions
    private const double MinSimilarityThreshold = 0.4; // Minimum similarity score (0-1)
    private const double LearnedWeightBoost = 0.3; // Boost for learned redirects

    public SlugSuggestionService(
        MostlylucidDbContext context,
        ILogger<SlugSuggestionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<PostListModel>> GetSlugSuggestionsAsync(
        string requestedSlug,
        string language = "en",
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestedSlug))
        {
            return new List<PostListModel>();
        }

        // Normalize the requested slug
        var normalizedSlug = NormalizeSlug(requestedSlug);

        _logger.LogInformation("Finding suggestions for slug: {RequestedSlug} (normalized: {NormalizedSlug})",
            requestedSlug, normalizedSlug);

        // Get learned redirects for this slug
        var learnedRedirects = await _context.SlugRedirects
            .AsNoTracking()
            .Where(r => r.FromSlug == normalizedSlug && r.Language == language)
            .ToDictionaryAsync(r => r.ToSlug, r => r, cancellationToken);

        // Get all blog posts with their slugs for the specified language
        var allPosts = await _context.BlogPosts
            .AsNoTracking()
            .Where(p => p.LanguageEntity.Name == language && !p.IsHidden)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Title,
                p.PublishedDate,
                p.Categories,
                Language = p.LanguageEntity.Name
            })
            .ToListAsync(cancellationToken);

        if (!allPosts.Any())
        {
            _logger.LogWarning("No blog posts found for language: {Language}", language);
            return new List<PostListModel>();
        }

        // Calculate similarity scores for each post, boosting learned redirects
        var scoredPosts = allPosts
            .Select(post =>
            {
                var baseScore = CalculateSimilarity(normalizedSlug, post.Slug);

                // Boost score if this is a learned redirect
                if (learnedRedirects.TryGetValue(post.Slug, out var redirect))
                {
                    // Add boost based on confidence score
                    baseScore += LearnedWeightBoost * redirect.ConfidenceScore;
                    baseScore = Math.Min(1.0, baseScore); // Cap at 1.0

                    _logger.LogDebug("Boosting {Slug} from {BaseScore} by {Boost} (weight: {Weight}, confidence: {Confidence})",
                        post.Slug, baseScore - LearnedWeightBoost * redirect.ConfidenceScore,
                        LearnedWeightBoost * redirect.ConfidenceScore, redirect.Weight, redirect.ConfidenceScore);
                }

                return new
                {
                    Post = post,
                    Score = baseScore
                };
            })
            .Where(x => x.Score >= MinSimilarityThreshold)
            .OrderByDescending(x => x.Score)
            .Take(maxSuggestions)
            .ToList();

        if (!scoredPosts.Any())
        {
            _logger.LogInformation("No similar slugs found for: {RequestedSlug}", requestedSlug);

            // Fall back to checking if any slug contains the requested slug as substring
            scoredPosts = allPosts
                .Where(p => p.Slug.Contains(normalizedSlug, StringComparison.OrdinalIgnoreCase) ||
                           normalizedSlug.Contains(p.Slug, StringComparison.OrdinalIgnoreCase))
                .Select(post => new
                {
                    Post = post,
                    Score = 0.5 // Give these a moderate score
                })
                .Take(maxSuggestions)
                .ToList();
        }

        _logger.LogInformation("Found {Count} suggestions for slug: {RequestedSlug}",
            scoredPosts.Count, requestedSlug);

        // Map to PostListModel
        var suggestions = scoredPosts.Select(x => new PostListModel
        {
            Id = x.Post.Id,
            Slug = x.Post.Slug,
            Title = x.Post.Title,
            PublishedDate = x.Post.PublishedDate,
            Categories = x.Post.Categories.Select(c => c.Name).ToList(),
            Language = x.Post.Language
        }).ToList();

        return suggestions;
    }

    /// <inheritdoc />
    public async Task RecordSuggestionClickAsync(
        string requestedSlug,
        string clickedSlug,
        string language,
        int suggestionPosition,
        double originalScore,
        string? userIp = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestedSlug = NormalizeSlug(requestedSlug);
        var normalizedClickedSlug = NormalizeSlug(clickedSlug);

        _logger.LogInformation("Recording click: {RequestedSlug} -> {ClickedSlug} (position: {Position})",
            normalizedRequestedSlug, normalizedClickedSlug, suggestionPosition);

        // Record the click event
        var clickEntity = new SlugSuggestionClickEntity
        {
            RequestedSlug = normalizedRequestedSlug,
            ClickedSlug = normalizedClickedSlug,
            Language = language,
            SuggestionPosition = suggestionPosition,
            OriginalSimilarityScore = originalScore,
            UserIp = userIp,
            UserAgent = userAgent,
            ClickedAt = DateTimeOffset.UtcNow
        };

        _context.SlugSuggestionClicks.Add(clickEntity);

        // Update or create the redirect mapping
        var redirect = await _context.SlugRedirects
            .FirstOrDefaultAsync(r =>
                r.FromSlug == normalizedRequestedSlug &&
                r.ToSlug == normalizedClickedSlug &&
                r.Language == language,
                cancellationToken);

        if (redirect == null)
        {
            redirect = new SlugRedirectEntity
            {
                FromSlug = normalizedRequestedSlug,
                ToSlug = normalizedClickedSlug,
                Language = language,
                Weight = 1,
                ShownCount = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };
            redirect.UpdateConfidenceScore();
            _context.SlugRedirects.Add(redirect);
        }
        else
        {
            redirect.Weight++;
            redirect.LastClickedAt = DateTimeOffset.UtcNow;
            redirect.UpdatedAt = DateTimeOffset.UtcNow;
            redirect.UpdateConfidenceScore();
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated redirect mapping: {From} -> {To} (weight: {Weight}, confidence: {Confidence:P}, auto: {Auto})",
            normalizedRequestedSlug, normalizedClickedSlug, redirect.Weight,
            redirect.ConfidenceScore, redirect.AutoRedirect);
    }

    /// <inheritdoc />
    public async Task RecordSuggestionsShownAsync(
        string requestedSlug,
        List<string> shownSlugs,
        string language,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestedSlug = NormalizeSlug(requestedSlug);

        foreach (var shownSlug in shownSlugs)
        {
            var normalizedShownSlug = NormalizeSlug(shownSlug);

            var redirect = await _context.SlugRedirects
                .FirstOrDefaultAsync(r =>
                    r.FromSlug == normalizedRequestedSlug &&
                    r.ToSlug == normalizedShownSlug &&
                    r.Language == language,
                    cancellationToken);

            if (redirect != null)
            {
                redirect.ShownCount++;
                redirect.UpdatedAt = DateTimeOffset.UtcNow;
                redirect.UpdateConfidenceScore();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetAutoRedirectSlugAsync(
        string requestedSlug,
        string language,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(requestedSlug);

        var redirect = await _context.SlugRedirects
            .AsNoTracking()
            .Where(r =>
                r.FromSlug == normalizedSlug &&
                r.Language == language &&
                r.AutoRedirect)
            .OrderByDescending(r => r.ConfidenceScore)
            .FirstOrDefaultAsync(cancellationToken);

        if (redirect != null)
        {
            _logger.LogInformation(
                "Auto-redirect found: {From} -> {To} (weight: {Weight}, confidence: {Confidence:P})",
                normalizedSlug, redirect.ToSlug, redirect.Weight, redirect.ConfidenceScore);
        }

        return redirect?.ToSlug;
    }

    /// <summary>
    /// Calculate similarity between two strings using a combination of Levenshtein distance
    /// and longest common subsequence
    /// </summary>
    private double CalculateSimilarity(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // Calculate Levenshtein distance
        var distance = CalculateLevenshteinDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);

        if (maxLength == 0)
        {
            return 1.0;
        }

        // Convert distance to similarity score (0-1, where 1 is identical)
        var levenshteinSimilarity = 1.0 - (double)distance / maxLength;

        // Calculate substring bonus (if one is contained in the other)
        var substringBonus = 0.0;
        if (source.Contains(target) || target.Contains(source))
        {
            substringBonus = 0.2;
        }

        // Calculate prefix bonus (common starting characters)
        var prefixLength = GetCommonPrefixLength(source, target);
        var prefixBonus = (double)prefixLength / Math.Min(source.Length, target.Length) * 0.1;

        // Combine scores
        var finalScore = Math.Min(1.0, levenshteinSimilarity + substringBonus + prefixBonus);

        return finalScore;
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings (edit distance)
    /// </summary>
    private int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return target?.Length ?? 0;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        var sourceLength = source.Length;
        var targetLength = target.Length;

        // Create a matrix to store distances
        var matrix = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column and row
        for (var i = 0; i <= sourceLength; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= targetLength; j++)
        {
            matrix[0, j] = j;
        }

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,      // Deletion
                        matrix[i, j - 1] + 1),      // Insertion
                    matrix[i - 1, j - 1] + cost);   // Substitution
            }
        }

        return matrix[sourceLength, targetLength];
    }

    /// <summary>
    /// Get the length of the common prefix between two strings
    /// </summary>
    private int GetCommonPrefixLength(string source, string target)
    {
        var minLength = Math.Min(source.Length, target.Length);
        var prefixLength = 0;

        for (var i = 0; i < minLength; i++)
        {
            if (source[i] == target[i])
            {
                prefixLength++;
            }
            else
            {
                break;
            }
        }

        return prefixLength;
    }

    /// <summary>
    /// Normalize a slug for comparison
    /// </summary>
    private string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        // Remove .md extension if present
        if (slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            slug = slug[..^3];
        }

        // Convert underscores and spaces to hyphens, and lowercase
        slug = slug.Replace('_', '-')
                  .Replace(' ', '-')
                  .ToLowerInvariant();

        // Remove any trailing slashes
        slug = slug.TrimEnd('/');

        return slug;
    }
}
