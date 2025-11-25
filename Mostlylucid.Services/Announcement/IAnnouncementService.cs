using Mostlylucid.Shared.Entities;

namespace Mostlylucid.Services.Announcement;

public interface IAnnouncementService
{
    /// <summary>
    /// Gets the active announcement for a given language
    /// </summary>
    Task<AnnouncementEntity?> GetActiveAnnouncementAsync(string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all announcements
    /// </summary>
    Task<List<AnnouncementEntity>> GetAllAnnouncementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an announcement by key and language
    /// </summary>
    Task<AnnouncementEntity?> GetAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates an announcement
    /// </summary>
    Task<AnnouncementEntity> UpsertAnnouncementAsync(string key, string markdown, string language = "en", bool isActive = true, int priority = 0, DateTimeOffset? startDate = null, DateTimeOffset? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an announcement
    /// </summary>
    Task<bool> DeleteAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates an announcement
    /// </summary>
    Task<bool> DeactivateAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default);
}
