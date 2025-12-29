using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Core user entity. A user can become a seller by having a SellerProfile.
/// </summary>
[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("email_verified")]
    public bool EmailVerified { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    // Navigation: optional seller profile (1:0..1)
    public SellerProfileEntity? SellerProfile { get; set; }

    // Navigation: stores this user manages
    public List<StoreUserEntity> StoreUsers { get; set; } = [];

    // Navigation: products this user sells (only if they have a SellerProfile)
    public List<ProductEntity> Products { get; set; } = [];

    /// <summary>
    /// Convenience property to check if user is a seller.
    /// </summary>
    [NotMapped]
    public bool IsSeller => SellerProfile != null;
}
