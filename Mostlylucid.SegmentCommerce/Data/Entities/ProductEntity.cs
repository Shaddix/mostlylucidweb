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

    [Required]
    [MaxLength(120)]
    [Column("handle")]
    public string Handle { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("original_price", TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    [Column("compare_at_price", TypeName = "decimal(18,2)")]
    public decimal? CompareAtPrice { get; set; }

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

    [Column("status")]
    public ProductStatus Status { get; set; } = ProductStatus.Active;

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [MaxLength(120)]
    [Column("brand")]
    public string? Brand { get; set; }

    [MaxLength(180)]
    [Column("seo_title")]
    public string? SeoTitle { get; set; }

    [MaxLength(320)]
    [Column("seo_description")]
    public string? SeoDescription { get; set; }

    [Column("is_trending")]
    public bool IsTrending { get; set; }

    [Column("is_featured")]
    public bool IsFeatured { get; set; }

    // Seller relationship
    [Required]
    [Column("seller_id")]
    public int SellerId { get; set; }

    // Constrained attributes (default variant shortcut)
    [MaxLength(50)]
    [Column("color")]
    public string? Color { get; set; }

    [MaxLength(50)]
    [Column("size")]
    public string? Size { get; set; }

    [MaxLength(100)]
    [Column("subcategory")]
    public string? Subcategory { get; set; }

    // Navigation property for seller
    public SellerEntity Seller { get; set; } = null!;

    // Product variations (for different colors/sizes)
    public List<ProductVariationEntity> Variations { get; set; } = [];

    // Taxonomy mapping
    public List<ProductTaxonomyEntity> ProductTaxonomy { get; set; } = [];

    // Store mapping
    public List<StoreProductEntity> StoreProducts { get; set; } = [];

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
