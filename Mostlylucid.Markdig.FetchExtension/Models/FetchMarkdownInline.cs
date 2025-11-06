using Markdig.Syntax.Inlines;

namespace Mostlylucid.Markdig.FetchExtension.Models;

/// <summary>
///     Custom inline element to represent a fetch directive
/// </summary>
public class FetchMarkdownInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public int PollFrequencyHours { get; set; }
    public string FetchedContent { get; set; } = string.Empty;
    public bool FetchSuccessful { get; set; }
}