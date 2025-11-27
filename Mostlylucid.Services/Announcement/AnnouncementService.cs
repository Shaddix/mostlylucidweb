using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared.Entities;

namespace Mostlylucid.Services.Announcement;

public class AnnouncementService : IAnnouncementService
{
    private readonly MostlylucidDbContext _context;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(
        MostlylucidDbContext context,
        ILogger<AnnouncementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AnnouncementEntity?> GetActiveAnnouncementAsync(string language = "en", CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive && a.Language == language)
            .Where(a => a.StartDate == null || a.StartDate <= now)
            .Where(a => a.EndDate == null || a.EndDate >= now)
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<AnnouncementEntity>> GetAllAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AnnouncementEntity?> GetAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default)
    {
        return await _context.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Key == key && a.Language == language, cancellationToken);
    }

    public async Task<AnnouncementEntity> UpsertAnnouncementAsync(
        string key,
        string markdown,
        string language = "en",
        bool isActive = true,
        int priority = 0,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Key == key && a.Language == language, cancellationToken);

        // Render markdown to HTML using the same pipeline as blog posts
        var htmlContent = global::Markdig.Markdown.ToHtml(markdown);

        if (existing != null)
        {
            existing.Markdown = markdown;
            existing.HtmlContent = htmlContent;
            existing.IsActive = isActive;
            existing.Priority = priority;
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            _context.Announcements.Update(existing);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated announcement {Key} for language {Language}", key, language);
            return existing;
        }

        var announcement = new AnnouncementEntity
        {
            Key = key,
            Markdown = markdown,
            HtmlContent = htmlContent,
            Language = language,
            IsActive = isActive,
            Priority = priority,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created announcement {Key} for language {Language}", key, language);
        return announcement;
    }

    public async Task<bool> DeleteAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Key == key && a.Language == language, cancellationToken);

        if (announcement == null)
        {
            return false;
        }

        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted announcement {Key} for language {Language}", key, language);
        return true;
    }

    public async Task<bool> DeactivateAnnouncementAsync(string key, string language = "en", CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Key == key && a.Language == language, cancellationToken);

        if (announcement == null)
        {
            return false;
        }

        announcement.IsActive = false;
        announcement.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated announcement {Key} for language {Language}", key, language);
        return true;
    }
}
