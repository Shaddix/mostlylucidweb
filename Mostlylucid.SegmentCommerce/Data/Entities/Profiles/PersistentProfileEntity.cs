using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// Long-term profile built from elevated session signals.
/// Zero PII - only behavioral data and computed segments.
/// Used for product matching and personalization.
/// </summary>
[Table("persistent_profiles")]
public class PersistentProfileEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Primary identification key (fingerprint hash, cookie ID, or user ID).
    /// </summary>
    [Required]
    [MaxLength(256)]
    [Column("profile_key")]
    public string ProfileKey { get; set; } = string.Empty;

    /// <summary>
    /// How this profile is identified.
    /// </summary>
    [Column("identification_mode")]
    public ProfileIdentificationMode IdentificationMode { get; set; }

    // ============ BEHAVIORAL DATA (JSONB + GIN indexed) ============

    /// <summary>
    /// Category interest scores: { "tech": 0.85, "fashion": 0.3, ... }
    /// Computed from accumulated signals, decays over time.
    /// </summary>
    [Column("interests", TypeName = "jsonb")]
    public Dictionary<string, double> Interests { get; set; } = new();

    /// <summary>
    /// Subcategory/tag affinities: { "headphones": 0.9, "mechanical-keyboards": 0.7, ... }
    /// More granular than category interests.
    /// </summary>
    [Column("affinities", TypeName = "jsonb")]
    public Dictionary<string, double> Affinities { get; set; } = new();

    /// <summary>
    /// Brand preferences: { "Sony": 0.8, "Apple": 0.6, ... }
    /// </summary>
    [Column("brand_affinities", TypeName = "jsonb")]
    public Dictionary<string, double> BrandAffinities { get; set; } = new();

    /// <summary>
    /// Price range preferences: { "min": 50, "max": 500, "avg": 150, "luxury": false }
    /// </summary>
    [Column("price_preferences", TypeName = "jsonb")]
    public PricePreferences? PricePreferences { get; set; }

    /// <summary>
    /// Behavioral traits: { "browses_extensively": true, "quick_buyer": false, ... }
    /// </summary>
    [Column("traits", TypeName = "jsonb")]
    public Dictionary<string, bool> Traits { get; set; } = new();

    // ============ COMPUTED SEGMENTS ============

    /// <summary>
    /// Bitflags for computed segments (fast filtering).
    /// </summary>
    [Column("segments")]
    public ProfileSegments Segments { get; set; } = ProfileSegments.None;

    /// <summary>
    /// LLM-generated segment labels with confidence: { "tech-enthusiast": 0.9, "bargain-hunter": 0.7 }
    /// </summary>
    [Column("llm_segments", TypeName = "jsonb")]
    public Dictionary<string, double>? LlmSegments { get; set; }

    /// <summary>
    /// When segments were last computed.
    /// </summary>
    [Column("segments_computed_at")]
    public DateTime? SegmentsComputedAt { get; set; }

    // ============ EMBEDDING FOR SIMILARITY ============

    /// <summary>
    /// Vector embedding of profile for similarity matching with products.
    /// Computed from interests, affinities, and traits.
    /// </summary>
    [Column("embedding", TypeName = "vector(384)")]
    public Vector? Embedding { get; set; }

    [Column("embedding_computed_at")]
    public DateTime? EmbeddingComputedAt { get; set; }

    // ============ STATISTICS ============

    [Column("total_sessions")]
    public int TotalSessions { get; set; }

    [Column("total_signals")]
    public int TotalSignals { get; set; }

    [Column("total_purchases")]
    public int TotalPurchases { get; set; }

    [Column("total_cart_adds")]
    public int TotalCartAdds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ============ NAVIGATION ============

    public ICollection<ProfileKeyEntity> AlternateKeys { get; set; } = new List<ProfileKeyEntity>();
    public ICollection<SessionProfileEntity> Sessions { get; set; } = new List<SessionProfileEntity>();
}

/// <summary>
/// Price preferences - Zero PII, just behavioral patterns.
/// </summary>
public class PricePreferences
{
    public decimal? MinObserved { get; set; }
    public decimal? MaxObserved { get; set; }
    public decimal? AveragePurchase { get; set; }
    public decimal? MedianViewed { get; set; }
    public bool PrefersDeals { get; set; }
    public bool PrefersLuxury { get; set; }
}
