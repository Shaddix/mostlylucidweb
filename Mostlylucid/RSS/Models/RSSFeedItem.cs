using Mostlylucid.Helpers;
using Mostlylucid.Shared.Helpers;

namespace Mostlylucid.RSS.Models;

public class RssFeedItem
{
    private const int SummaryMaxLength = 500;

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime PubDate { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string Author { get; set; } = "Scott Galloway";

    public string Guid => Slug.ToGuid();

    /// <summary>
    /// Gets a truncated summary suitable for RSS description (max 500 chars)
    /// </summary>
    public string Description
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Summary))
                return Title;

            if (Summary.Length <= SummaryMaxLength)
                return Summary;

            // Truncate at word boundary
            var truncated = Summary[..SummaryMaxLength];
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > SummaryMaxLength - 50)
                truncated = truncated[..lastSpace];

            return truncated + "...";
        }
    }
}