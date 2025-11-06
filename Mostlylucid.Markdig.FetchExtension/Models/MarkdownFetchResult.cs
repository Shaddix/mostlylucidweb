namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
///     Result of fetching markdown
/// </summary>
public class MarkdownFetchResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the content was last retrieved from the source
    /// </summary>
    public DateTime? LastRetrieved { get; set; }

    /// <summary>
    /// Whether this content came from cache
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// Whether the cached content is stale (past poll frequency)
    /// </summary>
    public bool IsStale { get; set; }

    /// <summary>
    /// The source URL
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Poll frequency in hours
    /// </summary>
    public int PollFrequencyHours { get; set; }
}