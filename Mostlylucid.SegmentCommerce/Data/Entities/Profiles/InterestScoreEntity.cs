using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

[Table("interest_scores")]
public class InterestScoreEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("profile_id")]
    public Guid? ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public AnonymousProfileEntity? Profile { get; set; }

    [Column("session_id")]
    public Guid? SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public SessionProfileEntity? Session { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("score")]
    public double Score { get; set; }

    [Column("decay_rate")]
    public double DecayRate { get; set; } = 0.02;

    [Column("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
