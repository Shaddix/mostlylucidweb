using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.BrokenLinks;

namespace Mostlylucid.Middleware;

/// <summary>
/// Middleware that collects external links from HTML responses and replaces known broken links.
/// - External broken links: Uses archive.org URL if available, otherwise removes href (plain text)
/// - Internal broken links: Uses semantic search to find matching content
/// </summary>
public partial class BrokenLinkArchiveMiddleware(RequestDelegate next, ILogger<BrokenLinkArchiveMiddleware> logger, IServiceScopeFactory serviceScopeFactory)
{
    // Regex to extract full anchor tags with href
    [GeneratedRegex(@"<a\s+([^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*)>([^<]*)</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AnchorTagRegex();

    // Regex to extract href attributes from anchor tags (for link collection)
    [GeneratedRegex(@"<a[^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefRegex();

    // List of special URL patterns to skip
    private static readonly string[] SkipPatterns = { "#", "mailto:", "tel:", "javascript:" };

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
                    // Extract and register all links (both internal and external)
                    var allLinks = ExtractAllLinks(html, context.Request);
                    var sourcePageUrl = context.Request.Path.Value;
                    if (allLinks.Count > 0)
                    {
                        // Fire and forget - don't block the response
                        // Must create a new scope since the original scoped services will be disposed
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using var scope = serviceScopeFactory.CreateScope();
                                var scopedBrokenLinkService = scope.ServiceProvider.GetRequiredService<IBrokenLinkService>();
                                await scopedBrokenLinkService.RegisterUrlsAsync(allLinks, sourcePageUrl);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error registering URLs");
                            }
                        });
                    }

                    // Get broken link data
                    var archiveMappings = await brokenLinkService.GetBrokenLinkMappingsAsync(context.RequestAborted);
                    var brokenWithoutArchive = await brokenLinkService.GetBrokenLinksWithoutArchiveAsync(context.RequestAborted);

                    // Replace broken links
                    if (archiveMappings.Count > 0 || brokenWithoutArchive.Count > 0)
                    {
                        html = await ReplaceBrokenLinksAsync(html, archiveMappings, brokenWithoutArchive, semanticSearchService, context.Request, context.RequestAborted);
                    }
                }

                // Write the potentially modified response
                var modifiedContent = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength = modifiedContent.Length;
                await originalBodyStream.WriteAsync(modifiedContent);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing broken links in response");
                // On error, copy original response
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

    private async Task<string> ReplaceBrokenLinksAsync(
        string html,
        Dictionary<string, string> archiveMappings,
        HashSet<string> brokenWithoutArchive,
        ISemanticSearchService? semanticSearchService,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var archiveReplacements = 0;
        var semanticReplacements = 0;
        var removedLinks = 0;
        var host = request.Host.Host;

        // Process archive.org replacements for external links
        foreach (var (originalUrl, archiveUrl) in archiveMappings)
        {
            if (html.Contains(originalUrl))
            {
                // Use DaisyUI tooltip to inform user about the archive.org replacement
                var tooltipText = $"Original link ({originalUrl}) is dead - archive.org version used";
                var originalPattern = $"href=\"{originalUrl}\"";
                var archivePattern = $"href=\"{archiveUrl}\" class=\"tooltip tooltip-warning\" data-tip=\"{tooltipText}\" data-original-url=\"{originalUrl}\"";

                var newHtml = html.Replace(originalPattern, archivePattern);

                // Also handle single quotes
                var originalPatternSingle = $"href='{originalUrl}'";
                var archivePatternSingle = $"href='{archiveUrl}' class='tooltip tooltip-warning' data-tip='{tooltipText}' data-original-url='{originalUrl}'";

                newHtml = newHtml.Replace(originalPatternSingle, archivePatternSingle);

                if (newHtml != html)
                {
                    archiveReplacements++;
                    html = newHtml;
                }
            }
        }

        // Process broken links without archive
        foreach (var brokenUrl in brokenWithoutArchive)
        {
            if (!html.Contains(brokenUrl)) continue;

            var isInternal = IsInternalUrl(brokenUrl, host);

            if (isInternal && semanticSearchService != null)
            {
                // Try semantic search for internal broken links
                var replacement = await TryFindSemanticReplacementAsync(brokenUrl, semanticSearchService, request, cancellationToken);
                if (replacement != null)
                {
                    html = ReplaceHref(html, brokenUrl, replacement);
                    semanticReplacements++;
                    continue;
                }
            }

            // Remove href for broken links without replacement (convert to plain text)
            html = RemoveHref(html, brokenUrl);
            removedLinks++;
        }

        if (archiveReplacements > 0 || semanticReplacements > 0 || removedLinks > 0)
        {
            logger.LogInformation(
                "Processed broken links: {Archive} archive replacements, {Semantic} semantic replacements, {Removed} removed",
                archiveReplacements, semanticReplacements, removedLinks);
        }

        return html;
    }

    private static bool IsInternalUrl(string url, string host)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".mostlylucid.net", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> TryFindSemanticReplacementAsync(
        string brokenUrl,
        ISemanticSearchService semanticSearchService,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract search terms from the URL path
            if (!Uri.TryCreate(brokenUrl, UriKind.Absolute, out var uri)) return null;

            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0) return null;

            // Use the last path segment as search query (usually the slug)
            var searchTerm = pathSegments[^1].Replace("-", " ").Replace("_", " ");

            var results = await semanticSearchService.SearchAsync(searchTerm, 1, cancellationToken);
            if (results.Count > 0 && results[0].Score > 0.5) // Only use if confidence is reasonable
            {
                var baseUri = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? (request.Scheme == "https" ? 443 : 80));
                return new Uri(baseUri.Uri, $"/blog/{results[0].Slug}").ToString();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to find semantic replacement for {Url}", brokenUrl);
        }

        return null;
    }

    private static string ReplaceHref(string html, string oldUrl, string newUrl)
    {
        // Use DaisyUI tooltip for semantic search replacements
        var tooltipText = $"Original link ({oldUrl}) was broken - similar content found";
        // Replace double-quoted href
        html = html.Replace($"href=\"{oldUrl}\"", $"href=\"{newUrl}\" class=\"tooltip tooltip-info\" data-tip=\"{tooltipText}\" data-original-url=\"{oldUrl}\"");
        // Replace single-quoted href
        html = html.Replace($"href='{oldUrl}'", $"href='{newUrl}' class='tooltip tooltip-info' data-tip='{tooltipText}' data-original-url='{oldUrl}'");
        return html;
    }

    private static string RemoveHref(string html, string brokenUrl)
    {
        // Match anchor tags with the broken URL and convert to span with DaisyUI tooltip
        var tooltipText = $"Link no longer available: {brokenUrl}";
        var pattern = $@"<a\s+[^>]*href\s*=\s*[""']{Regex.Escape(brokenUrl)}[""'][^>]*>([^<]*)</a>";
        var replacement = $"<span class=\"broken-link tooltip tooltip-error\" data-tip=\"{tooltipText}\">$1</span>";
        return Regex.Replace(html, pattern, replacement, RegexOptions.IgnoreCase);
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
