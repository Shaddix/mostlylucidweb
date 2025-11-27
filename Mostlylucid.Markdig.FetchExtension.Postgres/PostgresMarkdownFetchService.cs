using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;

namespace Mostlylucid.Markdig.FetchExtension.Postgres;

/// <summary>
///     PostgreSQL-based implementation of IMarkdownFetchService.
///     Caches fetched markdown to PostgreSQL database for persistence across restarts.
/// </summary>
public class PostgresMarkdownFetchService : IMarkdownFetchService
{
    private readonly MarkdownCacheDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PostgresMarkdownFetchService> _logger;

    public PostgresMarkdownFetchService(
        MarkdownCacheDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<PostgresMarkdownFetchService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MarkdownFetchResult> FetchMarkdownAsync(
        string url,
        int pollFrequencyHours,
        int blogPostId)
    {
        try
        {
            var cacheKey = GetCacheKey(url, blogPostId);

            // Check if we have cached content
            var cached = await _dbContext.MarkdownCache
                .FirstOrDefaultAsync(c => c.CacheKey == cacheKey);

            if (cached != null)
            {
                var age = DateTimeOffset.UtcNow - cached.LastFetchedAt;
                var isStale = age.TotalHours >= pollFrequencyHours;

                if (!isStale && !string.IsNullOrEmpty(cached.Content))
                {
                    _logger.LogDebug(
                        "Returning cached markdown for {Url} (age: {Age:F2}h)",
                        url,
                        age.TotalHours);

                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = cached.Content,
                        LastRetrieved = cached.LastFetchedAt.UtcDateTime,
                        IsCached = true,
                        IsStale = false,
                        SourceUrl = url,
                        PollFrequencyHours = pollFrequencyHours
                    };
                }
            }

            // Fetch fresh content
            _logger.LogInformation("Fetching fresh markdown from {Url}", url);
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, errorMsg);

                // Return stale cache if available
                if (cached?.Content != null)
                {
                    _logger.LogInformation("Returning stale cached content for {Url}", url);
                    return new MarkdownFetchResult
                    {
                        Success = true,
                        Content = cached.Content,
                        LastRetrieved = cached.LastFetchedAt.UtcDateTime,
                        IsCached = true,
                        IsStale = true,
                        SourceUrl = url,
                        PollFrequencyHours = pollFrequencyHours
                    };
                }

                return new MarkdownFetchResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
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

            // Update or create cache entry
            var now = DateTimeOffset.UtcNow;
            if (cached != null)
            {
                cached.Content = content;
                cached.LastFetchedAt = now;
            }
            else
            {
                cached = new MarkdownCacheEntry
                {
                    Url = url,
                    BlogPostId = blogPostId,
                    Content = content,
                    LastFetchedAt = now,
                    CacheKey = cacheKey
                };
                await _dbContext.MarkdownCache.AddAsync(cached);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully fetched and cached markdown from {Url}", url);

            return new MarkdownFetchResult
            {
                Success = true,
                Content = content,
                LastRetrieved = now.UtcDateTime,
                IsCached = false,
                IsStale = false,
                SourceUrl = url,
                PollFrequencyHours = pollFrequencyHours
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching markdown from {Url}", url);

            return new MarkdownFetchResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> RemoveCachedMarkdownAsync(string url, int blogPostId = 0)
    {
        var cacheKey = GetCacheKey(url, blogPostId);
        var cached = await _dbContext.MarkdownCache.FirstOrDefaultAsync(c => c.CacheKey == cacheKey);

        if (cached == null)
        {
            return false;
        }

        _dbContext.MarkdownCache.Remove(cached);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Removed cached markdown from database for {Url} (blogPostId: {BlogPostId})", url, blogPostId);
        return true;
    }

    private static string GetCacheKey(string url, int blogPostId)
    {
        var input = $"{url}_{blogPostId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}
