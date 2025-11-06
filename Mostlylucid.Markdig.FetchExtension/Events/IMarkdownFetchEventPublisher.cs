namespace Mostlylucid.Markdig.FetchExtension.Events;

/// <summary>
/// Publishes events for markdown fetch operations and allows external systems to trigger updates.
/// </summary>
public interface IMarkdownFetchEventPublisher
{
    /// <summary>
    /// Raised when a fetch operation begins.
    /// </summary>
    event EventHandler<FetchBeginningEventArgs>? FetchBeginning;

    /// <summary>
    /// Raised when a fetch operation completes successfully.
    /// </summary>
    event EventHandler<FetchCompletedEventArgs>? FetchCompleted;

    /// <summary>
    /// Raised when a fetch operation fails.
    /// </summary>
    event EventHandler<FetchFailedEventArgs>? FetchFailed;

    /// <summary>
    /// Raised when cached content is updated (from any source).
    /// </summary>
    event EventHandler<ContentUpdatedEventArgs>? ContentUpdated;

    /// <summary>
    /// Publishes a fetch beginning event.
    /// </summary>
    void OnFetchBeginning(string url, int blogPostId, int pollFrequencyHours);

    /// <summary>
    /// Publishes a fetch completed event.
    /// </summary>
    void OnFetchCompleted(string url, int blogPostId, string content, TimeSpan duration, bool wasCached, bool wasStale);

    /// <summary>
    /// Publishes a fetch failed event.
    /// </summary>
    void OnFetchFailed(string url, int blogPostId, Exception exception, bool fallbackToCacheUsed);

    /// <summary>
    /// Invalidates the cache for a specific URL, forcing a fresh fetch on next request.
    /// </summary>
    Task InvalidateCacheAsync(string url, int blogPostId = 0);

    /// <summary>
    /// Updates cached content from an external source (e.g., webhook, file watcher).
    /// Bypasses HTTP fetch and directly updates the cache.
    /// </summary>
    Task UpdateCachedContentAsync(string url, string markdown, int blogPostId = 0);

    /// <summary>
    /// Gets statistics about fetch operations.
    /// </summary>
    FetchStatistics GetStatistics();

    /// <summary>
    /// Removes cached content for a specific URL, deleting it from storage.
    /// The URL will be fetched fresh on next request.
    /// </summary>
    Task RemoveCachedContentAsync(string url, int blogPostId = 0);

    /// <summary>
    /// Stops automatic polling/updates for a specific URL.
    /// The cache will remain but won't be automatically refreshed.
    /// </summary>
    Task StopPollingAsync(string url, int blogPostId = 0);
}

/// <summary>
/// Statistics about fetch operations.
/// </summary>
public class FetchStatistics
{
    public int TotalFetches { get; set; }
    public int SuccessfulFetches { get; set; }
    public int FailedFetches { get; set; }
    public int CachedResponses { get; set; }
    public int ExternalUpdates { get; set; }
    public TimeSpan AverageFetchDuration { get; set; }
    public DateTime? LastFetchTime { get; set; }
}
