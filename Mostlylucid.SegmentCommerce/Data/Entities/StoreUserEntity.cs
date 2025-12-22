using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("store_users")]
public class StoreUserEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    // Reference to an external user identity (string to avoid coupling)
    [Required]
    [MaxLength(200)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = "admin";

    public StoreEntity Store { get; set; } = null!;
}
