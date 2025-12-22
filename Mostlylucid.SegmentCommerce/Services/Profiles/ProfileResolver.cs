using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.ClientFingerprint;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Resolves and manages profile identification across modes:
/// - None: Session-only, no persistence
/// - Fingerprint: Browser fingerprint hash (zero-cookie)
/// - Cookie: Optional tracking cookie
/// - Identity: Logged-in user ID
/// </summary>
public interface IProfileResolver
{
    /// <summary>
    /// Get or create a session profile for the current request.
    /// </summary>
    Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context);

    /// <summary>
    /// Get the persistent profile for the current request (if identifiable).
    /// </summary>
    Task<PersistentProfileEntity?> GetPersistentProfileAsync(HttpContext context);

    /// <summary>
    /// Link a session to a persistent profile.
    /// </summary>
    Task LinkSessionToProfileAsync(SessionProfileEntity session, PersistentProfileEntity profile);

    /// <summary>
    /// Upgrade identification mode (e.g., fingerprint → identity on login).
    /// </summary>
    Task UpgradeIdentificationAsync(
        PersistentProfileEntity profile,
        ProfileIdentificationMode newMode,
        string newKey);
}

public class ProfileResolver : IProfileResolver
{
    private readonly SegmentCommerceDbContext _db;
    private readonly IClientFingerprintService _fingerprint;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<ProfileResolver> _logger;

    private const string SessionProfileKey = "ProfileResolver.SessionId";
    private const string CookieName = ".SegmentCommerce.ProfileId";

    public ProfileResolver(
        SegmentCommerceDbContext db,
        IClientFingerprintService fingerprint,
        IHttpContextAccessor httpContext,
        ILogger<ProfileResolver> logger)
    {
        _db = db;
        _fingerprint = fingerprint;
        _httpContext = httpContext;
        _logger = logger;
    }

    public async Task<SessionProfileEntity> GetOrCreateSessionAsync(HttpContext context)
    {
        // Check if we already resolved this request
        if (context.Items.TryGetValue(SessionProfileKey, out var cached) && cached is SessionProfileEntity s)
            return s;

        var sessionKey = GetSessionKey(context);
        var session = await _db.SessionProfiles
            .FirstOrDefaultAsync(s => s.SessionKey == sessionKey && s.ExpiresAt > DateTime.UtcNow);

        if (session == null)
        {
            session = new SessionProfileEntity
            {
                SessionKey = sessionKey,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Context = BuildSessionContext(context)
            };
            _db.SessionProfiles.Add(session);
            await _db.SaveChangesAsync();
        }

        // Try to link to persistent profile
        if (session.PersistentProfileId == null)
        {
            var (mode, key) = ResolveIdentification(context);
            if (mode != ProfileIdentificationMode.None && !string.IsNullOrEmpty(key))
            {
                var profile = await GetOrCreatePersistentProfileAsync(mode, key);
                session.PersistentProfileId = profile.Id;
                session.IdentificationMode = mode;
                await _db.SaveChangesAsync();
            }
        }

        context.Items[SessionProfileKey] = session;
        return session;
    }

    public async Task<PersistentProfileEntity?> GetPersistentProfileAsync(HttpContext context)
    {
        var (mode, key) = ResolveIdentification(context);
        if (mode == ProfileIdentificationMode.None || string.IsNullOrEmpty(key))
            return null;

        return await _db.PersistentProfiles
            .Include(p => p.AlternateKeys)
            .FirstOrDefaultAsync(p => p.ProfileKey == key && p.IdentificationMode == mode);
    }

    public async Task LinkSessionToProfileAsync(SessionProfileEntity session, PersistentProfileEntity profile)
    {
        session.PersistentProfileId = profile.Id;
        session.IdentificationMode = profile.IdentificationMode;
        await _db.SaveChangesAsync();
    }

    public async Task UpgradeIdentificationAsync(
        PersistentProfileEntity profile,
        ProfileIdentificationMode newMode,
        string newKey)
    {
        // Store old key as alternate
        if (!profile.AlternateKeys.Any(k => k.KeyValue == profile.ProfileKey))
        {
            profile.AlternateKeys.Add(new ProfileKeyEntity
            {
                ProfileId = profile.Id,
                KeyValue = profile.ProfileKey,
                KeyType = profile.IdentificationMode,
                IsPrimary = false
            });
        }

        // Update to new identification
        profile.ProfileKey = newKey;
        profile.IdentificationMode = newMode;
        profile.UpdatedAt = DateTime.UtcNow;

        // Check for existing profile with new key and merge if needed
        var existing = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == newKey && p.Id != profile.Id);

        if (existing != null)
        {
            await MergeProfilesAsync(existing, profile);
        }

        await _db.SaveChangesAsync();
    }

    private async Task<PersistentProfileEntity> GetOrCreatePersistentProfileAsync(
        ProfileIdentificationMode mode, string key)
    {
        var profile = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == key);

        if (profile == null)
        {
            profile = new PersistentProfileEntity
            {
                ProfileKey = key,
                IdentificationMode = mode
            };
            _db.PersistentProfiles.Add(profile);
            await _db.SaveChangesAsync();
        }

        return profile;
    }

    private (ProfileIdentificationMode Mode, string? Key) ResolveIdentification(HttpContext context)
    {
        // Priority: Identity > Cookie > Fingerprint > None

        // 1. Check for logged-in user
        var userId = context.User?.FindFirst("sub")?.Value
                  ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return (ProfileIdentificationMode.Identity, userId);

        // 2. Check for tracking cookie
        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieId) && !string.IsNullOrEmpty(cookieId))
            return (ProfileIdentificationMode.Cookie, cookieId);

        // 3. Check for fingerprint
        var fingerprintId = _fingerprint.GetSessionId(context);
        if (!string.IsNullOrEmpty(fingerprintId))
            return (ProfileIdentificationMode.Fingerprint, fingerprintId);

        return (ProfileIdentificationMode.None, null);
    }

    private static string GetSessionKey(HttpContext context)
    {
        // Use ASP.NET session ID or generate one
        return context.Session?.Id ?? Guid.NewGuid().ToString("N");
    }

    private static SessionContext BuildSessionContext(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString().ToLowerInvariant();
        var deviceType = userAgent.Contains("mobile") ? "mobile"
                       : userAgent.Contains("tablet") ? "tablet"
                       : "desktop";

        var hour = DateTime.UtcNow.Hour;
        var timeOfDay = hour switch
        {
            >= 5 and < 12 => "morning",
            >= 12 and < 17 => "afternoon",
            >= 17 and < 21 => "evening",
            _ => "night"
        };

        var dayType = DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            ? "weekend" : "weekday";

        string? referrerDomain = null;
        if (Uri.TryCreate(context.Request.Headers.Referer.ToString(), UriKind.Absolute, out var refUri))
        {
            referrerDomain = refUri.Host;
        }

        return new SessionContext
        {
            DeviceType = deviceType,
            EntryPath = context.Request.Path.Value,
            ReferrerDomain = referrerDomain,
            TimeOfDay = timeOfDay,
            DayType = dayType
        };
    }

    private async Task MergeProfilesAsync(PersistentProfileEntity source, PersistentProfileEntity target)
    {
        // Merge interests (take higher values)
        foreach (var (category, score) in source.Interests)
        {
            if (!target.Interests.ContainsKey(category) || target.Interests[category] < score)
                target.Interests[category] = score;
        }

        // Merge affinities
        foreach (var (tag, score) in source.Affinities)
        {
            if (!target.Affinities.ContainsKey(tag) || target.Affinities[tag] < score)
                target.Affinities[tag] = score;
        }

        // Merge brand affinities
        foreach (var (brand, score) in source.BrandAffinities)
        {
            if (!target.BrandAffinities.ContainsKey(brand) || target.BrandAffinities[brand] < score)
                target.BrandAffinities[brand] = score;
        }

        // Merge stats
        target.TotalSessions += source.TotalSessions;
        target.TotalSignals += source.TotalSignals;
        target.TotalPurchases += source.TotalPurchases;
        target.TotalCartAdds += source.TotalCartAdds;

        // Move alternate keys
        foreach (var key in source.AlternateKeys)
        {
            key.ProfileId = target.Id;
        }

        // Re-link sessions
        await _db.SessionProfiles
            .Where(s => s.PersistentProfileId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PersistentProfileId, target.Id));

        // Delete source
        _db.PersistentProfiles.Remove(source);

        _logger.LogInformation("Merged profile {Source} into {Target}", source.Id, target.Id);
    }
}
