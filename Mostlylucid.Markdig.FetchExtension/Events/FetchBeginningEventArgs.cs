namespace Mostlylucid.Markdig.FetchExtension.Events;

public class FetchBeginningEventArgs : EventArgs
{
    public string Url { get; }
    public int BlogPostId { get; }
    public DateTime Timestamp { get; }
    public int PollFrequencyHours { get; }

    public FetchBeginningEventArgs(string url, int blogPostId, int pollFrequencyHours)
    {
        Url = url;
        BlogPostId = blogPostId;
        PollFrequencyHours = pollFrequencyHours;
        Timestamp = DateTime.UtcNow;
    }
}
