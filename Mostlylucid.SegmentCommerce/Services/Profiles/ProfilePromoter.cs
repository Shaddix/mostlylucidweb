using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

public interface IProfilePromoter
{
    Task<AnonymousProfileEntity?> PromoteAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public class ProfilePromoter : IProfilePromoter
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<ProfilePromoter> _logger;
    private readonly double _promotionThreshold;

    public ProfilePromoter(
        SegmentCommerceDbContext context,
        ILogger<ProfilePromoter> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _promotionThreshold = configuration.GetValue("Profiles:PromotionThreshold", 0.5);
    }

    public async Task<AnonymousProfileEntity?> PromoteAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.SessionProfiles
            .Include(s => s.InterestScores)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        if (session.IsPromoted)
        {
            return await LoadProfileAsync(session.AnonymousProfileId, cancellationToken);
        }

        var threshold = session.PromotionThreshold > 0 ? session.PromotionThreshold : _promotionThreshold;
        if (session.TotalWeight < threshold)
        {
            return null;
        }

        var profile = await LoadOrCreateProfileAsync(session.ProfileKey, cancellationToken);

        profile.TotalWeight += session.TotalWeight;
        profile.SignalCount += session.SignalCount;
        profile.LastSeenAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        foreach (var interest in session.InterestScores)
        {
            if (string.IsNullOrEmpty(interest.Category))
            {
                continue;
            }

            var existing = await _context.InterestScores
                .FirstOrDefaultAsync(i => i.ProfileId == profile.Id && i.Category == interest.Category, cancellationToken);

            if (existing == null)
            {
                existing = new InterestScoreEntity
                {
                    Profile = profile,
                    Category = interest.Category,
                    Score = interest.Score,
                    DecayRate = interest.DecayRate,
                    LastUpdatedAt = interest.LastUpdatedAt
                };

                _context.InterestScores.Add(existing);
            }
            else
            {
                existing.Score += interest.Score;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        session.AnonymousProfile = profile;
        session.IsPromoted = true;
        session.ExpiresAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Promoted session {SessionId} to profile {ProfileId}", session.Id, profile.Id);

        return profile;
    }

    private async Task<AnonymousProfileEntity> LoadOrCreateProfileAsync(string? profileKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(profileKey))
        {
            var existing = await _context.AnonymousProfiles
                .FirstOrDefaultAsync(p => p.ProfileKey == profileKey, cancellationToken);

            if (existing != null)
            {
                return existing;
            }
        }

        var key = profileKey ?? Guid.NewGuid().ToString("N");
        var profile = new AnonymousProfileEntity
        {
            ProfileKey = key,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AnonymousProfiles.Add(profile);
        await _context.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private async Task<AnonymousProfileEntity?> LoadProfileAsync(Guid? profileId, CancellationToken cancellationToken)
    {
        if (profileId == null)
        {
            return null;
        }

        return await _context.AnonymousProfiles.FirstOrDefaultAsync(p => p.Id == profileId, cancellationToken);
    }
}
