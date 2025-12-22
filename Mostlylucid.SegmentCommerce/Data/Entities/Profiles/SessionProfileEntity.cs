using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// Ephemeral session profile - collects signals during a visit.
/// Expires with session. High-value signals elevate to persistent profile.
/// Zero PII - only behavioral signals.
/// </summary>
[Table("session_profiles")]
public class SessionProfileEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Session identifier (ASP.NET session ID or fingerprint-derived).
    /// </summary>
    [Required]
    [MaxLength(128)]
    [Column("session_key")]
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// Link to persistent profile (if resolved).
    /// </summary>
    [Column("persistent_profile_id")]
    public Guid? PersistentProfileId { get; set; }

    [ForeignKey(nameof(PersistentProfileId))]
    public PersistentProfileEntity? PersistentProfile { get; set; }

    /// <summary>
    /// How this session links to persistent profile.
    /// </summary>
    [Column("identification_mode")]
    public ProfileIdentificationMode IdentificationMode { get; set; } = ProfileIdentificationMode.None;

    // ============ BEHAVIORAL SIGNALS (JSONB) ============

    /// <summary>
    /// Category interest scores for this session: { "tech": 0.75, "fashion": 0.25 }
    /// </summary>
    [Column("interests", TypeName = "jsonb")]
    public Dictionary<string, double> Interests { get; set; } = new();

    /// <summary>
    /// Detailed signals by category and type.
    /// Structure: { "tech": { "product_view": 5, "add_to_cart": 1 }, ... }
    /// </summary>
    [Column("signals", TypeName = "jsonb")]
    public Dictionary<string, Dictionary<string, int>> Signals { get; set; } = new();

    /// <summary>
    /// Products viewed in this session (for "recently viewed").
    /// </summary>
    [Column("viewed_products", TypeName = "jsonb")]
    public List<int> ViewedProducts { get; set; } = new();

    /// <summary>
    /// Session context (device type, entry point, etc.)
    /// </summary>
    [Column("context", TypeName = "jsonb")]
    public SessionContext? Context { get; set; }

    // ============ AGGREGATES ============

    [Column("total_weight")]
    public double TotalWeight { get; set; }

    [Column("signal_count")]
    public int SignalCount { get; set; }

    [Column("page_views")]
    public int PageViews { get; set; }

    [Column("product_views")]
    public int ProductViews { get; set; }

    [Column("cart_adds")]
    public int CartAdds { get; set; }

    // ============ TIMESTAMPS ============

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("last_activity_at")]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether high-value signals have been elevated to persistent profile.
    /// </summary>
    [Column("is_elevated")]
    public bool IsElevated { get; set; }

    // Navigation
    public ICollection<SignalEntity> SignalHistory { get; set; } = new List<SignalEntity>();
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
