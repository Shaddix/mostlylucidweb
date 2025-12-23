using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

public record SessionSignalInput(
    string SessionKey,
    string SignalType,
    string? Category,
    int? ProductId,
    double? Weight,
    SignalContext? Context,
    string? PageUrl);

public interface ISessionCollector
{
    Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken ct = default);
    Task ElevateToProfileAsync(SessionProfileEntity session, PersistentProfileEntity profile, CancellationToken ct = default);
}

public class SessionCollector : ISessionCollector
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ILogger<SessionCollector> _logger;
    private readonly int _sessionTimeoutMinutes;

    public SessionCollector(
        SegmentCommerceDbContext db,
        ILogger<SessionCollector> logger,
        IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _sessionTimeoutMinutes = config.GetValue("Profiles:SessionTimeoutMinutes", 30);
    }

    public async Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var session = await _db.SessionProfiles
            .FirstOrDefaultAsync(s => s.SessionKey == input.SessionKey, ct);

        if (session == null)
        {
            session = new SessionProfileEntity
            {
                SessionKey = input.SessionKey,
                StartedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes)
            };
            _db.SessionProfiles.Add(session);
        }

        session.LastActivityAt = now;
        session.ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes);

        var weight = input.Weight ?? SignalTypes.GetBaseWeight(input.SignalType);

        // Record detailed signal (optional, for history)
        var signal = new SignalEntity
        {
            SessionId = session.Id,
            SignalType = input.SignalType,
            Category = input.Category,
            ProductId = input.ProductId,
            Weight = weight,
            Context = input.Context,
            PageUrl = input.PageUrl,
            CreatedAt = now
        };
        _db.Signals.Add(signal);

        // Update aggregates
        session.TotalWeight += weight;
        session.SignalCount++;

        // Update JSONB interests
        if (!string.IsNullOrEmpty(input.Category))
        {
            session.Interests.TryGetValue(input.Category, out var currentScore);
            session.Interests[input.Category] = currentScore + weight;

            // Update signal counts
            if (!session.Signals.ContainsKey(input.Category))
                session.Signals[input.Category] = new Dictionary<string, int>();

            session.Signals[input.Category].TryGetValue(input.SignalType, out var count);
            session.Signals[input.Category][input.SignalType] = count + 1;
        }

        // Track viewed products
        if (input.ProductId.HasValue && input.SignalType == SignalTypes.ProductView)
        {
            if (!session.ViewedProducts.Contains(input.ProductId.Value))
            {
                session.ViewedProducts.Add(input.ProductId.Value);
                session.ProductViews++;
            }
        }

        // Update counters
        switch (input.SignalType)
        {
            case SignalTypes.PageView:
                session.PageViews++;
                break;
            case SignalTypes.AddToCart:
                session.CartAdds++;
                break;
        }

        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task ElevateToProfileAsync(SessionProfileEntity session, PersistentProfileEntity profile, CancellationToken ct = default)
    {
        if (session.IsElevated)
            return;

        // Merge interests (use higher value)
        foreach (var (category, score) in session.Interests)
        {
            if (!profile.Interests.ContainsKey(category) || profile.Interests[category] < score)
                profile.Interests[category] = score;
        }

        // Update stats
        profile.TotalSessions++;
        profile.TotalSignals += session.SignalCount;
        profile.TotalCartAdds += session.CartAdds;
        profile.LastSeenAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        // Mark session as elevated
        session.IsElevated = true;
        session.PersistentProfileId = profile.Id;

        // Clear segment cache (will be recomputed)
        profile.SegmentsComputedAt = null;
        profile.EmbeddingComputedAt = null;

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Elevated session {SessionId} to profile {ProfileId}", session.Id, profile.Id);
    }
}
