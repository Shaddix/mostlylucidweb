using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

public record SessionSignalInput(
    string SessionKey,
    string SignalType,
    string? Category,
    int? ProductId,
    double? Weight,
    Dictionary<string, object>? Context,
    string? PageUrl,
    string? Referrer,
    string? ProfileKey);

public interface ISessionCollector
{
    Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken cancellationToken = default);
}

public class SessionCollector : ISessionCollector
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<SessionCollector> _logger;
    private readonly int _sessionTimeoutMinutes;
    private readonly double _promotionThreshold;

    public SessionCollector(
        SegmentCommerceDbContext context,
        ILogger<SessionCollector> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _sessionTimeoutMinutes = configuration.GetValue("Profiles:SessionTimeoutMinutes", 30);
        _promotionThreshold = configuration.GetValue("Profiles:PromotionThreshold", 0.5);
    }

    public async Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var session = await _context.SessionProfiles
            .Include(s => s.InterestScores)
            .FirstOrDefaultAsync(s => s.SessionKey == input.SessionKey, cancellationToken);

        if (session == null)
        {
            session = new SessionProfileEntity
            {
                SessionKey = input.SessionKey,
                ProfileKey = input.ProfileKey,
                PromotionThreshold = _promotionThreshold,
                StartedAt = now,
                LastSeenAt = now,
                ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes)
            };

            _context.SessionProfiles.Add(session);
        }

        if (!string.IsNullOrEmpty(input.ProfileKey) && string.IsNullOrEmpty(session.ProfileKey))
        {
            session.ProfileKey = input.ProfileKey;
        }

        session.LastSeenAt = now;
        session.ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes);

        var weight = input.Weight ?? SignalTypes.GetBaseWeight(input.SignalType);

        var signal = new SignalEntity
        {
            SessionId = session.Id,
            SignalType = input.SignalType,
            Category = input.Category,
            ProductId = input.ProductId,
            Weight = weight,
            Context = input.Context,
            PageUrl = input.PageUrl,
            Referrer = input.Referrer,
            CreatedAt = now
        };

        _context.Signals.Add(signal);

        session.TotalWeight += weight;
        session.SignalCount += 1;

        if (!string.IsNullOrEmpty(input.Category))
        {
            var interest = session.InterestScores.FirstOrDefault(i => i.Category == input.Category);
            if (interest == null)
            {
                interest = new InterestScoreEntity
                {
                    Session = session,
                    Category = input.Category,
                    Score = weight,
                    DecayRate = 0.02,
                    LastUpdatedAt = now
                };

                session.InterestScores.Add(interest);
                _context.InterestScores.Add(interest);
            }
            else
            {
                interest.Score += weight;
                interest.LastUpdatedAt = now;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Recorded signal {SignalType} for session {SessionKey}", input.SignalType, input.SessionKey);

        return session;
    }
}
