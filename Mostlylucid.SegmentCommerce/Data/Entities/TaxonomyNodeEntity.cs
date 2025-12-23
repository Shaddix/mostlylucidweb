using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

[Table("taxonomy_nodes")]
public class TaxonomyNodeEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [MaxLength(120)]
    [Column("handle")]
    public string Handle { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("shopify_taxonomy_id")]
    public string? ShopifyTaxonomyId { get; set; }

    [Column("path", TypeName = "ltree")]
    public string Path { get; set; } = string.Empty;

    [Column("attributes", TypeName = "jsonb")]
    public TaxonomyAttributes? Attributes { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    public int? ParentId { get; set; }
    public TaxonomyNodeEntity? Parent { get; set; }
    public List<TaxonomyNodeEntity> Children { get; set; } = [];

    public List<ProductTaxonomyEntity> ProductTaxonomy { get; set; } = [];
}
