using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Represents a persistent visitor profile (anonymous by default).
/// This is the database representation of an interest signature that the user
/// has chosen to persist across sessions.
/// </summary>
[Table("visitor_profiles")]
public class VisitorProfileEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// A token stored in a first-party cookie to identify this profile.
    /// Not linked to any PII unless explicitly unmasked.
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("profile_token")]
    public string ProfileToken { get; set; } = string.Empty;

    /// <summary>
    /// The interest weights stored as JSONB for flexible schema.
    /// </summary>
    [Column("interests", TypeName = "jsonb")]
    public Dictionary<string, InterestWeightData> Interests { get; set; } = new();

    [Column("is_unmasked")]
    public bool IsUnmasked { get; set; }

    /// <summary>
    /// Optional: email if the user has unmasked.
    /// </summary>
    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("total_visits")]
    public int TotalVisits { get; set; } = 1;
}

/// <summary>
/// Interest weight data for JSONB storage.
/// </summary>
public class InterestWeightData
{
    public double Weight { get; set; }
    public DateTime LastReinforced { get; set; } = DateTime.UtcNow;
    public int ReinforcementCount { get; set; }
    public double DecayRate { get; set; } = 0.1;
}
