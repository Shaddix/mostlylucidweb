using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        var brokenLinks = await brokenLinkService.GetBrokenLinksNeedingArchiveAsync(BatchSize, cancellationToken);
        _logger.LogInformation("Found {Count} broken links needing archive.org lookup", brokenLinks.Count);

        foreach (var link in brokenLinks)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var archiveUrl = await GetArchiveUrlAsync(link.OriginalUrl, cancellationToken);
                await brokenLinkService.UpdateArchiveUrlAsync(link.Id, archiveUrl, cancellationToken);

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

    private async Task<string?> GetArchiveUrlAsync(string originalUrl, CancellationToken cancellationToken)
    {
        // Use Wayback Machine Availability API
        var apiUrl = $"https://archive.org/wayback/available?url={Uri.EscapeDataString(originalUrl)}";

        try
        {
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            if (root.TryGetProperty("archived_snapshots", out var snapshots) &&
                snapshots.TryGetProperty("closest", out var closest) &&
                closest.TryGetProperty("available", out var available) &&
                available.GetBoolean() &&
                closest.TryGetProperty("url", out var urlElement))
            {
                return urlElement.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get archive.org URL for {Url}", originalUrl);
            return null;
        }
    }
}
