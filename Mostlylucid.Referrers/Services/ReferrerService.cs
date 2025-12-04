using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.BotDetection.Services;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.Referrers.Config;
using Mostlylucid.Referrers.Models;

namespace Mostlylucid.Referrers.Services;

/// <summary>
/// Service for tracking and filtering blog post referrers
/// </summary>
public class ReferrerService : IReferrerService
{
    private readonly ILogger<ReferrerService> _logger;
    private readonly ReferrerConfig _config;
    private readonly IMemoryCache _cache;
    private readonly IBotDetectionService _botDetectionService;

    // In-memory storage for referrers (in production, this would be a database)
    private static readonly Dictionary<string, List<Referrer>> _referrerStore = new();
    private static readonly object _lock = new();

    private const string CacheKeyPrefix = "referrers_";

    public ReferrerService(
        ILogger<ReferrerService> logger,
        IOptions<ReferrerConfig> config,
        IMemoryCache cache,
        IBotDetectionService botDetectionService)
    {
        _logger = logger;
        _config = config.Value;
        _cache = cache;
        _botDetectionService = botDetectionService;
    }

    public async Task<bool> RecordReferrerAsync(string postSlug, HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            return false;
        }

        var referrer = httpContext.Request.Headers.Referer.ToString();

        if (string.IsNullOrWhiteSpace(referrer))
        {
            return false;
        }

        // Check if referrer is excluded
        if (IsExcludedReferrer(referrer))
        {
            _logger.LogDebug("Referrer {Referrer} is excluded", referrer);
            return false;
        }

        // Check for bots using the bot detection service
        var botResult = await _botDetectionService.DetectAsync(httpContext);

        if (botResult.IsBot)
        {
            _logger.LogDebug("Referrer {Referrer} detected as bot: {BotType} ({BotName})",
                referrer, botResult.BotType, botResult.BotName);
            return false;
        }

        // Record the legitimate referrer
        var referrerModel = CreateReferrerModel(referrer);

        lock (_lock)
        {
            if (!_referrerStore.TryGetValue(postSlug, out var referrers))
            {
                referrers = [];
                _referrerStore[postSlug] = referrers;
            }

            var existing = referrers.FirstOrDefault(r =>
                r.Domain.Equals(referrerModel.Domain, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.HitCount++;
                existing.LastSeen = DateTime.UtcNow;
                if (string.IsNullOrEmpty(existing.Url) && !string.IsNullOrEmpty(referrerModel.Url))
                {
                    existing.Url = referrerModel.Url;
                }
            }
            else
            {
                referrers.Add(referrerModel);
            }
        }

        // Invalidate cache
        _cache.Remove($"{CacheKeyPrefix}{postSlug}");

        _logger.LogInformation("Recorded referrer {Referrer} for post {PostSlug}", referrer, postSlug);
        return true;
    }

    public Task<PostReferrers> GetReferrersForPostAsync(string postSlug, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{postSlug}";

        if (_cache.TryGetValue(cacheKey, out PostReferrers? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        List<Referrer> referrers;
        lock (_lock)
        {
            referrers = _referrerStore.TryGetValue(postSlug, out var stored)
                ? stored
                    .Where(r => r.HitCount >= _config.MinHitsToDisplay)
                    .OrderByDescending(r => r.HitCount)
                    .Take(_config.MaxReferrersPerPost)
                    .ToList()
                : [];
        }

        var result = new PostReferrers
        {
            PostSlug = postSlug,
            Referrers = referrers,
            LastUpdated = DateTime.UtcNow
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_config.CacheDurationMinutes));

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Referrer>> GetTopReferrersAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}top_{limit}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Referrer>? cached) && cached != null)
        {
            return Task.FromResult(cached);
        }

        List<Referrer> aggregated;
        lock (_lock)
        {
            // Aggregate referrers across all posts by domain
            aggregated = _referrerStore.Values
                .SelectMany(r => r)
                .GroupBy(r => r.Domain, StringComparer.OrdinalIgnoreCase)
                .Select(g => new Referrer
                {
                    Domain = g.Key,
                    DisplayName = g.First().DisplayName,
                    Url = g.OrderByDescending(r => r.HitCount).First().Url,
                    HitCount = g.Sum(r => r.HitCount),
                    FirstSeen = g.Min(r => r.FirstSeen),
                    LastSeen = g.Max(r => r.LastSeen),
                    IsVerified = true
                })
                .Where(r => r.HitCount >= _config.MinHitsToDisplay)
                .OrderByDescending(r => r.HitCount)
                .Take(limit)
                .ToList();
        }

        _cache.Set(cacheKey, (IReadOnlyList<Referrer>)aggregated, TimeSpan.FromMinutes(_config.CacheDurationMinutes));

        return Task.FromResult<IReadOnlyList<Referrer>>(aggregated);
    }

    public bool IsExcludedReferrer(string referrerUrl)
    {
        if (string.IsNullOrWhiteSpace(referrerUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(referrerUrl, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var host = uri.Host.ToLowerInvariant();

        // Remove www. prefix for comparison
        if (host.StartsWith("www."))
        {
            host = host[4..];
        }

        return _config.ExcludedDomains.Any(excluded =>
            host.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{excluded}", StringComparison.OrdinalIgnoreCase));
    }

    private static Referrer CreateReferrerModel(string referrerUrl)
    {
        Uri.TryCreate(referrerUrl, UriKind.Absolute, out var uri);

        var domain = uri?.Host ?? referrerUrl;
        if (domain.StartsWith("www."))
        {
            domain = domain[4..];
        }

        return new Referrer
        {
            Url = referrerUrl,
            Domain = domain,
            DisplayName = CreateDisplayName(domain),
            HitCount = 1,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            IsVerified = true
        };
    }

    private static string CreateDisplayName(string domain)
    {
        // Create a friendly display name from domain
        // e.g., "alvinashcraft.com" -> "Alvin Ashcraft"
        // For now, just capitalize the domain without TLD
        var parts = domain.Split('.');
        if (parts.Length >= 2)
        {
            var name = parts[0];
            // Insert spaces before capital letters if camelCase
            var spaced = string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
            return char.ToUpper(spaced[0]) + spaced[1..];
        }
        return domain;
    }
}
