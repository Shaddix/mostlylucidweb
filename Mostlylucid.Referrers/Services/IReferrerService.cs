using Microsoft.AspNetCore.Http;
using Mostlylucid.Referrers.Models;

namespace Mostlylucid.Referrers.Services;

/// <summary>
/// Service for tracking and retrieving blog post referrers
/// </summary>
public interface IReferrerService
{
    /// <summary>
    /// Records an incoming referrer for a blog post, filtering out bots
    /// </summary>
    /// <param name="postSlug">The slug of the blog post being accessed</param>
    /// <param name="httpContext">The HTTP context containing referrer and request info</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the referrer was recorded (not a bot, not excluded), false otherwise</returns>
    Task<bool> RecordReferrerAsync(string postSlug, HttpContext httpContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all verified referrers for a specific blog post
    /// </summary>
    /// <param name="postSlug">The slug of the blog post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of referrers for the post</returns>
    Task<PostReferrers> GetReferrersForPostAsync(string postSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets referrers across all posts, ordered by hit count
    /// </summary>
    /// <param name="limit">Maximum number of referrers to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of top referrers</returns>
    Task<IReadOnlyList<Referrer>> GetTopReferrersAsync(int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a referrer URL should be excluded based on configuration
    /// </summary>
    /// <param name="referrerUrl">The referrer URL to check</param>
    /// <returns>True if the referrer should be excluded</returns>
    bool IsExcludedReferrer(string referrerUrl);
}
