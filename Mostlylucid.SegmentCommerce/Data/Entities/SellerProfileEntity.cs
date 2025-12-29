using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Seller profile - extends a User with seller-specific information.
/// Uses shared primary key pattern (UserId is both PK and FK).
/// </summary>
[Table("seller_profiles")]
public class SellerProfileEntity
{
    /// <summary>
    /// Shared primary key with UserEntity (1:1 relationship).
    /// </summary>
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    /// <summary>
    /// Business/store name (may differ from user's display name).
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("business_name")]
    public string BusinessName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Column("website")]
    public string? Website { get; set; }

    [MaxLength(500)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    [Column("address")]
    public string? Address { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("rating")]
    public double Rating { get; set; }

    [Column("review_count")]
    public int ReviewCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property back to user
    [ForeignKey(nameof(UserId))]
    public UserEntity User { get; set; } = null!;
}
