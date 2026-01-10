using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.SemanticSearch.Services;
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
    private readonly ISemanticSearchService? _semanticSearchService;
    private const int MaxLevenshteinDistance = 5; // Maximum edit distance for suggestions
    private const double MinSimilarityThreshold = 0.4; // Minimum similarity score (0-1)
    private const double LearnedWeightBoost = 0.3; // Boost for learned redirects
    private const double AutoRedirectScoreThreshold = 0.85; // Score threshold for auto-redirect on first-time typos
    private const double AutoRedirectScoreGap = 0.15; // Gap between top match and second to auto-redirect

    public SlugSuggestionService(
        MostlylucidDbContext context,
        ILogger<SlugSuggestionService> logger,
        ISemanticSearchService? semanticSearchService = null)
    {
        _context = context;
        _logger = logger;
        _semanticSearchService = semanticSearchService;
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
            Id = x.Post.Id.ToString(),
            Slug = x.Post.Slug,
            Title = x.Post.Title,
            PublishedDate = x.Post.PublishedDate.DateTime,
            Categories = x.Post.Categories.Select(c => c.Name).ToArray(),
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

    /// <inheritdoc />
    public async Task<List<SlugSuggestionWithScore>> GetSuggestionsWithScoreAsync(
        string requestedSlug,
        string language = "en",
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestedSlug))
        {
            return new List<SlugSuggestionWithScore>();
        }

        var normalizedSlug = NormalizeSlug(requestedSlug);

        // First, try to extract a canonical slug from the URL
        // E.g., /archive/blog-slug.aspx -> blog-slug
        // E.g., /blog/my-post-slug -> my-post-slug
        var extractedSlug = ExtractSlugFromUrl(requestedSlug);

        _logger.LogInformation("Finding suggestions for slug: {RequestedSlug} (extracted slug: {ExtractedSlug})",
            requestedSlug, extractedSlug);

        var results = new List<SlugSuggestionWithScore>();

        // Try exact slug match first (canonical match)
        if (!string.IsNullOrWhiteSpace(extractedSlug))
        {
            var exactMatch = await _context.BlogPosts
                .AsNoTracking()
                .Where(p => p.Slug == extractedSlug && p.LanguageEntity.Name == language && !p.IsHidden)
                .Select(p => new PostListModel
                {
                    Id = p.Id.ToString(),
                    Slug = p.Slug,
                    Title = p.Title,
                    Language = p.LanguageEntity.Name,
                    PublishedDate = p.PublishedDate.DateTime,
                    Categories = p.Categories.Select(c => c.Name).ToArray()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (exactMatch != null)
            {
                _logger.LogInformation("Found exact canonical slug match for: {ExtractedSlug}", extractedSlug);
                return new List<SlugSuggestionWithScore>
                {
                    new SlugSuggestionWithScore(exactMatch, 1.0)
                };
            }
        }

        // No exact match - prepare search term for semantic/fuzzy search
        var searchTerm = ExtractSearchTermFromUrl(requestedSlug);

        _logger.LogInformation("No exact match found, searching semantically for: {SearchTerm}", searchTerm);

        // Try semantic search next if available - searches for similar URL patterns
        if (_semanticSearchService != null)
        {
            try
            {
                // Search using the URL-derived term to find semantically similar slugs/URLs
                var semanticResults = await _semanticSearchService.SearchAsync(searchTerm, maxSuggestions, cancellationToken);

                if (semanticResults.Count > 0)
                {
                    // Get slugs from semantic search, then look up details from PostgreSQL
                    var slugs = semanticResults.Select(sr => sr.Slug).ToList();
                    var posts = await _context.BlogPosts
                        .Where(p => slugs.Contains(p.Slug) && p.LanguageEntity.Name == language)
                        .Select(p => new PostListModel
                        {
                            Id = p.Id.ToString(),
                            Slug = p.Slug,
                            Title = p.Title,
                            Language = p.LanguageEntity.Name,
                            PublishedDate = p.PublishedDate.DateTime,
                            Categories = p.Categories.Select(c => c.Name).ToArray()
                        })
                        .ToListAsync(cancellationToken);

                    // Match back with semantic scores
                    foreach (var post in posts)
                    {
                        var semanticResult = semanticResults.FirstOrDefault(sr => sr.Slug == post.Slug);
                        if (semanticResult != null)
                        {
                            results.Add(new SlugSuggestionWithScore(post, semanticResult.Score));
                        }
                    }

                    if (results.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} semantic search suggestions for: {RequestedSlug}",
                            results.Count, requestedSlug);
                        return results;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic search failed for slug suggestion: {RequestedSlug}", requestedSlug);
            }
        }

        // Fall back to Levenshtein-based matching
        var learnedRedirects = await _context.SlugRedirects
            .AsNoTracking()
            .Where(r => r.FromSlug == normalizedSlug && r.Language == language)
            .ToDictionaryAsync(r => r.ToSlug, r => r, cancellationToken);

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
            return results;
        }

        var scoredPosts = allPosts
            .Select(post =>
            {
                var baseScore = CalculateSimilarity(normalizedSlug, post.Slug);

                if (learnedRedirects.TryGetValue(post.Slug, out var redirect))
                {
                    baseScore += LearnedWeightBoost * redirect.ConfidenceScore;
                    baseScore = Math.Min(1.0, baseScore);
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

        results = scoredPosts.Select(x => new SlugSuggestionWithScore(
            new PostListModel
            {
                Id = x.Post.Id.ToString(),
                Slug = x.Post.Slug,
                Title = x.Post.Title,
                PublishedDate = x.Post.PublishedDate.DateTime,
                Categories = x.Post.Categories.Select(c => c.Name).ToArray(),
                Language = x.Post.Language
            },
            x.Score
        )).ToList();

        _logger.LogInformation("Found {Count} Levenshtein suggestions for slug: {RequestedSlug}",
            results.Count, requestedSlug);

        return results;
    }

    /// <inheritdoc />
    public async Task<string?> GetFirstTimeAutoRedirectSlugAsync(
        string requestedSlug,
        string language,
        CancellationToken cancellationToken = default)
    {
        var suggestions = await GetSuggestionsWithScoreAsync(requestedSlug, language, 2, cancellationToken);

        if (suggestions.Count == 0)
        {
            return null;
        }

        var topMatch = suggestions[0];

        // Check if the top match has a high enough score
        if (topMatch.Score < AutoRedirectScoreThreshold)
        {
            _logger.LogDebug(
                "Top match score {Score:F2} for {RequestedSlug} -> {TargetSlug} below auto-redirect threshold {Threshold:F2}",
                topMatch.Score, requestedSlug, topMatch.Post.Slug, AutoRedirectScoreThreshold);
            return null;
        }

        // If there's only one suggestion with high score, redirect
        if (suggestions.Count == 1)
        {
            _logger.LogInformation(
                "Auto-redirect (single high-confidence match): {RequestedSlug} -> {TargetSlug} (score: {Score:F2})",
                requestedSlug, topMatch.Post.Slug, topMatch.Score);
            return topMatch.Post.Slug;
        }

        // If there are multiple suggestions, only redirect if there's a significant gap
        var secondMatch = suggestions[1];
        var scoreGap = topMatch.Score - secondMatch.Score;

        if (scoreGap >= AutoRedirectScoreGap)
        {
            _logger.LogInformation(
                "Auto-redirect (clear winner): {RequestedSlug} -> {TargetSlug} (score: {Score:F2}, gap: {Gap:F2})",
                requestedSlug, topMatch.Post.Slug, topMatch.Score, scoreGap);
            return topMatch.Post.Slug;
        }

        _logger.LogDebug(
            "No auto-redirect for {RequestedSlug}: scores too close ({TopScore:F2} vs {SecondScore:F2})",
            requestedSlug, topMatch.Score, secondMatch.Score);
        return null;
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

    /// <inheritdoc />
    public async Task<List<SlugSuggestionWithScore>> GetSuggestionsForArchiveIdAsync(
        string archiveId,
        string language = "en",
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archiveId))
        {
            return new List<SlugSuggestionWithScore>();
        }

        _logger.LogInformation("Finding suggestions for archive ID: {ArchiveId}", archiveId);

        // Get all blog posts
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
            return new List<SlugSuggestionWithScore>();
        }

        // Extract numbers from slugs and match against the archive ID
        var scoredPosts = allPosts
            .Select(post =>
            {
                var numbersInSlug = ExtractNumbers(post.Slug);
                var bestScore = 0.0;

                // Try matching the archive ID against each number in the slug
                foreach (var number in numbersInSlug)
                {
                    var score = CalculateNumericSimilarity(archiveId, number);
                    if (score > bestScore)
                    {
                        bestScore = score;
                    }
                }

                // Also try matching the archive ID against the full slug (for cases like "post-445")
                var slugWithoutDashes = post.Slug.Replace("-", "");
                if (slugWithoutDashes.Contains(archiveId, StringComparison.OrdinalIgnoreCase))
                {
                    bestScore = Math.Max(bestScore, 0.9); // High score for exact substring match
                }

                return new
                {
                    Post = post,
                    Score = bestScore
                };
            })
            .Where(x => x.Score >= 0.5) // Only include reasonably good matches
            .OrderByDescending(x => x.Score)
            .Take(maxSuggestions)
            .ToList();

        var results = scoredPosts.Select(x => new SlugSuggestionWithScore(
            new PostListModel
            {
                Id = x.Post.Id.ToString(),
                Slug = x.Post.Slug,
                Title = x.Post.Title,
                PublishedDate = x.Post.PublishedDate.DateTime,
                Categories = x.Post.Categories.Select(c => c.Name).ToArray(),
                Language = x.Post.Language
            },
            x.Score
        )).ToList();

        _logger.LogInformation("Found {Count} archive ID suggestions for: {ArchiveId}",
            results.Count, archiveId);

        return results;
    }

    /// <summary>
    /// Extract all numeric sequences from a string
    /// </summary>
    private List<string> ExtractNumbers(string input)
    {
        var numbers = new List<string>();
        var currentNumber = new System.Text.StringBuilder();

        foreach (var c in input)
        {
            if (char.IsDigit(c))
            {
                currentNumber.Append(c);
            }
            else if (currentNumber.Length > 0)
            {
                numbers.Add(currentNumber.ToString());
                currentNumber.Clear();
            }
        }

        if (currentNumber.Length > 0)
        {
            numbers.Add(currentNumber.ToString());
        }

        return numbers;
    }

    /// <summary>
    /// Extract a canonical slug from a URL path
    /// E.g., /archive/blog-slug.aspx -> blog-slug
    /// E.g., /blog/my-post-slug -> my-post-slug
    /// E.g., /archive/2003/06/06/old-post.html -> old-post
    /// </summary>
    private string ExtractSlugFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        // Remove query string
        var path = url.Split('?')[0];

        // Remove file extensions (.html, .aspx, etc.)
        path = System.Text.RegularExpressions.Regex.Replace(path, @"\.(html|aspx|md|htm)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Split path into segments
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Find the last meaningful segment (skip dates, common prefixes)
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i];

            // Skip if it's a date pattern or common prefix
            if (System.Text.RegularExpressions.Regex.IsMatch(segment, @"^\d{4}$") || // Year
                System.Text.RegularExpressions.Regex.IsMatch(segment, @"^\d{1,2}$") || // Month/day
                segment.Equals("blog", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("archive", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("archite", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("post", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // This is likely the slug
            return segment.ToLowerInvariant();
        }

        return string.Empty;
    }

    /// <summary>
    /// Extract search terms from a URL path and query string
    /// Splits on common delimiters and filters out noise (dates, numbers, file extensions)
    /// </summary>
    private string ExtractSearchTermFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        // Split on query string first
        var parts = url.Split('?');
        var path = parts[0];
        var query = parts.Length > 1 ? parts[1] : string.Empty;

        // Remove file extensions (.html, .aspx, etc.)
        path = System.Text.RegularExpressions.Regex.Replace(path, @"\.(html|aspx|md|htm)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Split path into segments
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Filter out common noise:
        // - Date patterns (YYYY, MM, DD)
        // - Pure numbers
        // - Single characters
        // - Common path prefixes (blog, archive, post, etc.)
        var meaningfulSegments = pathSegments
            .Where(seg =>
                seg.Length > 1 &&
                !System.Text.RegularExpressions.Regex.IsMatch(seg, @"^\d{4}$") && // Not a year
                !System.Text.RegularExpressions.Regex.IsMatch(seg, @"^\d{1,2}$") && // Not a month/day
                !seg.Equals("blog", StringComparison.OrdinalIgnoreCase) &&
                !seg.Equals("archive", StringComparison.OrdinalIgnoreCase) &&
                !seg.Equals("archite", StringComparison.OrdinalIgnoreCase) &&
                !seg.Equals("post", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        // Convert remaining segments to words (replace hyphens/underscores with spaces)
        var searchWords = meaningfulSegments
            .SelectMany(seg => seg.Replace("-", " ").Replace("_", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        // Also extract query parameters (key=value pairs might have useful terms)
        if (!string.IsNullOrWhiteSpace(query))
        {
            var queryParams = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var param in queryParams)
            {
                var keyValue = param.Split('=');
                if (keyValue.Length == 2 && !string.IsNullOrWhiteSpace(keyValue[1]))
                {
                    // Add both key and value as search terms
                    searchWords.Add(keyValue[0].Replace("-", " ").Replace("_", " "));
                    searchWords.Add(System.Net.WebUtility.UrlDecode(keyValue[1]).Replace("-", " ").Replace("_", " "));
                }
            }
        }

        return string.Join(" ", searchWords.Distinct()).Trim();
    }

    /// <summary>
    /// Calculate similarity between two numeric strings
    /// Uses Levenshtein distance but also considers numeric proximity
    /// </summary>
    private double CalculateNumericSimilarity(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        // Calculate Levenshtein distance for strings
        var distance = CalculateLevenshteinDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);

        if (maxLength == 0)
        {
            return 1.0;
        }

        var stringSimilarity = 1.0 - (double)distance / maxLength;

        // Also consider numeric proximity if both are valid numbers
        double numericSimilarity = 0;
        if (int.TryParse(source, out var sourceNum) && int.TryParse(target, out var targetNum))
        {
            var numericDiff = Math.Abs(sourceNum - targetNum);
            var maxNum = Math.Max(sourceNum, targetNum);
            if (maxNum > 0)
            {
                numericSimilarity = 1.0 - Math.Min(1.0, (double)numericDiff / maxNum);
            }
        }

        // Return the better of the two similarity measures
        return Math.Max(stringSimilarity, numericSimilarity);
    }
}
