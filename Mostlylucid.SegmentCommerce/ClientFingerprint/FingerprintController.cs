using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// Receives client fingerprint hash and associates it with the current session.
/// 
/// Flow:
/// 1. Page loads → session created via middleware (no fingerprint yet)
/// 2. JS runs → POSTs fingerprint hash here
/// 3. We HMAC the hash → create/find persistent profile
/// 4. Link session to persistent profile
/// 5. Next request → session is now linked
/// </summary>
[ApiController]
[Route("api/fingerprint")]
public class FingerprintController : ControllerBase
{
    private readonly IClientFingerprintService _fingerprintService;
    private readonly SegmentCommerceDbContext _db;
    private readonly ClientFingerprintConfig _config;
    private readonly ILogger<FingerprintController> _logger;

    public FingerprintController(
        IClientFingerprintService fingerprintService,
        SegmentCommerceDbContext db,
        IOptions<ClientFingerprintConfig> config,
        ILogger<FingerprintController> logger)
    {
        _fingerprintService = fingerprintService;
        _db = db;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receive fingerprint hash from browser and link to current session.
    /// Called via sendBeacon after page load.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceiveFingerprint([FromBody] FingerprintRequest request)
    {
        if (!_config.Enabled || string.IsNullOrEmpty(request.Hash))
            return NoContent();

        try
        {
            // Generate keyed session ID from client hash
            var profileKey = _fingerprintService.GenerateSessionId(request.Hash);

            // Get current session from HttpContext (set by middleware)
            if (!HttpContext.Items.TryGetValue("SessionId", out var sessionIdObj) || 
                sessionIdObj is not Guid sessionId)
            {
                // No session yet - try to get from session key
                var sessionKey = HttpContext.Items["SessionKey"] as string;
                if (string.IsNullOrEmpty(sessionKey))
                {
                    _logger.LogDebug("No session available for fingerprint association");
                    return NoContent();
                }

                var session = await _db.SessionProfiles
                    .FirstOrDefaultAsync(s => s.SessionKey == sessionKey);
                
                if (session != null)
                    sessionId = session.Id;
                else
                    return NoContent();
            }

            // Get or create persistent profile for this fingerprint
            var profile = await GetOrCreateProfileAsync(profileKey);

            // Link session to profile
            await _db.SessionProfiles
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PersistentProfileId, profile.Id)
                    .SetProperty(p => p.IdentificationMode, ProfileIdentificationMode.Fingerprint));

            // Store in HttpContext for rest of this request
            HttpContext.Items["ProfileId"] = profile.Id;
            HttpContext.Items["ProfileKey"] = profileKey;
            HttpContext.Items["IdentificationMode"] = "Fingerprint";

            _logger.LogDebug("Linked session {SessionId} to profile {ProfileId} via fingerprint", 
                sessionId, profile.Id);

            // Return profile info so client can trigger HTMX personalization
            return Ok(new FingerprintResponse
            {
                ProfileId = profile.Id.ToString(),
                IsNew = profile.CreatedAt > DateTime.UtcNow.AddSeconds(-5),
                Segments = GetSegmentNames(profile.Segments)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fingerprint");
            return NoContent();
        }
    }

    /// <summary>
    /// Health check.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { enabled = _config.Enabled, v = "1.0" });

    private async Task<PersistentProfileEntity> GetOrCreateProfileAsync(string profileKey)
    {
        var profile = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == profileKey);

        if (profile == null)
        {
            profile = new PersistentProfileEntity
            {
                ProfileKey = profileKey,
                IdentificationMode = ProfileIdentificationMode.Fingerprint
            };
            _db.PersistentProfiles.Add(profile);
            await _db.SaveChangesAsync();
            
            _logger.LogDebug("Created new profile for fingerprint: {ProfileKey}", profileKey[..8] + "...");
        }
        else
        {
            profile.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return profile;
    }

    /// <summary>
    /// Convert bitflag segments to an array of segment names.
    /// </summary>
    private static string[] GetSegmentNames(ProfileSegments segments)
    {
        if (segments == ProfileSegments.None)
            return [];

        var names = new List<string>();
        foreach (ProfileSegments flag in Enum.GetValues<ProfileSegments>())
        {
            if (flag != ProfileSegments.None && segments.HasFlag(flag))
            {
                names.Add(ToKebabCase(flag.ToString()));
            }
        }
        return names.ToArray();
    }

    private static string ToKebabCase(string value)
    {
        return string.Concat(value.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
