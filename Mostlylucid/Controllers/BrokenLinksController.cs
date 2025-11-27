using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.BrokenLinks;

namespace Mostlylucid.Controllers;

/// <summary>
/// API controller for broken link mappings used by client-side JavaScript
/// </summary>
[Route("api/brokenlinks")]
[ApiController]
public class BrokenLinksController : ControllerBase
{
    private readonly IBrokenLinkService _brokenLinkService;
    private readonly MostlylucidDbContext _dbContext;
    private readonly ILogger<BrokenLinksController> _logger;

    public BrokenLinksController(
        IBrokenLinkService brokenLinkService,
        MostlylucidDbContext dbContext,
        ILogger<BrokenLinksController> logger)
    {
        _brokenLinkService = brokenLinkService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all broken link to archive URL mappings for client-side link replacement
    /// </summary>
    [HttpGet("mappings")]
    [OutputCache(Duration = 300)] // Cache for 5 minutes
    public async Task<IActionResult> GetMappings(CancellationToken cancellationToken)
    {
        try
        {
            var mappings = await _brokenLinkService.GetBrokenLinkMappingsAsync(cancellationToken);
            return Ok(mappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching broken link mappings");
            return StatusCode(500, "Error fetching broken link mappings");
        }
    }

    /// <summary>
    /// Get diagnostic stats about broken link checking status
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var totalLinks = await _dbContext.BrokenLinks.CountAsync(cancellationToken);
            var checkedLinks = await _dbContext.BrokenLinks.CountAsync(x => x.LastCheckedAt != null, cancellationToken);
            var uncheckedLinks = await _dbContext.BrokenLinks.CountAsync(x => x.LastCheckedAt == null, cancellationToken);
            var brokenLinks = await _dbContext.BrokenLinks.CountAsync(x => x.IsBroken, cancellationToken);
            var archiveChecked = await _dbContext.BrokenLinks.CountAsync(x => x.ArchiveChecked, cancellationToken);
            var withArchiveUrl = await _dbContext.BrokenLinks.CountAsync(x => x.ArchiveUrl != null, cancellationToken);
            var pendingArchiveCheck = await _dbContext.BrokenLinks.CountAsync(x => x.IsBroken && !x.ArchiveChecked, cancellationToken);

            // Get some sample broken links with archive URLs
            var sampleMappings = await _dbContext.BrokenLinks
                .Where(x => x.IsBroken && x.ArchiveUrl != null)
                .Take(10)
                .Select(x => new { x.OriginalUrl, x.ArchiveUrl, x.SourcePageUrl, x.LastCheckedAt })
                .ToListAsync(cancellationToken);

            // Get some sample unchecked links
            var sampleUnchecked = await _dbContext.BrokenLinks
                .Where(x => x.LastCheckedAt == null)
                .Take(10)
                .Select(x => new { x.OriginalUrl, x.SourcePageUrl, x.DiscoveredAt })
                .ToListAsync(cancellationToken);

            // Get some sample broken links pending archive check
            var samplePendingArchive = await _dbContext.BrokenLinks
                .Where(x => x.IsBroken && !x.ArchiveChecked)
                .Take(10)
                .Select(x => new { x.OriginalUrl, x.SourcePageUrl, x.LastStatusCode, x.LastError })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                summary = new
                {
                    totalLinks,
                    checkedLinks,
                    uncheckedLinks,
                    brokenLinks,
                    archiveChecked,
                    withArchiveUrl,
                    pendingArchiveCheck
                },
                sampleMappings,
                sampleUnchecked,
                samplePendingArchive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching broken link stats");
            return StatusCode(500, "Error fetching broken link stats");
        }
    }

    /// <summary>
    /// Manually trigger a check cycle (development only)
    /// </summary>
    [HttpPost("trigger-check")]
    public async Task<IActionResult> TriggerCheck(CancellationToken cancellationToken)
    {
        try
        {
            // Get links that need checking
            var linksToCheck = await _brokenLinkService.GetLinksToCheckAsync(10, cancellationToken);
            _logger.LogInformation("Manual trigger: Found {Count} links to check", linksToCheck.Count);

            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var results = new List<object>();

            foreach (var link in linksToCheck)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, link.OriginalUrl);
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MostlylucidBot/1.0)");

                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    var statusCode = (int)response.StatusCode;
                    var isBroken = response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                   response.StatusCode == System.Net.HttpStatusCode.Gone ||
                                   statusCode >= 500;

                    await _brokenLinkService.UpdateLinkStatusAsync(link.Id, statusCode, isBroken, null, cancellationToken);

                    results.Add(new { link.OriginalUrl, statusCode, isBroken });
                }
                catch (Exception ex)
                {
                    await _brokenLinkService.UpdateLinkStatusAsync(link.Id, 0, true, ex.Message, cancellationToken);
                    results.Add(new { link.OriginalUrl, statusCode = 0, isBroken = true, error = ex.Message });
                }
            }

            return Ok(new { checkedCount = linksToCheck.Count, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering manual check");
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    /// Process a specific page URL immediately - discover, check, and lookup archive URLs in one request
    /// </summary>
    [HttpPost("process-page")]
    public async Task<IActionResult> ProcessPage([FromQuery] string pageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageUrl))
        {
            return BadRequest("pageUrl query parameter is required");
        }

        try
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MostlylucidBot/1.0)");

            // Step 1: Fetch the page and extract links
            _logger.LogInformation("Fetching page: {Url}", pageUrl);
            var pageResponse = await httpClient.GetStringAsync(pageUrl, cancellationToken);

            var links = ExtractLinksFromHtml(pageResponse);
            _logger.LogInformation("Found {Count} links on page", links.Count);

            if (links.Count == 0)
            {
                return Ok(new { message = "No links found on page", links = new List<string>() });
            }

            // Step 2: Register all links
            var sourcePagePath = new Uri(pageUrl).AbsolutePath;
            await _brokenLinkService.RegisterUrlsAsync(links, sourcePagePath, cancellationToken);

            // Step 3: Check each link and lookup archive.org for broken ones
            var results = new List<object>();
            foreach (var link in links.Take(50)) // Limit to 50 to avoid timeout
            {
                try
                {
                    // Get or create the link entity
                    var linkEntity = await _dbContext.BrokenLinks
                        .FirstOrDefaultAsync(x => x.OriginalUrl == link, cancellationToken);

                    if (linkEntity == null) continue;

                    // Check if link is broken
                    int statusCode;
                    bool isBroken;
                    string? error = null;

                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Head, link);
                        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MostlylucidBot/1.0)");
                        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                        statusCode = (int)response.StatusCode;
                        isBroken = response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                   response.StatusCode == System.Net.HttpStatusCode.Gone ||
                                   statusCode >= 500;
                    }
                    catch (Exception ex)
                    {
                        statusCode = 0;
                        isBroken = true;
                        error = ex.Message;
                    }

                    await _brokenLinkService.UpdateLinkStatusAsync(linkEntity.Id, statusCode, isBroken, error, cancellationToken);

                    string? archiveUrl = null;
                    if (isBroken)
                    {
                        // Look up archive.org
                        archiveUrl = await LookupArchiveUrl(httpClient, link, cancellationToken);
                        await _brokenLinkService.UpdateArchiveUrlAsync(linkEntity.Id, archiveUrl, cancellationToken);
                    }

                    results.Add(new
                    {
                        url = link,
                        statusCode,
                        isBroken,
                        error,
                        archiveUrl,
                        hasArchive = archiveUrl != null
                    });

                    // Small delay to be nice to servers
                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    results.Add(new { url = link, error = ex.Message });
                }
            }

            var brokenCount = results.Count(r => ((dynamic)r).isBroken == true);
            var archivedCount = results.Count(r => ((dynamic)r).hasArchive == true);

            return Ok(new
            {
                message = $"Processed {results.Count} links. {brokenCount} broken, {archivedCount} have archive.org URLs.",
                pageUrl,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing page: {Url}", pageUrl);
            return StatusCode(500, ex.Message);
        }
    }

    private List<string> ExtractLinksFromHtml(string html)
    {
        var links = new List<string>();
        var regex = new System.Text.RegularExpressions.Regex(
            @"<a[^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in regex.Matches(html))
        {
            var href = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(href)) continue;
            if (href.StartsWith("#") || href.StartsWith("mailto:") || href.StartsWith("javascript:")) continue;

            // Only track absolute HTTP/HTTPS URLs (external links)
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https") &&
                !uri.Host.Contains("localhost") &&
                !uri.Host.Contains("mostlylucid"))
            {
                links.Add(href);
            }
        }

        return links.Distinct().ToList();
    }

    private async Task<string?> LookupArchiveUrl(HttpClient httpClient, string originalUrl, CancellationToken cancellationToken)
    {
        try
        {
            var apiUrl = $"https://web.archive.org/cdx/search/cdx?url={Uri.EscapeDataString(originalUrl)}&output=json&fl=timestamp,original,statuscode&filter=statuscode:200&limit=1";

            using var response = await httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;

            var jsonArray = System.Text.Json.JsonSerializer.Deserialize<string[][]>(json);
            if (jsonArray == null || jsonArray.Length < 2 || jsonArray[1].Length < 2) return null;

            var timestamp = jsonArray[1][0];
            var original = jsonArray[1][1];
            return $"https://web.archive.org/web/{timestamp}/{original}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Manually trigger archive.org lookup for broken links (development only)
    /// </summary>
    [HttpPost("trigger-archive-lookup")]
    public async Task<IActionResult> TriggerArchiveLookup(CancellationToken cancellationToken)
    {
        try
        {
            var brokenLinks = await _brokenLinkService.GetBrokenLinksNeedingArchiveAsync(10, cancellationToken);
            _logger.LogInformation("Manual trigger: Found {Count} broken links needing archive lookup", brokenLinks.Count);

            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var results = new List<object>();

            foreach (var link in brokenLinks)
            {
                try
                {
                    // Look up archive.org URL
                    var apiUrl = $"https://web.archive.org/cdx/search/cdx?url={Uri.EscapeDataString(link.OriginalUrl)}&output=json&fl=timestamp,original,statuscode&filter=statuscode:200&limit=1";

                    using var response = await httpClient.GetAsync(apiUrl, cancellationToken);
                    string? archiveUrl = null;

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (!string.IsNullOrWhiteSpace(json) && json != "[]")
                        {
                            var jsonArray = System.Text.Json.JsonSerializer.Deserialize<string[][]>(json);
                            if (jsonArray != null && jsonArray.Length >= 2 && jsonArray[1].Length >= 2)
                            {
                                var timestamp = jsonArray[1][0];
                                var original = jsonArray[1][1];
                                archiveUrl = $"https://web.archive.org/web/{timestamp}/{original}";
                            }
                        }
                    }

                    await _brokenLinkService.UpdateArchiveUrlAsync(link.Id, archiveUrl, cancellationToken);
                    results.Add(new { link.OriginalUrl, archiveUrl, found = archiveUrl != null });

                    // Respect rate limits
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    await _brokenLinkService.UpdateArchiveUrlAsync(link.Id, null, cancellationToken);
                    results.Add(new { link.OriginalUrl, archiveUrl = (string?)null, found = false, error = ex.Message });
                }
            }

            return Ok(new { lookupCount = brokenLinks.Count, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering archive lookup");
            return StatusCode(500, ex.Message);
        }
    }
}
