using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Background service that handles session expiration events.
/// When a session expires from the cache, this service elevates
/// high-value signals to the persistent profile.
/// </summary>
public class SessionExpirationHandler : BackgroundService
{
    private readonly ISessionProfileCache _sessionCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionExpirationHandler> _logger;
    private readonly Channel<SessionProfileEntity> _expirationQueue;

    public SessionExpirationHandler(
        ISessionProfileCache sessionCache,
        IServiceScopeFactory scopeFactory,
        ILogger<SessionExpirationHandler> logger)
    {
        _sessionCache = sessionCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _expirationQueue = Channel.CreateBounded<SessionProfileEntity>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        // Subscribe to session expiration events
        _sessionCache.OnSessionExpired += OnSessionExpired;
    }

    private void OnSessionExpired(SessionProfileEntity session)
    {
        // Queue for async processing (don't block the cache eviction callback)
        if (!_expirationQueue.Writer.TryWrite(session))
        {
            _logger.LogWarning("Failed to queue session expiration for {SessionKey}", session.SessionKey);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionExpirationHandler started");

        await foreach (var session in _expirationQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ElevateSessionAsync(session, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to elevate session {SessionKey}", session.SessionKey);
            }
        }
    }

    private async Task ElevateSessionAsync(SessionProfileEntity session, CancellationToken ct)
    {
        if (session.IsElevated || !session.PersistentProfileId.HasValue)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SegmentCommerceDbContext>();

        var persistentProfile = await db.PersistentProfiles.FindAsync(
            new object[] { session.PersistentProfileId.Value }, ct);

        if (persistentProfile == null)
        {
            _logger.LogWarning("Persistent profile {ProfileId} not found for session {SessionKey}",
                session.PersistentProfileId, session.SessionKey);
            return;
        }

        // Merge interests (use higher value)
        foreach (var (category, score) in session.Interests)
        {
            if (!persistentProfile.Interests.ContainsKey(category) || 
                persistentProfile.Interests[category] < score)
            {
                persistentProfile.Interests[category] = score;
            }
        }

        // Update stats
        persistentProfile.TotalSessions++;
        persistentProfile.TotalSignals += session.SignalCount;
        persistentProfile.TotalCartAdds += session.CartAdds;
        persistentProfile.LastSeenAt = DateTime.UtcNow;
        persistentProfile.UpdatedAt = DateTime.UtcNow;

        // Clear segment cache (will be recomputed)
        persistentProfile.SegmentsComputedAt = null;
        persistentProfile.EmbeddingComputedAt = null;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Elevated expired session {SessionKey} to profile {ProfileId}",
            session.SessionKey, persistentProfile.Id);
    }

    public override void Dispose()
    {
        _sessionCache.OnSessionExpired -= OnSessionExpired;
        _expirationQueue.Writer.Complete();
        base.Dispose();
    }
}
