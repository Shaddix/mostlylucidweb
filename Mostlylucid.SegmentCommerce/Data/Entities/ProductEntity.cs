using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("products")]
public class ProductEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("original_price", TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    [MaxLength(500)]
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("category_path", TypeName = "ltree")]
    public string CategoryPath { get; set; } = string.Empty;

    [Column("tags")]
    public List<string> Tags { get; set; } = [];

    // [Column("search_vector", TypeName = "tsvector")]
    // public string? SearchVector { get; set; }

    [Column("is_trending")]
    public bool IsTrending { get; set; }

    [Column("is_featured")]
    public bool IsFeatured { get; set; }

    // Seller relationship
    [Required]
    [Column("seller_id")]
    public int SellerId { get; set; }

    // Constrained attributes
    [MaxLength(50)]
    [Column("color")]
    public string? Color { get; set; }

    [MaxLength(50)]
    [Column("size")]
    public string? Size { get; set; }

    // Navigation property for seller
    public SellerEntity Seller { get; set; } = null!;

    // Product variations (for different colors/sizes)
    [Column("variations")]
    public List<ProductVariationEntity> Variations { get; set; } = [];

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
