using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Join entity linking Users to Stores with a specific role.
/// </summary>
[Table("store_users")]
public class StoreUserEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = "admin";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public StoreEntity Store { get; set; } = null!;
    
    [ForeignKey(nameof(UserId))]
    public UserEntity User { get; set; } = null!;
}
