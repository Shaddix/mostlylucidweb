namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Service interface for fetching remote markdown
/// </summary>
public interface IMarkdownFetchService
{
    Task<MarkdownFetchResult> FetchMarkdownAsync(string url, int pollFrequencyHours, int blogPostId);
}