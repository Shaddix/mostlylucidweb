using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("product_taxonomy")]
public class ProductTaxonomyEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Required]
    [Column("taxonomy_node_id")]
    public int TaxonomyNodeId { get; set; }

    public ProductEntity Product { get; set; } = null!;
    public TaxonomyNodeEntity TaxonomyNode { get; set; } = null!;
}
