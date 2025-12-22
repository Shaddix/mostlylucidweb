using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

[Table("session_profiles")]
public class SessionProfileEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(128)]
    [Column("session_key")]
    public string SessionKey { get; set; } = string.Empty;

    [MaxLength(256)]
    [Column("profile_key")]
    public string? ProfileKey { get; set; }

    [Column("anonymous_profile_id")]
    public Guid? AnonymousProfileId { get; set; }

    [ForeignKey(nameof(AnonymousProfileId))]
    public AnonymousProfileEntity? AnonymousProfile { get; set; }

    [Column("total_weight")]
    public double TotalWeight { get; set; }

    [Column("signal_count")]
    public int SignalCount { get; set; }

    [Column("promotion_threshold")]
    public double PromotionThreshold { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow;

    [Column("is_promoted")]
    public bool IsPromoted { get; set; }

    public ICollection<SignalEntity> Signals { get; set; } = new List<SignalEntity>();

    public ICollection<InterestScoreEntity> InterestScores { get; set; } = new List<InterestScoreEntity>();
}
