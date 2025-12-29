using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Ephemeral session service for cookieless "Session Only" mode.
/// 
/// Generates short, URL-friendly session keys using hash + base62.
/// Sessions live only in memory cache with sliding expiration.
/// Key is passed via query string: ?sessionid=2kF9xMnP3
/// </summary>
public interface IEphemeralSessionService
{
    /// <summary>
    /// Query string parameter name for session key.
    /// </summary>
    const string QueryParam = "sessionid";
    
    /// <summary>
    /// Generate a new ephemeral session key.
    /// </summary>
    string GenerateKey();
    
    /// <summary>
    /// Get session by key, returns null if expired/not found.
    /// </summary>
    SessionProfileEntity? Get(string key);
    
    /// <summary>
    /// Get or create session for the given key.
    /// </summary>
    SessionProfileEntity GetOrCreate(string key, Func<SessionProfileEntity> factory);
    
    /// <summary>
    /// Update session in cache (extends expiration).
    /// </summary>
    void Set(string key, SessionProfileEntity session);
    
    /// <summary>
    /// Remove session from cache.
    /// </summary>
    void Remove(string key);
    
    /// <summary>
    /// Check if a key is valid format (not expired check, just format).
    /// </summary>
    bool IsValidKeyFormat(string? key);
}

public class EphemeralSessionService : IEphemeralSessionService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<EphemeralSessionService> _logger;
    private readonly TimeSpan _slidingExpiration;
    private readonly TimeSpan _absoluteExpiration;
    
    // Base62 alphabet (alphanumeric, URL-safe)
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    
    // Key length constraints
    private const int MinKeyLength = 8;
    private const int MaxKeyLength = 16;

    public EphemeralSessionService(
        IMemoryCache cache,
        ILogger<EphemeralSessionService> logger,
        IConfiguration config)
    {
        _cache = cache;
        _logger = logger;
        
        var slidingMinutes = config.GetValue("Profiles:EphemeralSlidingMinutes", 30);
        var absoluteMinutes = config.GetValue("Profiles:EphemeralAbsoluteMinutes", 120);
        
        _slidingExpiration = TimeSpan.FromMinutes(slidingMinutes);
        _absoluteExpiration = TimeSpan.FromMinutes(absoluteMinutes);
    }

    public string GenerateKey()
    {
        // Generate GUID and hash it to get a shorter key
        var guid = Guid.NewGuid();
        var bytes = guid.ToByteArray();
        
        // Use first 8 bytes of SHA256 hash (like xxHash64 output size)
        var hash = SHA256.HashData(bytes);
        var hashValue = BitConverter.ToUInt64(hash, 0);
        
        // Convert to base62
        var key = ToBase62(hashValue);
        
        _logger.LogDebug("Generated ephemeral session key: {Key}", key);
        return key;
    }

    public SessionProfileEntity? Get(string key)
    {
        var cacheKey = GetCacheKey(key);
        return _cache.Get<SessionProfileEntity>(cacheKey);
    }

    public SessionProfileEntity GetOrCreate(string key, Func<SessionProfileEntity> factory)
    {
        var cacheKey = GetCacheKey(key);
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = _slidingExpiration;
            entry.AbsoluteExpirationRelativeToNow = _absoluteExpiration;
            entry.Priority = CacheItemPriority.Normal;
            
            var session = factory();
            _logger.LogDebug("Created ephemeral session for key: {Key}", key);
            return session;
        })!;
    }

    public void Set(string key, SessionProfileEntity session)
    {
        var cacheKey = GetCacheKey(key);
        
        var options = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _slidingExpiration,
            AbsoluteExpirationRelativeToNow = _absoluteExpiration,
            Priority = CacheItemPriority.Normal
        };
        
        _cache.Set(cacheKey, session, options);
    }

    public void Remove(string key)
    {
        var cacheKey = GetCacheKey(key);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Removed ephemeral session: {Key}", key);
    }

    public bool IsValidKeyFormat(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
            
        if (key.Length < MinKeyLength || key.Length > MaxKeyLength)
            return false;
            
        // Must be all base62 characters
        return key.All(c => Base62Chars.Contains(c));
    }

    private static string GetCacheKey(string key) => $"ephemeral:{key}";

    private static string ToBase62(ulong value)
    {
        if (value == 0)
            return "00000000"; // Minimum length
            
        var sb = new StringBuilder(12);
        
        while (value > 0)
        {
            sb.Insert(0, Base62Chars[(int)(value % 62)]);
            value /= 62;
        }
        
        // Pad to minimum length
        while (sb.Length < MinKeyLength)
            sb.Insert(0, '0');
        
        return sb.ToString();
    }
}
