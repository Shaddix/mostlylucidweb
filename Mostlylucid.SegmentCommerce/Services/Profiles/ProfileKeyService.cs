using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

/// <summary>
/// Request for profile key generation.
/// </summary>
public record ProfileKeyRequest(
    string? FingerprintHash,
    string? CookieId,
    string? UserId);

/// <summary>
/// Result of profile key lookup/creation.
/// </summary>
public record ProfileKeyResult(
    string KeyHash,
    PersistentProfileEntity Profile,
    bool WasCreated);

/// <summary>
/// Service for generating and managing profile keys.
/// Handles stable key generation for fingerprint/cookie/identity modes.
/// </summary>
public interface IProfileKeyService
{
    /// <summary>
    /// Generate a stable key hash from the request inputs.
    /// </summary>
    string GenerateKey(ProfileKeyRequest request);
    
    /// <summary>
    /// Get or create a profile for the given key request.
    /// </summary>
    Task<ProfileKeyResult> GetOrCreateAsync(ProfileKeyRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Link an alternate key to an existing profile (for profile merging).
    /// </summary>
    Task LinkAlternateKeyAsync(Guid profileId, ProfileKeyRequest alternateKey, CancellationToken ct = default);
}

public class ProfileKeyService : IProfileKeyService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ILogger<ProfileKeyService> _logger;
    private readonly byte[] _secretKey;

    public ProfileKeyService(
        SegmentCommerceDbContext db,
        ILogger<ProfileKeyService> logger,
        IConfiguration config)
    {
        _db = db;
        _logger = logger;
        
        var secret = config["Profiles:KeySecret"] ?? "default-dev-secret-change-in-production";
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateKey(ProfileKeyRequest request)
    {
        // Priority: UserId > CookieId > FingerprintHash
        var input = !string.IsNullOrEmpty(request.UserId) ? $"user:{request.UserId}"
                  : !string.IsNullOrEmpty(request.CookieId) ? $"cookie:{request.CookieId}"
                  : !string.IsNullOrEmpty(request.FingerprintHash) ? $"fp:{request.FingerprintHash}"
                  : throw new ArgumentException("At least one identifier is required");

        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public async Task<ProfileKeyResult> GetOrCreateAsync(ProfileKeyRequest request, CancellationToken ct = default)
    {
        var keyHash = GenerateKey(request);
        
        // Check for existing profile
        var profile = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == keyHash, ct);

        if (profile != null)
        {
            profile.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new ProfileKeyResult(keyHash, profile, WasCreated: false);
        }

        // Check alternate keys for profile merging
        var alternateKey = await _db.ProfileKeys
            .Include(pk => pk.Profile)
            .FirstOrDefaultAsync(pk => pk.KeyValue == keyHash, ct);

        if (alternateKey != null)
        {
            alternateKey.Profile.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new ProfileKeyResult(keyHash, alternateKey.Profile, WasCreated: false);
        }

        // Create new profile
        var mode = !string.IsNullOrEmpty(request.UserId) ? ProfileIdentificationMode.Identity
                 : !string.IsNullOrEmpty(request.CookieId) ? ProfileIdentificationMode.Cookie
                 : ProfileIdentificationMode.Fingerprint;

        profile = new PersistentProfileEntity
        {
            ProfileKey = keyHash,
            IdentificationMode = mode
        };

        _db.PersistentProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Created new profile {ProfileId} with mode {Mode}", profile.Id, mode);
        return new ProfileKeyResult(keyHash, profile, WasCreated: true);
    }

    public async Task LinkAlternateKeyAsync(Guid profileId, ProfileKeyRequest alternateKey, CancellationToken ct = default)
    {
        var keyHash = GenerateKey(alternateKey);
        
        // Check if this key is already linked
        var existing = await _db.ProfileKeys
            .FirstOrDefaultAsync(pk => pk.KeyValue == keyHash, ct);

        if (existing != null)
        {
            if (existing.ProfileId == profileId)
                return; // Already linked to this profile
                
            _logger.LogWarning("Alternate key {KeyHash} already linked to different profile {ExistingProfileId}", 
                keyHash[..8], existing.ProfileId);
            return;
        }

        var mode = !string.IsNullOrEmpty(alternateKey.UserId) ? ProfileIdentificationMode.Identity
                 : !string.IsNullOrEmpty(alternateKey.CookieId) ? ProfileIdentificationMode.Cookie
                 : ProfileIdentificationMode.Fingerprint;

        var profileKeyEntity = new ProfileKeyEntity
        {
            ProfileId = profileId,
            KeyValue = keyHash,
            KeyType = mode
        };

        _db.ProfileKeys.Add(profileKeyEntity);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Linked alternate key to profile {ProfileId}", profileId);
    }
}
