using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Mostlylucid.RSS;

public class FeedController(RSSFeedService feedService) : Controller
{
    private const int CacheDurationSeconds = 3600; // 1 hour

    /// <summary>
    /// RSS 2.0 Feed
    /// </summary>
    [HttpGet("/rss")]
    [ResponseCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" })]
    public async Task<IActionResult> Rss([FromQuery] string? language = null)
    {
        var feed = await feedService.GenerateFeed(language: language);
        return CachedFeedResult(feed, "application/rss+xml");
    }

    /// <summary>
    /// Atom 1.0 Feed
    /// </summary>
    [HttpGet("/atom")]
    [ResponseCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" })]
    public async Task<IActionResult> Atom([FromQuery] string? language = null)
    {
        var feed = await feedService.GenerateAtomFeed(language: language);
        return CachedFeedResult(feed, "application/atom+xml");
    }

    /// <summary>
    /// Generic /feed endpoint - returns Atom by default (more modern)
    /// </summary>
    [HttpGet("/feed")]
    [ResponseCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language", "format" }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language", "format" })]
    public async Task<IActionResult> Feed([FromQuery] string? language = null, [FromQuery] string? format = null)
    {
        // Default to Atom, but allow ?format=rss
        if (string.Equals(format, "rss", StringComparison.OrdinalIgnoreCase))
        {
            return await Rss(language);
        }
        return await Atom(language);
    }

    private IActionResult CachedFeedResult(string feedContent, string contentType)
    {
        // Generate ETag from content hash for cache validation
        var contentBytes = Encoding.UTF8.GetBytes(feedContent);
        var hashBytes = MD5.HashData(contentBytes);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexString(hashBytes)}\"");

        // Check If-None-Match header for conditional GET
        var requestEtag = Request.Headers.IfNoneMatch.FirstOrDefault();
        if (!string.IsNullOrEmpty(requestEtag) && requestEtag == etag.ToString())
        {
            return StatusCode(304); // Not Modified
        }

        // Set caching headers
        Response.Headers.ETag = etag.ToString();
        Response.Headers.CacheControl = $"public, max-age={CacheDurationSeconds}";

        return Content(feedContent, contentType, Encoding.UTF8);
    }
}