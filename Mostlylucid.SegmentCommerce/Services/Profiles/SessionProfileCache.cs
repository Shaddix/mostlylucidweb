using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// In-memory LFU cache for session profiles with sliding expiration.
/// Session profiles are NEVER persisted to database - they exist only in memory.
/// Data is intentionally lost on restart (ephemeral by design).
/// 
/// When sessions expire, high-value signals can be elevated to persistent profiles
/// via the OnSessionExpired event.
/// </summary>
public interface ISessionProfileCache
{
    /// <summary>
    /// Get or create a session profile for the given session key.
    /// </summary>
    SessionProfileEntity GetOrCreate(string sessionKey, Func<SessionProfileEntity>? factory = null);

    /// <summary>
    /// Get a session profile if it exists.
    /// </summary>
    SessionProfileEntity? Get(string sessionKey);

    /// <summary>
    /// Update a session profile (refreshes sliding expiration).
    /// </summary>
    void Set(string sessionKey, SessionProfileEntity profile);

    /// <summary>
    /// Remove a session profile.
    /// </summary>
    void Remove(string sessionKey);

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    SessionCacheStats GetStats();

    /// <summary>
    /// Event fired when a session expires. Use this to elevate signals to persistent profile.
    /// </summary>
    event Action<SessionProfileEntity>? OnSessionExpired;
}

public class SessionProfileCache : ISessionProfileCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionProfileCache> _logger;
    private readonly TimeSpan _slidingExpiration;
    private readonly TimeSpan _absoluteExpiration;
    
    // Track stats since IMemoryCache doesn't expose them
    private long _hits;
    private long _misses;
    private long _evictions;

    private const string CacheKeyPrefix = "session_profile:";

    /// <summary>
    /// Event fired when a session expires. Subscribers can use this to elevate signals.
    /// </summary>
    public event Action<SessionProfileEntity>? OnSessionExpired;

    public SessionProfileCache(
        IMemoryCache cache,
        ILogger<SessionProfileCache> logger,
        IConfiguration config)
    {
        _cache = cache;
        _logger = logger;
        
        var sessionTimeoutMinutes = config.GetValue("Profiles:SessionTimeoutMinutes", 30);
        _slidingExpiration = TimeSpan.FromMinutes(sessionTimeoutMinutes);
        _absoluteExpiration = TimeSpan.FromHours(24); // Max lifetime even with activity
    }

    public SessionProfileEntity GetOrCreate(string sessionKey, Func<SessionProfileEntity>? factory = null)
    {
        var cacheKey = CacheKeyPrefix + sessionKey;

        if (_cache.TryGetValue(cacheKey, out SessionProfileEntity? existing) && existing != null)
        {
            Interlocked.Increment(ref _hits);
            // Touch to refresh sliding expiration
            existing.LastActivityAt = DateTime.UtcNow;
            return existing;
        }

        Interlocked.Increment(ref _misses);

        var profile = factory?.Invoke() ?? new SessionProfileEntity
        {
            SessionKey = sessionKey,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_slidingExpiration)
        };

        Set(sessionKey, profile);
        
        _logger.LogDebug("Created session profile {SessionKey}", sessionKey);
        return profile;
    }

    public SessionProfileEntity? Get(string sessionKey)
    {
        var cacheKey = CacheKeyPrefix + sessionKey;

        if (_cache.TryGetValue(cacheKey, out SessionProfileEntity? profile) && profile != null)
        {
            Interlocked.Increment(ref _hits);
            return profile;
        }

        Interlocked.Increment(ref _misses);
        return null;
    }

    public void Set(string sessionKey, SessionProfileEntity profile)
    {
        var cacheKey = CacheKeyPrefix + sessionKey;

        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _slidingExpiration,
            AbsoluteExpirationRelativeToNow = _absoluteExpiration,
            Priority = CacheItemPriority.Normal
        };

        // Track evictions and fire event for session expiration
        options.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (reason != EvictionReason.Replaced)
            {
                Interlocked.Increment(ref _evictions);
                _logger.LogDebug("Session profile evicted: {Key}, Reason: {Reason}", key, reason);

                // Fire event so signals can be elevated to persistent profile
                if (value is SessionProfileEntity expiredSession && 
                    expiredSession.PersistentProfileId.HasValue && 
                    !expiredSession.IsElevated &&
                    expiredSession.SignalCount > 0)
                {
                    try
                    {
                        OnSessionExpired?.Invoke(expiredSession);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling session expiration for {SessionKey}", expiredSession.SessionKey);
                    }
                }
            }
        });

        profile.ExpiresAt = DateTime.UtcNow.Add(_slidingExpiration);
        _cache.Set(cacheKey, profile, options);
    }

    public void Remove(string sessionKey)
    {
        var cacheKey = CacheKeyPrefix + sessionKey;
        _cache.Remove(cacheKey);
    }

    public SessionCacheStats GetStats()
    {
        return new SessionCacheStats
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Evictions = Interlocked.Read(ref _evictions),
            HitRatio = _hits + _misses > 0 
                ? (double)_hits / (_hits + _misses) 
                : 0
        };
    }
}

public class SessionCacheStats
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long Evictions { get; set; }
    public double HitRatio { get; set; }
}
