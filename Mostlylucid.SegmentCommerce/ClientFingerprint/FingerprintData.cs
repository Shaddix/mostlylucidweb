using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.ClientFingerprint;

/// <summary>
/// Fingerprint request from client - just the hash, no raw data.
/// </summary>
public class FingerprintRequest
{
    /// <summary>
    /// Client-computed hash of fingerprint signals.
    /// </summary>
    [JsonPropertyName("h")]
    public string Hash { get; set; } = string.Empty;
    
    /// <summary>
    /// Client timestamp.
    /// </summary>
    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }
}
