using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.Services.Blog;

/// <summary>
/// Represents a slug suggestion with its similarity score
/// </summary>
public record SlugSuggestionWithScore(PostListModel Post, double Score);

/// <summary>
/// Service for suggesting alternative blog post slugs when a 404 occurs
/// with machine learning capabilities
/// </summary>
public interface ISlugSuggestionService
{
    /// <summary>
    /// Get suggested blog posts based on a malformed or not-found slug
    /// This method incorporates learned weights from previous user clicks
    /// </summary>
    /// <param name="requestedSlug">The slug that was not found</param>
    /// <param name="language">The requested language (defaults to "en")</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of suggested blog posts</returns>
    Task<List<PostListModel>> GetSlugSuggestionsAsync(
        string requestedSlug,
        string language = "en",
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record that a user clicked on a suggested slug from a 404 page
    /// This updates the learned weights for future suggestions
    /// </summary>
    /// <param name="requestedSlug">The original slug that caused the 404</param>
    /// <param name="clickedSlug">The suggested slug that was clicked</param>
    /// <param name="language">The language of the blog post</param>
    /// <param name="suggestionPosition">Position of the clicked suggestion in the list (0-based)</param>
    /// <param name="originalScore">The original similarity score from the algorithm</param>
    /// <param name="userIp">User's IP address (optional, for analytics)</param>
    /// <param name="userAgent">User agent string (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordSuggestionClickAsync(
        string requestedSlug,
        string clickedSlug,
        string language,
        int suggestionPosition,
        double originalScore,
        string? userIp = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record that suggestions were shown but not clicked
    /// This helps calculate confidence scores
    /// </summary>
    /// <param name="requestedSlug">The slug that caused the 404</param>
    /// <param name="shownSlugs">List of slugs that were shown as suggestions</param>
    /// <param name="language">The language of the blog post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordSuggestionsShownAsync(
        string requestedSlug,
        List<string> shownSlugs,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a slug should automatically redirect (without showing 404)
    /// </summary>
    /// <param name="requestedSlug">The slug to check</param>
    /// <param name="language">The language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The target slug to redirect to, or null if no auto-redirect</returns>
    Task<string?> GetAutoRedirectSlugAsync(
        string requestedSlug,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get suggested blog posts with similarity scores
    /// This method is used for both showing suggestions and determining auto-redirect
    /// </summary>
    /// <param name="requestedSlug">The slug that was not found</param>
    /// <param name="language">The requested language (defaults to "en")</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of suggestions with their scores</returns>
    Task<List<SlugSuggestionWithScore>> GetSuggestionsWithScoreAsync(
        string requestedSlug,
        string language = "en",
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if there's a high-confidence single match that should auto-redirect
    /// This is for first-time typos before any learned patterns exist
    /// </summary>
    /// <param name="requestedSlug">The slug that was requested</param>
    /// <param name="language">The language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The target slug if high-confidence match found, otherwise null</returns>
    Task<string?> GetFirstTimeAutoRedirectSlugAsync(
        string requestedSlug,
        string language,
        CancellationToken cancellationToken = default);
}
