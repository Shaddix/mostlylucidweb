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
    /// <summary>
    /// Record a signal to the session profile (in-memory only).
    /// </summary>
    SessionProfileEntity RecordSignal(SessionSignalInput input);
    
    /// <summary>
    /// Record a signal asynchronously (still in-memory, async for interface compatibility).
    /// </summary>
    Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken ct = default);
    
    /// <summary>
    /// Elevate session signals to a persistent profile (this DOES write to DB).
    /// </summary>
    Task ElevateToProfileAsync(SessionProfileEntity session, PersistentProfileEntity profile, CancellationToken ct = default);
}

public class SessionCollector : ISessionCollector
{
    private readonly ISessionProfileCache _sessionCache;
    private readonly SegmentCommerceDbContext _db;
    private readonly ILogger<SessionCollector> _logger;
    private readonly int _sessionTimeoutMinutes;

    public SessionCollector(
        ISessionProfileCache sessionCache,
        SegmentCommerceDbContext db,
        ILogger<SessionCollector> logger,
        IConfiguration config)
    {
        _sessionCache = sessionCache;
        _db = db;
        _logger = logger;
        _sessionTimeoutMinutes = config.GetValue("Profiles:SessionTimeoutMinutes", 30);
    }

    /// <summary>
    /// Record a signal to the session profile. 
    /// Session profiles are in-memory only - no database writes.
    /// </summary>
    public SessionProfileEntity RecordSignal(SessionSignalInput input)
    {
        var now = DateTime.UtcNow;

        // Get or create session from in-memory cache
        var session = _sessionCache.GetOrCreate(input.SessionKey, () => new SessionProfileEntity
        {
            SessionKey = input.SessionKey,
            StartedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes)
        });

        session.LastActivityAt = now;

        var weight = input.Weight ?? SignalTypes.GetBaseWeight(input.SignalType);

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

        // Update cache (refreshes sliding expiration)
        _sessionCache.Set(input.SessionKey, session);

        return session;
    }

    /// <summary>
    /// Async wrapper for RecordSignal - still in-memory, no DB writes.
    /// Kept for interface compatibility.
    /// </summary>
    public Task<SessionProfileEntity> RecordSignalAsync(SessionSignalInput input, CancellationToken ct = default)
    {
        var session = RecordSignal(input);
        return Task.FromResult(session);
    }

    /// <summary>
    /// Elevate high-value session signals to a persistent profile.
    /// This DOES write to the database (persistent profiles are stored in DB).
    /// </summary>
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

        // Mark session as elevated (in cache)
        session.IsElevated = true;
        session.PersistentProfileId = profile.Id;
        _sessionCache.Set(session.SessionKey, session);

        // Clear segment cache (will be recomputed)
        profile.SegmentsComputedAt = null;
        profile.EmbeddingComputedAt = null;

        // Save persistent profile to database
        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Elevated session {SessionKey} to profile {ProfileId}", session.SessionKey, profile.Id);
    }
}
