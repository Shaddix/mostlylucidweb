namespace Mostlylucid.Markdig.FetchExtension.Services;

using Mostlylucid.Markdig.FetchExtension.Models;

/// <summary>
///     Service interface for fetching remote markdown
/// </summary>
public interface IMarkdownFetchService
{
    Task<MarkdownFetchResult> FetchMarkdownAsync(string url, int pollFrequencyHours, int blogPostId);

    /// <summary>
    /// Removes cached markdown for a specific URL.
    /// Returns true if content was removed, false if it didn't exist.
    /// </summary>
    Task<bool> RemoveCachedMarkdownAsync(string url, int blogPostId = 0);
}