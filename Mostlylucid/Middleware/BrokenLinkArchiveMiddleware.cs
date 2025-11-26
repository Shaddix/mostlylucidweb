using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.BrokenLinks;

namespace Mostlylucid.Middleware;

/// <summary>
/// Middleware that collects external links from HTML responses and replaces known broken links.
/// - External broken links: Uses archive.org URL if available, otherwise removes href (plain text)
/// - Internal broken links: Uses semantic search to find matching content
/// </summary>
public partial class BrokenLinkArchiveMiddleware(
    RequestDelegate next,
    ILogger<BrokenLinkArchiveMiddleware> logger,
    IServiceScopeFactory serviceScopeFactory,
    IMemoryCache memoryCache)
{
    private const string BrokenLinkMappingsCacheKey = "BrokenLinkMappings";
    private const string BrokenLinksWithoutArchiveCacheKey = "BrokenLinksWithoutArchive";
    private static readonly TimeSpan MappingsCacheDuration = TimeSpan.FromMinutes(5);

    // Regex to extract full anchor tags with href
    [GeneratedRegex(@"<a\s+([^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*)>([^<]*)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorTagRegex();

    // Regex to extract href attributes from anchor tags (for link collection)
    [GeneratedRegex(@"<a[^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    // Regex for removing broken link anchors
    [GeneratedRegex(@"<a\s+[^>]*href\s*=\s*[""'](?<url>[^""']+)[""'][^>]*>(?<text>[^<]*)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex BrokenLinkAnchorRegex();

    // List of special URL patterns to skip
    private static readonly string[] SkipPatterns = ["#", "mailto:", "tel:", "javascript:"];

    /// <summary>
    /// Get broken link mappings from cache, refreshing if needed
    /// </summary>
    private async Task<Dictionary<string, string>> GetCachedBrokenLinkMappingsAsync(
        IBrokenLinkService brokenLinkService, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(BrokenLinkMappingsCacheKey, out Dictionary<string, string>? cached) && cached != null)
        {
            return cached;
        }

        var mappings = await brokenLinkService.GetBrokenLinkMappingsAsync(cancellationToken);
        memoryCache.Set(BrokenLinkMappingsCacheKey, mappings, MappingsCacheDuration);
        return mappings;
    }

    /// <summary>
    /// Get broken links without archive from cache, refreshing if needed
    /// </summary>
    private async Task<HashSet<string>> GetCachedBrokenLinksWithoutArchiveAsync(
        IBrokenLinkService brokenLinkService, CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(BrokenLinksWithoutArchiveCacheKey, out HashSet<string>? cached) && cached != null)
        {
            return cached;
        }

        var links = await brokenLinkService.GetBrokenLinksWithoutArchiveAsync(cancellationToken);
        memoryCache.Set(BrokenLinksWithoutArchiveCacheKey, links, MappingsCacheDuration);
        return links;
    }

    /// <summary>
    /// Invalidate all caches (call when markdown files change or broken links are updated)
    /// </summary>
    public static void InvalidateAllCaches(IMemoryCache cache)
    {
        cache.Remove(BrokenLinkMappingsCacheKey);
        cache.Remove(BrokenLinksWithoutArchiveCacheKey);
        // Note: individual page caches will expire naturally or be invalidated by slug
    }

    /// <summary>
    /// Invalidate broken link caches (call when markdown file changes)
    /// Note: Page-level caching is handled by OutputCache with tag-based eviction
    /// </summary>
    public static void InvalidateLinkCaches(IMemoryCache cache)
    {
        cache.Remove(BrokenLinkMappingsCacheKey);
        cache.Remove(BrokenLinksWithoutArchiveCacheKey);
    }

    public async Task InvokeAsync(HttpContext context, IBrokenLinkService? brokenLinkService, ISemanticSearchService? semanticSearchService)
    {
        // Only process HTML responses for blog pages - be very conservative
        if (!ShouldProcessRequest(context))
        {
            await next(context);
            return;
        }

        // Don't intercept if services aren't available
        if (brokenLinkService == null)
        {
            await next(context);
            return;
        }

        var pageUrl = context.Request.Path.Value ?? "";

        // Note: Page-level caching is handled by OutputCache (before this middleware)
        // This middleware only processes cache misses, and the result is then cached by OutputCache

        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await next(context);
        }
        catch
        {
            // On any exception, restore original stream and rethrow
            context.Response.Body = originalBodyStream;
            throw;
        }

        // Only process successful HTML responses (status 200, text/html content)
        var isSuccessfulHtml = context.Response.StatusCode == 200 &&
                               context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true &&
                               responseBody.Length > 0;

        if (isSuccessfulHtml)
        {
            try
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                var html = await new StreamReader(responseBody).ReadToEndAsync();

                if (!string.IsNullOrEmpty(html))
                {
                    // Extract links and fire-and-forget background processing
                    var allLinks = ExtractAllLinks(html, context.Request);
                    var sourcePageUrl = context.Request.Path.Value;

                    if (allLinks.Count > 0)
                    {
                        // Fire and forget - register, check, and lookup archive URLs in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = serviceScopeFactory.CreateScope();
                                var scopedBrokenLinkService = scope.ServiceProvider.GetRequiredService<IBrokenLinkService>();
                                var dbContext = scope.ServiceProvider.GetRequiredService<MostlylucidDbContext>();
                                var outputCacheStore = scope.ServiceProvider.GetService<IOutputCacheStore>();

                                await scopedBrokenLinkService.RegisterUrlsAsync(allLinks, sourcePageUrl);

                                var externalLinks = allLinks
                                    .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                                  !uri.Host.Contains("localhost") &&
                                                  !uri.Host.Contains("mostlylucid"))
                                    .ToList();

                                if (externalLinks.Count > 0)
                                {
                                    var linksUpdated = await CheckAndProcessLinksAsync(externalLinks, sourcePageUrl, scopedBrokenLinkService, dbContext, logger);

                                    if (linksUpdated)
                                    {
                                        // Invalidate caches so next request picks up changes
                                        memoryCache.Remove(BrokenLinkMappingsCacheKey);
                                        memoryCache.Remove(BrokenLinksWithoutArchiveCacheKey);

                                        // Evict OutputCache so the cached response is refreshed
                                        if (outputCacheStore != null)
                                        {
                                            await outputCacheStore.EvictByTagAsync("blog", CancellationToken.None);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error registering/checking URLs");
                            }
                        });
                    }

                    // Get broken link data from cache (non-blocking for most requests)
                    var archiveMappings = await GetCachedBrokenLinkMappingsAsync(brokenLinkService, context.RequestAborted);
                    var brokenWithoutArchive = await GetCachedBrokenLinksWithoutArchiveAsync(brokenLinkService, context.RequestAborted);

                    // Replace broken links (fast string operations)
                    if (archiveMappings.Count > 0 || brokenWithoutArchive.Count > 0)
                    {
                        html = ReplaceBrokenLinks(html, archiveMappings, brokenWithoutArchive, context.Request.Host.Host);
                    }

                    // Write processed response - OutputCache will cache this result
                    var outputBytes = Encoding.UTF8.GetBytes(html);
                    context.Response.ContentLength = outputBytes.Length;
                    await originalBodyStream.WriteAsync(outputBytes);
                }
                else
                {
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing broken links in response");
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        else
        {
            // For non-HTML/error responses, just copy the original response
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }

        context.Response.Body = originalBodyStream;
    }

    private static bool ShouldProcessRequest(HttpContext context)
    {
        // Only process GET requests
        if (!HttpMethods.IsGet(context.Request.Method)) return false;

        var path = context.Request.Path.Value ?? "";

        // Skip static files and API endpoints
        if (path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_", StringComparison.OrdinalIgnoreCase) ||
            path.Contains('.'))  // Skip any path with file extension
        {
            return false;
        }

        // Process blog pages and home page
        return path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/blog", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ExtractAllLinks(string html, HttpRequest request)
    {
        var links = new List<string>();
        var matches = HrefRegex().Matches(html);

        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;

            // Skip special links
            if (string.IsNullOrWhiteSpace(href)) continue;
            if (SkipPatterns.Any(p => href.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;

            // For absolute URLs, only track HTTP/HTTPS
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == "http" || uri.Scheme == "https")
                {
                    links.Add(href);
                }
            }
            // For relative URLs (internal links), track them too
            else if (href.StartsWith("/"))
            {
                // Convert to absolute URL for tracking
                var baseUri = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? (request.Scheme == "https" ? 443 : 80));
                links.Add(new Uri(baseUri.Uri, href).ToString());
            }
        }

        return links.Distinct().ToList();
    }

    /// <summary>
    /// Fast synchronous replacement of broken links - uses string operations and cached regex
    /// </summary>
    private static string ReplaceBrokenLinks(
        string html,
        Dictionary<string, string> archiveMappings,
        HashSet<string> brokenWithoutArchive,
        string host)
    {
        // Fast path: nothing to do
        if (archiveMappings.Count == 0 && brokenWithoutArchive.Count == 0)
            return html;

        var sb = new StringBuilder(html);

        // Process archive.org replacements for external links (simple string replace)
        foreach (var (originalUrl, archiveUrl) in archiveMappings)
        {
            // Double quotes
            sb.Replace(
                $"href=\"{originalUrl}\"",
                $"href=\"{archiveUrl}\" class=\"archived-link\" data-original=\"{originalUrl}\" title=\"Archived version - original link was broken\"");

            // Single quotes
            sb.Replace(
                $"href='{originalUrl}'",
                $"href='{archiveUrl}' class='archived-link' data-original='{originalUrl}' title='Archived version - original link was broken'");
        }

        // Process broken links without archive - mark them as broken
        foreach (var brokenUrl in brokenWithoutArchive)
        {
            // Skip internal links (they're handled separately)
            if (Uri.TryCreate(brokenUrl, UriKind.Absolute, out var uri) &&
                (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".mostlylucid.net", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Mark external broken links without archive
            sb.Replace(
                $"href=\"{brokenUrl}\"",
                $"href=\"{brokenUrl}\" class=\"broken-link\" title=\"This link may be broken\"");

            sb.Replace(
                $"href='{brokenUrl}'",
                $"href='{brokenUrl}' class='broken-link' title='This link may be broken'");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Check external links and look up archive.org URLs for broken ones.
    /// Returns true if any links were updated.
    /// </summary>
    private static async Task<bool> CheckAndProcessLinksAsync(
        List<string> externalLinks,
        string? sourcePageUrl,
        IBrokenLinkService brokenLinkService,
        MostlylucidDbContext dbContext,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        var anyUpdated = false;
        // Get publication date for archive.org lookup (period-correct archives)
        DateTime? publishDate = null;
        if (!string.IsNullOrEmpty(sourcePageUrl))
        {
            var slugMatch = Regex.Match(sourcePageUrl, @"/blog/([^/]+)", RegexOptions.IgnoreCase);
            if (slugMatch.Success)
            {
                var slug = slugMatch.Groups[1].Value;
                publishDate = await dbContext.BlogPosts
                    .Where(bp => bp.Slug == slug)
                    .Select(bp => bp.PublishedDate.DateTime)
                    .FirstOrDefaultAsync();
            }
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MostlylucidBot/1.0; +https://www.mostlylucid.net)");

        foreach (var url in externalLinks.Take(20)) // Limit to avoid blocking too long
        {
            try
            {
                // Get the link entity
                var linkEntity = await dbContext.BrokenLinks
                    .FirstOrDefaultAsync(x => x.OriginalUrl == url);

                if (linkEntity == null) continue;

                // Skip if already checked recently (within 24 hours)
                if (linkEntity.LastCheckedAt.HasValue &&
                    linkEntity.LastCheckedAt.Value > DateTimeOffset.UtcNow.AddHours(-24))
                {
                    continue;
                }

                // Check if link is broken
                int statusCode;
                bool isBroken;
                string? error = null;

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, url);
                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    statusCode = (int)response.StatusCode;
                    isBroken = response.StatusCode == HttpStatusCode.NotFound ||
                               response.StatusCode == HttpStatusCode.Gone ||
                               statusCode >= 500;
                }
                catch (Exception ex)
                {
                    statusCode = 0;
                    isBroken = true;
                    error = ex.Message;
                }

                await brokenLinkService.UpdateLinkStatusAsync(linkEntity.Id, statusCode, isBroken, error);
                anyUpdated = true;

                // If broken and not yet checked for archive, look up archive.org
                if (isBroken && !linkEntity.ArchiveChecked)
                {
                    var archiveUrl = await GetArchiveUrlAsync(httpClient, url, publishDate, logger);
                    await brokenLinkService.UpdateArchiveUrlAsync(linkEntity.Id, archiveUrl);

                    if (archiveUrl != null)
                    {
                        logger.LogInformation("Found archive.org URL for broken link {Url}: {ArchiveUrl}", url, archiveUrl);
                    }
                }

                // Small delay to be respectful to servers
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking link: {Url}", url);
            }
        }

        return anyUpdated;
    }

    /// <summary>
    /// Look up archive.org URL for a broken link, preferring snapshots from at/before the publication date
    /// </summary>
    private static async Task<string?> GetArchiveUrlAsync(HttpClient httpClient, string originalUrl, DateTime? beforeDate, Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"url={Uri.EscapeDataString(originalUrl)}",
                "output=json",
                "fl=timestamp,original,statuscode",
                "filter=statuscode:200",
                "limit=1"
            };

            // If we have a publication date, get snapshot at or before that date (period-correct)
            if (beforeDate.HasValue)
            {
                queryParams.Add($"to={beforeDate.Value:yyyyMMdd}");
                queryParams.Add("sort=closest");
                queryParams.Add($"closest={beforeDate.Value:yyyyMMdd}");
            }

            var apiUrl = $"https://web.archive.org/cdx/search/cdx?{string.Join("&", queryParams)}";

            using var response = await httpClient.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;

            var jsonArray = JsonSerializer.Deserialize<string[][]>(json);
            if (jsonArray == null || jsonArray.Length < 2 || jsonArray[1].Length < 2) return null;

            var timestamp = jsonArray[1][0];
            var original = jsonArray[1][1];
            return $"https://web.archive.org/web/{timestamp}/{original}";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get archive.org URL for {Url}", originalUrl);
            return null;
        }
    }
}

/// <summary>
/// Extension methods for registering the broken link archive middleware
/// </summary>
public static class BrokenLinkArchiveMiddlewareExtensions
{
    public static IApplicationBuilder UseBrokenLinkArchive(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BrokenLinkArchiveMiddleware>();
    }
}
