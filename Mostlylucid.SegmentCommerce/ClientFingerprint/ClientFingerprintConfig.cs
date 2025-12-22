namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// Configuration for client-side fingerprinting (zero-cookie session identification).
/// </summary>
public class ClientFingerprintConfig
{
    public const string SectionName = "ClientFingerprint";
    
    /// <summary>
    /// Enable/disable client fingerprinting entirely.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// API endpoint path for receiving fingerprint hash.
    /// </summary>
    public string Endpoint { get; set; } = "/api/fingerprint";
    
    /// <summary>
    /// HMAC key for hashing fingerprint data (must be kept secret).
    /// If empty, a random key is generated at startup.
    /// </summary>
    public string HmacKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Collect WebGL vendor/renderer info for fingerprint.
    /// </summary>
    public bool CollectWebGL { get; set; } = true;
    
    /// <summary>
    /// Collect canvas fingerprint.
    /// </summary>
    public bool CollectCanvas { get; set; } = true;
    
    /// <summary>
    /// Collect audio context fingerprint.
    /// </summary>
    public bool CollectAudio { get; set; } = true;
    
    /// <summary>
    /// XHR timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 5000;
}
