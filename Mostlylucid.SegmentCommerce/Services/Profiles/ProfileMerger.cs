using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

public interface IProfileMerger
{
    Task<AnonymousProfileEntity?> MergeAsync(Guid sourceProfileId, Guid targetProfileId, CancellationToken cancellationToken = default);
}

public class ProfileMerger : IProfileMerger
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<ProfileMerger> _logger;

    public ProfileMerger(
        SegmentCommerceDbContext context,
        ILogger<ProfileMerger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AnonymousProfileEntity?> MergeAsync(Guid sourceProfileId, Guid targetProfileId, CancellationToken cancellationToken = default)
    {
        if (sourceProfileId == targetProfileId)
        {
            return await _context.AnonymousProfiles
                .FirstOrDefaultAsync(p => p.Id == targetProfileId, cancellationToken);
        }

        var source = await _context.AnonymousProfiles
            .Include(p => p.InterestScores)
            .FirstOrDefaultAsync(p => p.Id == sourceProfileId, cancellationToken);

        var target = await _context.AnonymousProfiles
            .Include(p => p.InterestScores)
            .FirstOrDefaultAsync(p => p.Id == targetProfileId, cancellationToken);

        if (source == null || target == null)
        {
            return target ?? source;
        }

        target.TotalWeight += source.TotalWeight;
        target.SignalCount += source.SignalCount;
        target.LastSeenAt = DateTime.UtcNow;
        target.UpdatedAt = DateTime.UtcNow;

        foreach (var interest in source.InterestScores)
        {
            if (string.IsNullOrEmpty(interest.Category))
            {
                continue;
            }

            var existing = target.InterestScores.FirstOrDefault(i => i.Category == interest.Category);
            if (existing == null)
            {
                existing = new InterestScoreEntity
                {
                    Profile = target,
                    Category = interest.Category,
                    Score = interest.Score,
                    DecayRate = interest.DecayRate,
                    LastUpdatedAt = interest.LastUpdatedAt
                };

                target.InterestScores.Add(existing);
                _context.InterestScores.Add(existing);
            }
            else
            {
                existing.Score += interest.Score;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }

            _context.InterestScores.Remove(interest);
        }

        var sessions = await _context.SessionProfiles
            .Where(s => s.AnonymousProfileId == source.Id)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.AnonymousProfileId = target.Id;
        }

        var keys = await _context.ProfileKeys
            .Where(k => k.ProfileId == source.Id)
            .ToListAsync(cancellationToken);

        foreach (var key in keys)
        {
            key.ProfileId = target.Id;
        }

        _context.AnonymousProfiles.Remove(source);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Merged profile {Source} into {Target}", sourceProfileId, targetProfileId);
        return target;
    }
}
