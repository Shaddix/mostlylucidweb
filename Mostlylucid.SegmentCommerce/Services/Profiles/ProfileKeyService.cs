using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Profiles;

public record ProfileKeyRequest(string? FingerprintHash, string? CookieId, string? UserId);

public interface IProfileKeyService
{
    string GenerateKey(ProfileKeyRequest request);

    Task<ProfileKeyEntity> GetOrCreateAsync(ProfileKeyRequest request, CancellationToken cancellationToken = default);

    Task<AnonymousProfileEntity> AttachOrCreateProfileAsync(ProfileKeyRequest request, CancellationToken cancellationToken = default);
}

public class ProfileKeyService : IProfileKeyService
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<ProfileKeyService> _logger;
    private readonly string _secret;

    public ProfileKeyService(
        SegmentCommerceDbContext context,
        ILogger<ProfileKeyService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _secret = configuration["Profiles:KeySecret"] ?? "change-me";
    }

    public string GenerateKey(ProfileKeyRequest request)
    {
        var payload = $"{request.FingerprintHash ?? string.Empty}|{request.CookieId ?? string.Empty}|{request.UserId ?? string.Empty}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<ProfileKeyEntity> GetOrCreateAsync(ProfileKeyRequest request, CancellationToken cancellationToken = default)
    {
        var keyHash = GenerateKey(request);
        var method = ResolveMethod(request);

        var existing = await _context.ProfileKeys
            .Include(k => k.Profile)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);

        if (existing != null)
        {
            existing.Profile.LastSeenAt = DateTime.UtcNow;
            existing.Profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var profile = new AnonymousProfileEntity
        {
            ProfileKey = keyHash,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var profileKey = new ProfileKeyEntity
        {
            KeyHash = keyHash,
            DerivationMethod = method,
            SourceHint = method,
            Profile = profile,
            IsPrimary = true
        };

        _context.AnonymousProfiles.Add(profile);
        _context.ProfileKeys.Add(profileKey);

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Created profile key via {Method}", method);

        return profileKey;
    }

    public async Task<AnonymousProfileEntity> AttachOrCreateProfileAsync(ProfileKeyRequest request, CancellationToken cancellationToken = default)
    {
        var profileKey = await GetOrCreateAsync(request, cancellationToken);
        return profileKey.Profile;
    }

    private static string ResolveMethod(ProfileKeyRequest request)
    {
        if (!string.IsNullOrEmpty(request.UserId))
        {
            return "user_id";
        }

        if (!string.IsNullOrEmpty(request.FingerprintHash))
        {
            return "fingerprint";
        }

        if (!string.IsNullOrEmpty(request.CookieId))
        {
            return "cookie";
        }

        return "unknown";
    }
}
