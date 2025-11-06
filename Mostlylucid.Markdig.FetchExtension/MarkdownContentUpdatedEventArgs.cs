namespace Mostlylucid.Markdig.FetchExtension;

public sealed class MarkdownContentUpdatedEventArgs : EventArgs
{
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
}