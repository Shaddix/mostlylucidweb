using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Records interaction events for analytics (views, clicks, add-to-cart, purchases).
/// These are anonymous and can be aggregated without linking to individual users.
/// </summary>
[Table("interaction_events")]
public class InteractionEventEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// The session ID (anonymous, rotates).
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional link to persistent profile.
    /// </summary>
    [Column("profile_id")]
    public Guid? ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public VisitorProfileEntity? Profile { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("product_id")]
    public int? ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public ProductEntity? Product { get; set; }

    [MaxLength(50)]
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Additional event metadata as JSONB.
    /// </summary>
    [Column("metadata", TypeName = "jsonb")]
    public InteractionMetadata? Metadata { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class EventTypes
{
    public const string View = "view";
    public const string Click = "click";
    public const string AddToCart = "add_to_cart";
    public const string RemoveFromCart = "remove_from_cart";
    public const string Purchase = "purchase";
    public const string CategoryBrowse = "category_browse";
    public const string Search = "search";
}
