using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("store_products")]
public class StoreProductEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("store_id")]
    public int StoreId { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    public StoreEntity Store { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
}
