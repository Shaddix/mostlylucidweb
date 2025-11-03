namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Result of fetching markdown
/// </summary>
public class MarkdownFetchResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}