using Mostlylucid.Shared.Entities;

namespace Mostlylucid.Services.BrokenLinks;

/// <summary>
/// Service for managing broken links and their archive.org replacements
/// </summary>
public interface IBrokenLinkService
{
    /// <summary>
    /// Register a collection of URLs discovered in content for tracking
    /// </summary>
    Task RegisterUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all known broken links that have archive.org replacements
    /// </summary>
    Task<Dictionary<string, string>> GetBrokenLinkMappingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get links that need to be checked for validity
    /// </summary>
    Task<List<BrokenLinkEntity>> GetLinksToCheckAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update link status after checking
    /// </summary>
    Task UpdateLinkStatusAsync(int linkId, int statusCode, bool isBroken, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the archive URL for a broken link
    /// </summary>
    Task UpdateArchiveUrlAsync(int linkId, string? archiveUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get broken links that need archive.org lookup
    /// </summary>
    Task<List<BrokenLinkEntity>> GetBrokenLinksNeedingArchiveAsync(int batchSize, CancellationToken cancellationToken = default);
}
