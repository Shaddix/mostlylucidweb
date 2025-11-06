namespace Mostlylucid.Markdig.FetchExtension.Events;

public enum ContentUpdateSource
{
    Fetch,
    ExternalUpdate,
    CacheInvalidation
}

public class ContentUpdatedEventArgs : EventArgs
{
    public string Url { get; }
    public int BlogPostId { get; }
    public string Content { get; }
    public ContentUpdateSource Source { get; }
    public DateTime Timestamp { get; }
    public int ContentLength { get; }

    public ContentUpdatedEventArgs(
        string url,
        int blogPostId,
        string content,
        ContentUpdateSource source)
    {
        Url = url;
        BlogPostId = blogPostId;
        Content = content;
        Source = source;
        ContentLength = content?.Length ?? 0;
        Timestamp = DateTime.UtcNow;
    }
}
