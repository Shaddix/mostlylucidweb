using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("sellers")]
public class SellerEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(200)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(200)]
    [Column("phone")]
    public string? Phone { get; set; }

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

    // Navigation property for products
    public List<ProductEntity> Products { get; set; } = [];
}