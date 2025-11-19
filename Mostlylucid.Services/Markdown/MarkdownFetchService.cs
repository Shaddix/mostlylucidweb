using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Shared.Entities;
using Polly;
using Polly.Retry;
using System.Security.Cryptography;
using System.Text;

namespace Mostlylucid.Services.Markdown;

public class MarkdownFetchService : IMarkdownFetchService
{
    private readonly IMostlylucidDBContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MarkdownFetchService> _logger;
    private readonly BlogPostProcessingContext _processingContext;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public MarkdownFetchService(
        IMostlylucidDBContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<MarkdownFetchService> logger,
        BlogPostProcessingContext processingContext)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _processingContext = processingContext;

        // Create retry policy with exponential backoff
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} for URL {Url} after {Delay}s. Reason: {Reason}",
                        retryCount,
                        context.GetValueOrDefault("url"),
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase ?? "Unknown"
                    );
                });
    }

    public async Task<MarkdownFetchResult> FetchMarkdownAsync(
        string url,
        int pollFrequencyHours,
        int blogPostId)
    {
        try
        {
            // Use processing context blog post ID if available and blogPostId parameter is 0
            if (blogPostId == 0 && _processingContext.CurrentBlogPostId > 0)
            {
                blogPostId = _processingContext.CurrentBlogPostId;
                _logger.LogDebug(
                    "Using blog post ID {BlogPostId} from processing context for fetch {Url}",
                    blogPostId,
                    url);
            }

            // Check cache by URL (with optional blogPostId filter)
            MarkdownFetchEntity? fetchEntity = null;
            if (blogPostId > 0)
            {
                // If we have a blog post ID, try to find an entry for this specific post
                fetchEntity = await _context.MarkdownFetches
                    .FirstOrDefaultAsync(f => f.Url == url && f.BlogPostId == blogPostId);
            }
            else
            {
                // If no blog post ID, find any cached entry for this URL
                fetchEntity = await _context.MarkdownFetches
                    .FirstOrDefaultAsync(f => f.Url == url);
            }

            // If we have a cached version and it's still fresh, return it
            if (fetchEntity != null &&
                !string.IsNullOrEmpty(fetchEntity.CachedContent) &&
                fetchEntity.LastFetchedAt.HasValue)
            {
                var age = DateTimeOffset.UtcNow - fetchEntity.LastFetchedAt.Value;
                if (age.TotalHours < pollFrequencyHours)
                {
                    _logger.LogDebug(
                        "Returning cached markdown from {Url} (age: {Age:F2}h, blogPostId: {BlogPostId})",
                        url,
                        age.TotalHours,
                        fetchEntity.BlogPostId);

                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = fetchEntity.CachedContent
                    };
                }
            }

            // Fetch fresh content
            var fetchResult = await FetchFromUrlAsync(url);

            if (!fetchResult.Success)
            {
                // If fetch failed but we have cached content, return cached
                if (fetchEntity != null && !string.IsNullOrEmpty(fetchEntity.CachedContent))
                {
                    _logger.LogWarning(
                        "Fetch failed for {Url}, returning stale cached content. Error: {Error}",
                        url,
                        fetchResult.ErrorMessage);

                    await UpdateFetchAttemptAsync(fetchEntity, false, fetchResult.ErrorMessage);

                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = fetchEntity.CachedContent
                    };
                }

                // No cached content, return error
                return fetchResult;
            }

            // Always persist to database (even without a blogPostId)
            // Update or create fetch entity
            if (fetchEntity == null)
            {
                fetchEntity = new MarkdownFetchEntity
                {
                    Url = url,
                    BlogPostId = blogPostId > 0 ? blogPostId : null,
                    PollFrequencyHours = pollFrequencyHours,
                    CreatedAt = DateTimeOffset.UtcNow,
                    IsEnabled = true
                };
                _context.MarkdownFetches.Add(fetchEntity);
            }

            var contentHash = ComputeHash(fetchResult.Content);
            var contentChanged = fetchEntity.ContentHash != contentHash;

            fetchEntity.CachedContent = fetchResult.Content;
            fetchEntity.ContentHash = contentHash;
            fetchEntity.LastFetchedAt = DateTimeOffset.UtcNow;
            fetchEntity.LastAttemptedAt = DateTimeOffset.UtcNow;
            fetchEntity.ConsecutiveFailures = 0;
            fetchEntity.LastError = null;
            fetchEntity.UpdatedAt = DateTimeOffset.UtcNow;
            fetchEntity.PollFrequencyHours = pollFrequencyHours;

            // Update BlogPostId if we now have one and didn't before
            if (blogPostId > 0 && fetchEntity.BlogPostId == null)
            {
                fetchEntity.BlogPostId = blogPostId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully fetched and cached markdown from {Url} (blogPostId: {BlogPostId}, content changed: {Changed})",
                url,
                fetchEntity.BlogPostId ?? 0,
                contentChanged);

            return fetchResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching markdown from {Url}", url);
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private async Task<MarkdownFetchResult> FetchFromUrlAsync(string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var context = new Context { { "url", url } };

            var response = await _retryPolicy.ExecuteAsync(
                ctx => client.GetAsync(url),
                context);

            if (!response.IsSuccessStatusCode)
            {
                return new MarkdownFetchResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new MarkdownFetchResult
                {
                    Success = false,
                    ErrorMessage = "Fetched content was empty"
                };
            }

            return new MarkdownFetchResult
            {
                Success = true,
                Content = content
            };
        }
        catch (TaskCanceledException)
        {
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = "Request timed out after 30 seconds"
            };
        }
        catch (HttpRequestException ex)
        {
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching from URL {Url}", url);
            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = $"Fetch error: {ex.Message}"
            };
        }
    }

    private async Task UpdateFetchAttemptAsync(
        MarkdownFetchEntity entity,
        bool success,
        string? errorMessage)
    {
        entity.LastAttemptedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (!success)
        {
            entity.ConsecutiveFailures++;
            entity.LastError = errorMessage;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> RemoveCachedMarkdownAsync(string url, int blogPostId = 0)
    {
        var entity = await _context.MarkdownFetches
            .FirstOrDefaultAsync(e => e.Url == url && e.BlogPostId == blogPostId);

        if (entity == null)
        {
            return false;
        }

        _context.MarkdownFetches.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed cached markdown from database for {Url} (blogPostId: {BlogPostId})", url, blogPostId);
        return true;
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
