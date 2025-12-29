using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Resolves session profiles for requests.
/// 
/// Session profiles are stored ONLY in memory (IMemoryCache) with sliding expiration.
/// They are NEVER persisted to database - ephemeral by design.
/// 
/// Flow:
/// 1. First request → create session in cache (no persistent profile yet)
/// 2. Fingerprint JS runs → FingerprintController links session to persistent profile
/// 3. Subsequent requests → session retrieved from cache
/// 
/// For cookie/identity modes, we can link immediately.
/// For fingerprint mode, linking happens async via JS callback.
/// </summary>
public interface IProfileResolver
{
    Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context);
    Task<PersistentProfileEntity?> GetPersistentProfileAsync(Guid profileId);
    
    /// <summary>
    /// Link a session to a persistent profile (e.g., after fingerprint resolution).
    /// </summary>
    Task LinkSessionToProfileAsync(string sessionKey, Guid persistentProfileId, ProfileIdentificationMode mode);
}

public class ProfileResolver : IProfileResolver
{
    private readonly ISessionProfileCache _sessionCache;
    private readonly IEphemeralSessionService _ephemeralService;
    private readonly SegmentCommerceDbContext _db;
    private readonly ILogger<ProfileResolver> _logger;
    private readonly int _sessionTimeoutMinutes;

    private const string SessionCookieName = ".SegmentCommerce.Session";
    private const string SessionContextKey = "ProfileResolver.Session";
    private const string EphemeralKeyContextKey = "ProfileResolver.EphemeralKey";

    public ProfileResolver(
        ISessionProfileCache sessionCache,
        IEphemeralSessionService ephemeralService,
        SegmentCommerceDbContext db,
        ILogger<ProfileResolver> logger,
        IConfiguration config)
    {
        _sessionCache = sessionCache;
        _ephemeralService = ephemeralService;
        _db = db;
        _logger = logger;
        _sessionTimeoutMinutes = config.GetValue("Profiles:SessionTimeoutMinutes", 30);
    }

    public async Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context)
    {
        // Return cached if already resolved this request
        if (context.Items.TryGetValue(SessionContextKey, out var cached) && cached is SessionProfileEntity s)
            return s;

        var now = DateTime.UtcNow;
        SessionProfileEntity session;
        string? ephemeralKey = null;

        // 1. Check for ephemeral session via ?sessionid=xxx (cookieless mode)
        var querySessionId = context.Request.Query[IEphemeralSessionService.QueryParam].FirstOrDefault();
        if (_ephemeralService.IsValidKeyFormat(querySessionId) && querySessionId is not null)
        {
            ephemeralKey = querySessionId;
            session = _ephemeralService.GetOrCreate(ephemeralKey, () => new SessionProfileEntity
            {
                SessionKey = ephemeralKey!,
                StartedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes),
                IdentificationMode = ProfileIdentificationMode.None, // Ephemeral = no persistent tracking
                Context = BuildSessionContext(context)
            });
            
            _logger.LogDebug("Using ephemeral session from query: {Key}", ephemeralKey);
        }
        else
        {
            // 2. Standard session resolution (cookie/header based)
            var sessionKey = GetOrCreateSessionKey(context);
            session = _sessionCache.GetOrCreate(sessionKey, () => new SessionProfileEntity
            {
                SessionKey = sessionKey,
                StartedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes),
                Context = BuildSessionContext(context)
            });

            // Check if user is logged in - link immediately if not already linked
            if (session.IdentificationMode != ProfileIdentificationMode.Identity)
            {
                var userId = GetUserId(context);
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await GetOrCreateIdentityProfileAsync(userId);
                    session.PersistentProfileId = profile.Id;
                    session.IdentificationMode = ProfileIdentificationMode.Identity;
                    _sessionCache.Set(sessionKey, session);
                    _logger.LogDebug("Linked session to identity profile {ProfileId}", profile.Id);
                }
            }
        }

        // Update activity timestamp
        session.LastActivityAt = now;

        // Store in HttpContext.Items for this request
        context.Items[SessionContextKey] = session;
        context.Items["SessionId"] = session.Id;
        context.Items["SessionKey"] = session.SessionKey;
        if (ephemeralKey != null)
            context.Items[EphemeralKeyContextKey] = ephemeralKey;

        return session;
    }

    public async Task<PersistentProfileEntity?> GetPersistentProfileAsync(Guid profileId)
    {
        return await _db.PersistentProfiles.FindAsync(profileId);
    }

    public async Task LinkSessionToProfileAsync(string sessionKey, Guid persistentProfileId, ProfileIdentificationMode mode)
    {
        var session = _sessionCache.Get(sessionKey);
        if (session == null)
        {
            _logger.LogWarning("Cannot link session {SessionKey} - not found in cache", sessionKey);
            return;
        }

        // Only upgrade if not already at a higher identification level
        if (session.IdentificationMode >= mode)
        {
            _logger.LogDebug("Session already has identification mode {Mode}, skipping", session.IdentificationMode);
            return;
        }

        session.PersistentProfileId = persistentProfileId;
        session.IdentificationMode = mode;
        _sessionCache.Set(sessionKey, session);

        _logger.LogDebug("Linked session {SessionKey} to profile {ProfileId} via {Mode}", 
            sessionKey, persistentProfileId, mode);
    }

    private async Task<PersistentProfileEntity> GetOrCreateIdentityProfileAsync(string userId)
    {
        var profile = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == userId && 
                                      p.IdentificationMode == ProfileIdentificationMode.Identity);

        if (profile == null)
        {
            profile = new PersistentProfileEntity
            {
                ProfileKey = userId,
                IdentificationMode = ProfileIdentificationMode.Identity
            };
            _db.PersistentProfiles.Add(profile);
            await _db.SaveChangesAsync();
        }

        return profile;
    }

    private static string GetOrCreateSessionKey(HttpContext context)
    {
        // 1. Check if middleware provided a session key (from header or query - cookieless mode)
        if (context.Items.TryGetValue("ProvidedSessionKey", out var providedKey) && 
            providedKey is string pk && !string.IsNullOrEmpty(pk))
            return pk;
        
        // 2. Use ASP.NET session ID if available
        if (context.Session.IsAvailable && !string.IsNullOrEmpty(context.Session.Id))
            return context.Session.Id;

        // 3. Fallback to cookie
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var existing) && 
            !string.IsNullOrEmpty(existing))
            return existing;

        // 4. Generate new session key
        var key = $"s_{Guid.NewGuid():N}";
        
        // Still set cookie as fallback, but client JS will prefer header/query approach
        context.Response.Cookies.Append(SessionCookieName, key, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(7)
        });
        return key;
    }

    private static string? GetUserId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return null;

        return context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }

    private static SessionContext BuildSessionContext(HttpContext context)
    {
        var ua = context.Request.Headers.UserAgent.ToString().ToLowerInvariant();
        var deviceType = ua.Contains("mobile") ? "mobile"
                       : ua.Contains("tablet") ? "tablet"
                       : "desktop";

        var hour = DateTime.UtcNow.Hour;
        var timeOfDay = hour switch
        {
            >= 5 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };

        string? referrerDomain = null;
        var referer = context.Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
            referrerDomain = refUri.Host;

        return new SessionContext
        {
            DeviceType = deviceType,
            EntryPath = context.Request.Path.Value,
            ReferrerDomain = referrerDomain,
            TimeOfDay = timeOfDay,
            DayType = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday 
                ? "weekend" : "weekday"
        };
    }
}
