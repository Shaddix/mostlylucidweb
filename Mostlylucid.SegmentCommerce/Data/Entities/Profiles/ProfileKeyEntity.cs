using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// Alternate identification keys for a persistent profile.
/// Allows merging profiles when user upgrades identification (e.g., fingerprint → login).
/// </summary>
[Table("profile_keys")]
public class ProfileKeyEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("profile_id")]
    public Guid ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public PersistentProfileEntity Profile { get; set; } = null!;

    /// <summary>
    /// The identification key (fingerprint hash, cookie ID, or user ID).
    /// </summary>
    [Required]
    [MaxLength(256)]
    [Column("key_value")]
    public string KeyValue { get; set; } = string.Empty;

    /// <summary>
    /// Type of identification.
    /// </summary>
    [Column("key_type")]
    public ProfileIdentificationMode KeyType { get; set; }

    /// <summary>
    /// Whether this is the primary key for the profile.
    /// </summary>
    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_used_at")]
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
