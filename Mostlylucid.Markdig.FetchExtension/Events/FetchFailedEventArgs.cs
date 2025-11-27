namespace Mostlylucid.Markdig.FetchExtension.Events;

public class FetchFailedEventArgs : EventArgs
{
    public string Url { get; }
    public int BlogPostId { get; }
    public Exception Exception { get; }
    public bool FallbackToCacheUsed { get; }
    public DateTime Timestamp { get; }
    public string ErrorMessage { get; }

    public FetchFailedEventArgs(
        string url,
        int blogPostId,
        Exception exception,
        bool fallbackToCacheUsed)
    {
        Url = url;
        BlogPostId = blogPostId;
        Exception = exception;
        FallbackToCacheUsed = fallbackToCacheUsed;
        ErrorMessage = exception?.Message ?? "Unknown error";
        Timestamp = DateTime.UtcNow;
    }
}
