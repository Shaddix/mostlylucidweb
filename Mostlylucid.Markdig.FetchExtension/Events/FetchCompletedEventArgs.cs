namespace Mostlylucid.Markdig.FetchExtension.Events;

public class FetchCompletedEventArgs : EventArgs
{
    public string Url { get; }
    public int BlogPostId { get; }
    public string Content { get; }
    public TimeSpan Duration { get; }
    public bool WasCached { get; }
    public bool WasStale { get; }
    public DateTime Timestamp { get; }
    public int ContentLength { get; }

    public FetchCompletedEventArgs(
        string url,
        int blogPostId,
        string content,
        TimeSpan duration,
        bool wasCached,
        bool wasStale)
    {
        Url = url;
        BlogPostId = blogPostId;
        Content = content;
        Duration = duration;
        WasCached = wasCached;
        WasStale = wasStale;
        ContentLength = content?.Length ?? 0;
        Timestamp = DateTime.UtcNow;
    }
}
