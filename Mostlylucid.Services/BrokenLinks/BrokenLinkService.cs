using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared.Entities;

namespace Mostlylucid.Services.BrokenLinks;

/// <summary>
/// Service for managing broken links and their archive.org replacements
/// </summary>
public class BrokenLinkService : IBrokenLinkService
{
    private readonly MostlylucidDbContext _dbContext;
    private readonly ILogger<BrokenLinkService> _logger;

    public BrokenLinkService(MostlylucidDbContext dbContext, ILogger<BrokenLinkService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RegisterUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var urlList = urls.Distinct().ToList();
        if (urlList.Count == 0) return;

        // Get existing URLs
        var existingUrls = await _dbContext.BrokenLinks
            .Where(x => urlList.Contains(x.OriginalUrl))
            .Select(x => x.OriginalUrl)
            .ToListAsync(cancellationToken);

        var newUrls = urlList.Except(existingUrls).ToList();

        if (newUrls.Count == 0) return;

        var entities = newUrls.Select(url => new BrokenLinkEntity
        {
            OriginalUrl = url,
            DiscoveredAt = DateTimeOffset.UtcNow
        }).ToList();

        _dbContext.BrokenLinks.AddRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered {Count} new URLs for broken link tracking", newUrls.Count);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetBrokenLinkMappingsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BrokenLinks
            .Where(x => x.IsBroken && x.ArchiveUrl != null)
            .ToDictionaryAsync(
                x => x.OriginalUrl,
                x => x.ArchiveUrl!,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<BrokenLinkEntity>> GetLinksToCheckAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

        return await _dbContext.BrokenLinks
            .Where(x => x.LastCheckedAt == null || x.LastCheckedAt < cutoff)
            .OrderBy(x => x.LastCheckedAt ?? DateTimeOffset.MinValue)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateLinkStatusAsync(int linkId, int statusCode, bool isBroken, string? error = null, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.BrokenLinks.FindAsync(new object[] { linkId }, cancellationToken);
        if (link == null) return;

        link.LastStatusCode = statusCode;
        link.IsBroken = isBroken;
        link.LastCheckedAt = DateTimeOffset.UtcNow;
        link.LastError = error;

        if (isBroken)
        {
            link.ConsecutiveFailures++;
        }
        else
        {
            link.ConsecutiveFailures = 0;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateArchiveUrlAsync(int linkId, string? archiveUrl, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.BrokenLinks.FindAsync(new object[] { linkId }, cancellationToken);
        if (link == null) return;

        link.ArchiveUrl = archiveUrl;
        link.ArchiveChecked = true;
        link.ArchiveCheckedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(archiveUrl))
        {
            _logger.LogInformation("Found archive.org URL for {OriginalUrl}: {ArchiveUrl}", link.OriginalUrl, archiveUrl);
        }
    }

    /// <inheritdoc />
    public async Task<List<BrokenLinkEntity>> GetBrokenLinksNeedingArchiveAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BrokenLinks
            .Where(x => x.IsBroken && !x.ArchiveChecked)
            .OrderBy(x => x.DiscoveredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
