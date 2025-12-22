using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

[Table("anonymous_profiles")]
public class AnonymousProfileEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    [Column("profile_key")]
    public string ProfileKey { get; set; } = string.Empty;

    [Column("total_weight")]
    public double TotalWeight { get; set; }

    [Column("signal_count")]
    public int SignalCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProfileKeyEntity> Keys { get; set; } = new List<ProfileKeyEntity>();

    public ICollection<SessionProfileEntity> Sessions { get; set; } = new List<SessionProfileEntity>();

    public ICollection<InterestScoreEntity> InterestScores { get; set; } = new List<InterestScoreEntity>();
}
