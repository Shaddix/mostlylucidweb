using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Stores vector embeddings for products to enable semantic search.
/// Uses pgvector for efficient similarity queries.
/// </summary>
[Table("product_embeddings")]
public class ProductEmbeddingEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public ProductEntity Product { get; set; } = null!;

    /// <summary>
    /// The embedding vector (1536 dimensions for OpenAI, 384 for all-MiniLM-L6-v2, etc.)
    /// </summary>
    [Column("embedding", TypeName = "vector(384)")]
    public Vector Embedding { get; set; } = null!;

    /// <summary>
    /// The model used to generate this embedding.
    /// </summary>
    [MaxLength(100)]
    [Column("model")]
    public string Model { get; set; } = "all-MiniLM-L6-v2";

    /// <summary>
    /// The text that was embedded (product name + description).
    /// </summary>
    [Column("source_text")]
    public string SourceText { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stores embeddings for visitor interest signatures for similarity matching.
/// </summary>
[Table("interest_embeddings")]
public class InterestEmbeddingEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("profile_id")]
    public Guid? ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public VisitorProfileEntity? Profile { get; set; }

    /// <summary>
    /// Session ID for anonymous users without persistent profiles.
    /// </summary>
    [MaxLength(64)]
    [Column("session_id")]
    public string? SessionId { get; set; }

    /// <summary>
    /// The embedding vector representing user interests.
    /// </summary>
    [Column("embedding", TypeName = "vector(384)")]
    public Vector Embedding { get; set; } = null!;

    [MaxLength(100)]
    [Column("model")]
    public string Model { get; set; } = "all-MiniLM-L6-v2";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
