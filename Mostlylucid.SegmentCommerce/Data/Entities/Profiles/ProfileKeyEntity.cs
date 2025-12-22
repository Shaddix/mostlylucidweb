using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

[Table("profile_keys")]
public class ProfileKeyEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("profile_id")]
    public Guid ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public AnonymousProfileEntity Profile { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    [Column("key_hash")]
    public string KeyHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("derivation_method")]
    public string DerivationMethod { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("source_hint")]
    public string? SourceHint { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
