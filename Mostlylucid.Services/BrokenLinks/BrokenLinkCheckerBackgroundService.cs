using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;

namespace Mostlylucid.Services.BrokenLinks;

/// <summary>
/// Background service that periodically checks links for validity and fetches archive.org URLs
/// </summary>
public class BrokenLinkCheckerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BrokenLinkCheckerBackgroundService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private const int BatchSize = 20;

    public BrokenLinkCheckerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BrokenLinkCheckerBackgroundService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("BrokenLinkChecker");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Broken Link Checker Background Service started");

        // Wait for app to fully initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckLinksAsync(stoppingToken);
                await FetchArchiveUrlsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Broken Link Checker Background Service");
            }

            _logger.LogInformation("Broken Link Checker sleeping for {Interval}", _checkInterval);
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Broken Link Checker Background Service stopped");
    }

    private async Task CheckLinksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting link validity check");

        using var scope = _serviceProvider.CreateScope();
        var brokenLinkService = scope.ServiceProvider.GetRequiredService<IBrokenLinkService>();

        var linksToCheck = await brokenLinkService.GetLinksToCheckAsync(BatchSize, cancellationToken);
        _logger.LogInformation("Found {Count} links to check", linksToCheck.Count);

        foreach (var link in linksToCheck)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var (statusCode, isBroken, error) = await CheckUrlAsync(link.OriginalUrl, cancellationToken);
                await brokenLinkService.UpdateLinkStatusAsync(link.Id, statusCode, isBroken, error, cancellationToken);

                if (isBroken)
                {
                    _logger.LogWarning("Link is broken: {Url} (Status: {StatusCode})", link.OriginalUrl, statusCode);
                }

                // Small delay to be respectful to servers
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking link: {Url}", link.OriginalUrl);
                await brokenLinkService.UpdateLinkStatusAsync(link.Id, 0, true, ex.Message, cancellationToken);
            }
        }
    }

    private async Task<(int statusCode, bool isBroken, string? error)> CheckUrlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MostlylucidBot/1.0; +https://www.mostlylucid.net)");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var statusCode = (int)response.StatusCode;
            var isBroken = response.StatusCode == HttpStatusCode.NotFound ||
                           response.StatusCode == HttpStatusCode.Gone ||
                           statusCode >= 500;

            return (statusCode, isBroken, null);
        }
        catch (HttpRequestException ex)
        {
            return (0, true, ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return (0, true, "Request timed out");
        }
    }

    private async Task FetchArchiveUrlsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting archive.org URL fetch");

        using var scope = _serviceProvider.CreateScope();
        var brokenLinkService = scope.ServiceProvider.GetRequiredService<IBrokenLinkService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MostlylucidDbContext>();

        var brokenLinks = await brokenLinkService.GetBrokenLinksNeedingArchiveAsync(BatchSize, cancellationToken);
        _logger.LogInformation("Found {Count} broken links needing archive.org lookup", brokenLinks.Count);

        foreach (var link in brokenLinks)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Look up the blog post's publish date based on source page URL
                DateTime? publishDate = null;
                if (!string.IsNullOrEmpty(link.SourcePageUrl))
                {
                    publishDate = await GetBlogPostPublishDateAsync(dbContext, link.SourcePageUrl, cancellationToken);
                }

                var archiveUrl = await GetArchiveUrlAsync(link.OriginalUrl, publishDate, cancellationToken);
                await brokenLinkService.UpdateArchiveUrlAsync(link.Id, archiveUrl, cancellationToken);

                if (archiveUrl != null && publishDate.HasValue)
                {
                    _logger.LogInformation("Found archive.org snapshot at or before {PublishDate} for {Url}",
                        publishDate.Value.ToString("yyyy-MM-dd"), link.OriginalUrl);
                }

                // Respect archive.org rate limits
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching archive.org URL for: {Url}", link.OriginalUrl);
                // Mark as checked to avoid infinite retries
                await brokenLinkService.UpdateArchiveUrlAsync(link.Id, null, cancellationToken);
            }
        }
    }

    private static async Task<DateTime?> GetBlogPostPublishDateAsync(MostlylucidDbContext dbContext, string sourcePageUrl, CancellationToken cancellationToken)
    {
        // Extract slug from URL like /blog/my-post-slug or /blog/my-post-slug/en
        var match = Regex.Match(sourcePageUrl, @"/blog/([^/]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var slug = match.Groups[1].Value;

        // Look up the blog post's publish date
        var publishDate = await dbContext.BlogPosts
            .Where(bp => bp.Slug == slug)
            .Select(bp => bp.PublishedDate)
            .FirstOrDefaultAsync(cancellationToken);

        return publishDate == default ? null : publishDate.DateTime;
    }

    private async Task<string?> GetArchiveUrlAsync(string originalUrl, DateTime? beforeDate, CancellationToken cancellationToken)
    {
        // Use CDX API to find snapshots at or before the publish date
        // This ensures we get content that was current when the blog post was written
        var queryParams = new List<string>
        {
            $"url={Uri.EscapeDataString(originalUrl)}",
            "output=json",
            "fl=timestamp,original,statuscode",
            "filter=statuscode:200",  // Only successful responses
            "limit=1"  // We only need the closest one
        };

        // If we have a publish date, filter to snapshots at or before that date
        if (beforeDate.HasValue)
        {
            queryParams.Add($"to={beforeDate.Value:yyyyMMdd}");
            queryParams.Add("sort=closest");  // Get the closest to the end date
            queryParams.Add($"closest={beforeDate.Value:yyyyMMdd}");  // Find closest to this date
        }

        var apiUrl = $"https://web.archive.org/cdx/search/cdx?{string.Join("&", queryParams)}";

        try
        {
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;

            // Parse CDX JSON response
            var jsonArray = JsonSerializer.Deserialize<string[][]>(json);
            if (jsonArray == null || jsonArray.Length < 2) return null;  // Need header + at least one record

            // Skip header row (first row), get first data row
            var record = jsonArray[1];
            if (record.Length < 2) return null;

            var timestamp = record[0];
            var original = record[1];

            // Build Wayback URL
            return $"https://web.archive.org/web/{timestamp}/{original}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get archive.org URL for {Url}", originalUrl);
            return null;
        }
    }
}
