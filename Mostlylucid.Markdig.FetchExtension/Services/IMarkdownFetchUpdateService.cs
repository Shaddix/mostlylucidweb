namespace Mostlylucid.Markdig.FetchExtension.Services;

using Mostlylucid.Markdig.FetchExtension.Models;

/// <summary>
///     In-memory polling service API. Implemented by the extension to poll remote markdown
///     and raise an event when detected content changes.
/// </summary>
public interface IMarkdownFetchUpdateService
{
    /// <summary>
    ///     Raised whenever a tracked URL's content changes (by XXHash64 comparison).
    /// </summary>
    event EventHandler<MarkdownContentUpdatedEventArgs>? ContentUpdated;

    /// <summary>
    ///     Start tracking a URL with the given poll frequency in hours.
    ///     Safe to call multiple times; the last poll frequency wins.
    /// </summary>
    void Register(string url, int pollFrequencyHours);

    /// <summary>
    ///     Stop tracking a URL.
    /// </summary>
    void Unregister(string url);

    /// <summary>
    ///     Try to get the current cached content/hash for a URL (if already fetched).
    /// </summary>
    bool TryGet(string url, out string? content, out string? hash, out DateTimeOffset? fetchedAt);
}