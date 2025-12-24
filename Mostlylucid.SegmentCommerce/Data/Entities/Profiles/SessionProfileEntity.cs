namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// Ephemeral session profile - collects signals during a visit.
/// 
/// IMPORTANT: This entity is stored IN-MEMORY ONLY via ISessionProfileCache.
/// It is NOT persisted to the database. Data is intentionally lost on restart.
/// 
/// High-value signals are elevated to PersistentProfileEntity when the session expires.
/// Zero PII - only behavioral signals.
/// </summary>
public class SessionProfileEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Session identifier (ASP.NET session ID or fingerprint-derived).
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// Link to persistent profile (if resolved).
    /// </summary>
    public Guid? PersistentProfileId { get; set; }

    /// <summary>
    /// How this session links to persistent profile.
    /// </summary>
    public ProfileIdentificationMode IdentificationMode { get; set; } = ProfileIdentificationMode.None;

    // ============ BEHAVIORAL SIGNALS ============

    /// <summary>
    /// Category interest scores for this session: { "tech": 0.75, "fashion": 0.25 }
    /// </summary>
    public Dictionary<string, double> Interests { get; set; } = new();

    /// <summary>
    /// Detailed signals by category and type.
    /// Structure: { "tech": { "product_view": 5, "add_to_cart": 1 }, ... }
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> Signals { get; set; } = new();

    /// <summary>
    /// Products viewed in this session (for "recently viewed").
    /// </summary>
    public List<int> ViewedProducts { get; set; } = new();

    /// <summary>
    /// Session context (device type, entry point, etc.)
    /// </summary>
    public SessionContext? Context { get; set; }

    // ============ AGGREGATES ============

    public double TotalWeight { get; set; }

    public int SignalCount { get; set; }

    public int PageViews { get; set; }

    public int ProductViews { get; set; }

    public int CartAdds { get; set; }

    // ============ TIMESTAMPS ============

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether high-value signals have been elevated to persistent profile.
    /// </summary>
    public bool IsElevated { get; set; }
}

/// <summary>
/// Session context - Zero PII, just device/behavior patterns.
/// </summary>
public class SessionContext
{
    /// <summary>
    /// Device type: mobile, desktop, tablet
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// Entry page path (no query params)
    /// </summary>
    public string? EntryPath { get; set; }
    
    /// <summary>
    /// Referrer domain only (not full URL)
    /// </summary>
    public string? ReferrerDomain { get; set; }
    
    /// <summary>
    /// Time of day bucket: morning, afternoon, evening, night
    /// </summary>
    public string? TimeOfDay { get; set; }
    
    /// <summary>
    /// Day of week: weekday, weekend
    /// </summary>
    public string? DayType { get; set; }
}
