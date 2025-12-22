using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Resolves session profiles for requests.
/// 
/// Flow:
/// 1. First request → create session (no persistent profile yet)
/// 2. Fingerprint JS runs → FingerprintController links session to profile
/// 3. Subsequent requests → session already linked
/// 
/// For cookie/identity modes, we can link immediately.
/// For fingerprint mode, linking happens async via JS callback.
/// </summary>
public interface IProfileResolver
{
    Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context);
    Task<PersistentProfileEntity?> GetPersistentProfileAsync(Guid profileId);
}

public class ProfileResolver : IProfileResolver
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ILogger<ProfileResolver> _logger;
    private readonly int _sessionTimeoutMinutes;

    private const string SessionCookieName = ".SegmentCommerce.Session";
    private const string SessionContextKey = "ProfileResolver.Session";

    public ProfileResolver(
        SegmentCommerceDbContext db,
        ILogger<ProfileResolver> logger,
        IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _sessionTimeoutMinutes = config.GetValue("Profiles:SessionTimeoutMinutes", 30);
    }

    public async Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context)
    {
        // Return cached if already resolved this request
        if (context.Items.TryGetValue(SessionContextKey, out var cached) && cached is SessionProfileEntity s)
            return s;

        var sessionKey = GetOrCreateSessionKey(context);
        var now = DateTime.UtcNow;

        var session = await _db.SessionProfiles
            .FirstOrDefaultAsync(s => s.SessionKey == sessionKey && s.ExpiresAt > now);

        if (session == null)
        {
            session = new SessionProfileEntity
            {
                SessionKey = sessionKey,
                StartedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes),
                Context = BuildSessionContext(context)
            };

            // Check if user is logged in - link immediately
            var userId = GetUserId(context);
            if (!string.IsNullOrEmpty(userId))
            {
                var profile = await GetOrCreateIdentityProfileAsync(userId);
                session.PersistentProfileId = profile.Id;
                session.IdentificationMode = ProfileIdentificationMode.Identity;
            }

            _db.SessionProfiles.Add(session);
            await _db.SaveChangesAsync();
            
            _logger.LogDebug("Created session {SessionKey}", sessionKey);
        }
        else
        {
            // Update activity
            session.LastActivityAt = now;
            session.ExpiresAt = now.AddMinutes(_sessionTimeoutMinutes);
            
            // Check if user just logged in
            if (session.IdentificationMode != ProfileIdentificationMode.Identity)
            {
                var userId = GetUserId(context);
                if (!string.IsNullOrEmpty(userId))
                {
                    var profile = await GetOrCreateIdentityProfileAsync(userId);
                    session.PersistentProfileId = profile.Id;
                    session.IdentificationMode = ProfileIdentificationMode.Identity;
                    _logger.LogDebug("Upgraded session to identity mode");
                }
            }
            
            await _db.SaveChangesAsync();
        }

        context.Items[SessionContextKey] = session;
        context.Items["SessionId"] = session.Id;
        context.Items["SessionKey"] = session.SessionKey;

        return session;
    }

    public async Task<PersistentProfileEntity?> GetPersistentProfileAsync(Guid profileId)
    {
        return await _db.PersistentProfiles.FindAsync(profileId);
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
        // Use ASP.NET session ID if available
        if (context.Session.IsAvailable && !string.IsNullOrEmpty(context.Session.Id))
            return context.Session.Id;

        // Fallback to cookie
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var existing) && 
            !string.IsNullOrEmpty(existing))
            return existing;

        // Generate new
        var key = Guid.NewGuid().ToString("N");
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
