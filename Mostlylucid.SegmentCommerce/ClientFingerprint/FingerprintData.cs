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

/// <summary>
/// Response after fingerprint processing - provides profile info for HTMX personalization.
/// </summary>
public class FingerprintResponse
{
    /// <summary>
    /// The persistent profile ID (for HTMX requests).
    /// </summary>
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a new profile (first visit).
    /// </summary>
    [JsonPropertyName("isNew")]
    public bool IsNew { get; set; }
    
    /// <summary>
    /// Computed segment names for client-side personalization hints.
    /// </summary>
    [JsonPropertyName("segments")]
    public string[] Segments { get; set; } = [];
}
