using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.Services.BrokenLinks;

namespace Mostlylucid.Middleware;

/// <summary>
/// Middleware that collects external links from HTML responses and replaces known broken links with archive.org URLs
/// </summary>
public partial class BrokenLinkArchiveMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BrokenLinkArchiveMiddleware> _logger;

    // Regex to extract href attributes from anchor tags
    [GeneratedRegex(@"<a[^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HrefRegex();

    // List of internal URL patterns to ignore
    private static readonly string[] InternalPatterns = { "/", "#", "mailto:", "tel:", "javascript:" };

    public BrokenLinkArchiveMiddleware(RequestDelegate next, ILogger<BrokenLinkArchiveMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IBrokenLinkService? brokenLinkService)
    {
        // Only process HTML responses for blog pages
        if (!ShouldProcessRequest(context))
        {
            await _next(context);
            return;
        }

        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        // Only process successful HTML responses
        if (context.Response.StatusCode == 200 &&
            context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var html = await new StreamReader(responseBody).ReadToEndAsync();

            if (brokenLinkService != null)
            {
                try
                {
                    // Extract and register external links
                    var externalLinks = ExtractExternalLinks(html, context.Request);
                    if (externalLinks.Count > 0)
                    {
                        // Fire and forget - don't block the response
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await brokenLinkService.RegisterUrlsAsync(externalLinks);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error registering URLs");
                            }
                        });
                    }

                    // Replace known broken links with archive URLs
                    var mappings = await brokenLinkService.GetBrokenLinkMappingsAsync(context.RequestAborted);
                    if (mappings.Count > 0)
                    {
                        html = ReplaceBrokenLinks(html, mappings);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing broken links in response");
                }
            }

            // Write the potentially modified response
            var modifiedContent = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength = modifiedContent.Length;

            await originalBodyStream.WriteAsync(modifiedContent);
        }
        else
        {
            // For non-HTML responses, just copy the original response
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

        // Process blog pages and home page
        return path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/blog", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ExtractExternalLinks(string html, HttpRequest request)
    {
        var links = new List<string>();
        var host = request.Host.Host;

        var matches = HrefRegex().Matches(html);

        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;

            // Skip internal and special links
            if (string.IsNullOrWhiteSpace(href)) continue;
            if (InternalPatterns.Any(p => href.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;

            // Check if it's an external URL
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                // Skip our own domain
                if (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) continue;
                if (uri.Host.EndsWith(".mostlylucid.net", StringComparison.OrdinalIgnoreCase)) continue;

                // Only track HTTP/HTTPS links
                if (uri.Scheme == "http" || uri.Scheme == "https")
                {
                    links.Add(href);
                }
            }
        }

        return links.Distinct().ToList();
    }

    private string ReplaceBrokenLinks(string html, Dictionary<string, string> mappings)
    {
        var replacementCount = 0;

        foreach (var (originalUrl, archiveUrl) in mappings)
        {
            if (html.Contains(originalUrl))
            {
                // Replace href="original" with href="archive" and add data attribute for transparency
                var originalPattern = $"href=\"{originalUrl}\"";
                var archivePattern = $"href=\"{archiveUrl}\" data-original-url=\"{originalUrl}\" title=\"Original link unavailable - archived version\"";

                var newHtml = html.Replace(originalPattern, archivePattern);

                // Also handle single quotes
                var originalPatternSingle = $"href='{originalUrl}'";
                var archivePatternSingle = $"href='{archiveUrl}' data-original-url='{originalUrl}' title='Original link unavailable - archived version'";

                newHtml = newHtml.Replace(originalPatternSingle, archivePatternSingle);

                if (newHtml != html)
                {
                    replacementCount++;
                    html = newHtml;
                }
            }
        }

        if (replacementCount > 0)
        {
            _logger.LogInformation("Replaced {Count} broken links with archive.org URLs", replacementCount);
        }

        return html;
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
