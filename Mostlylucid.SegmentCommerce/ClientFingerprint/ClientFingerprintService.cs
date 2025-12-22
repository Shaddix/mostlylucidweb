using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// Service for generating session identifiers from client fingerprint hashes.
/// 
/// PRIVACY: This service does NOT collect or use:
/// - IP addresses
/// - Location data
/// - Personal information
/// - Raw fingerprint signals
/// 
/// It only receives a client-computed hash and re-hashes it with a server key.
/// </summary>
public interface IClientFingerprintService
{
    /// <summary>
    /// Generate a keyed session identifier from the client's fingerprint hash.
    /// The client hash is HMAC'd with a server secret to produce the final ID.
    /// </summary>
    string GenerateSessionId(string clientHash);
    
    /// <summary>
    /// Store the session ID in the current request context.
    /// </summary>
    void SetSessionId(HttpContext context, string sessionId);
    
    /// <summary>
    /// Get the session ID from the current request context.
    /// </summary>
    string? GetSessionId(HttpContext context);
}

public class ClientFingerprintService : IClientFingerprintService
{
    private readonly byte[] _hmacKey;
    private readonly ILogger<ClientFingerprintService> _logger;
    
    private const string ContextKey = "FingerprintSessionId";

    public ClientFingerprintService(
        IOptions<ClientFingerprintConfig> config,
        ILogger<ClientFingerprintService> logger)
    {
        _logger = logger;
        
        if (!string.IsNullOrEmpty(config.Value.HmacKey))
        {
            _hmacKey = Convert.FromBase64String(config.Value.HmacKey);
        }
        else
        {
            // Generate ephemeral key - sessions won't persist across restarts
            _hmacKey = RandomNumberGenerator.GetBytes(32);
            _logger.LogWarning(
                "No HMAC key configured for ClientFingerprint. " +
                "Sessions will not persist across app restarts. " +
                "Set ClientFingerprint:HmacKey in configuration.");
        }
    }

    public string GenerateSessionId(string clientHash)
    {
        if (string.IsNullOrEmpty(clientHash))
            return string.Empty;
            
        // HMAC-SHA256 the client hash with our secret key
        using var hmac = new HMACSHA256(_hmacKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(clientHash));
        
        // Return as URL-safe base64, truncated to reasonable length
        return Convert.ToBase64String(hashBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=')[..22]; // ~128 bits of entropy
    }

    public void SetSessionId(HttpContext context, string sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            context.Items[ContextKey] = sessionId;
        }
    }

    public string? GetSessionId(HttpContext context)
    {
        return context.Items.TryGetValue(ContextKey, out var id) ? id as string : null;
    }
}
