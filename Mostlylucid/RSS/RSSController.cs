using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Mostlylucid.RSS;

[Microsoft.AspNetCore.Components.Route("rss")]
public class RssController(RSSFeedService rssFeedService) : Controller
{
    private const int CacheDurationSeconds = 3600; // 1 hour, matches TTL in feed

    [HttpGet]
    [ResponseCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = CacheDurationSeconds, VaryByQueryKeys = new[] { "language" })]
    public async Task<IActionResult> Index([FromQuery] string? language = null)
    {
        var rssFeed = await rssFeedService.GenerateFeed(language: language);

        // Generate ETag from content hash for cache validation
        var contentBytes = Encoding.UTF8.GetBytes(rssFeed);
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

        return Content(rssFeed, "application/rss+xml", Encoding.UTF8);
    }
}