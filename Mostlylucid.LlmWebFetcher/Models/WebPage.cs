namespace Mostlylucid.LlmWebFetcher.Models;

/// <summary>
/// Represents a fetched web page with metadata.
/// </summary>
public class WebPage
{
    /// <summary>
    /// The final URL after any redirects.
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// The raw HTML content of the page.
    /// </summary>
    public string Html { get; set; } = "";
    
    /// <summary>
    /// HTTP status code of the response.
    /// </summary>
    public int StatusCode { get; set; }
    
    /// <summary>
    /// Content-Type header value.
    /// </summary>
    public string ContentType { get; set; } = "";
    
    /// <summary>
    /// Time taken to fetch the page.
    /// </summary>
    public TimeSpan FetchTime { get; set; }
}
