using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("product_variations")]
public class ProductVariationEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("color")]
    public string Color { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("size")]
    public string Size { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column("original_price", TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    [Column("compare_at_price", TypeName = "decimal(18,2)")]
    public decimal? CompareAtPrice { get; set; }

    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("sku")]
    public string? Sku { get; set; }

    [MaxLength(64)]
    [Column("gtin")]
    public string? Gtin { get; set; }

    [MaxLength(64)]
    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("stock_quantity")]
    public int StockQuantity { get; set; }

    [Column("availability_status")]
    public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.InStock;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ProductEntity Product { get; set; } = null!;
}
