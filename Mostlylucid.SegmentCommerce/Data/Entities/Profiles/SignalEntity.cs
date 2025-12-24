using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// A behavioural signal captured from user activity.
/// Signals are the atomic unit of user behaviour tracking.
/// </summary>
[Table("signals")]
public class SignalEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// The session this signal belongs to.
    /// Note: SessionId is kept for analytics but FK is removed since sessions are now in-memory only.
    /// </summary>
    [Column("session_id")]
    public Guid SessionId { get; set; }

    /// <summary>
    /// Type of signal (view, click, scroll, add_to_cart, purchase, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("signal_type")]
    public string SignalType { get; set; } = string.Empty;

    /// <summary>
    /// Category the signal relates to (e.g., "tech", "fashion").
    /// </summary>
    [MaxLength(50)]
    [Column("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Product ID if signal relates to a specific product.
    /// </summary>
    [Column("product_id")]
    public int? ProductId { get; set; }

    /// <summary>
    /// The raw weight/strength of this signal (before decay).
    /// Different signal types have different base weights.
    /// </summary>
    [Column("weight")]
    public double Weight { get; set; }

    /// <summary>
    /// Additional context as JSONB (scroll depth, time on page, etc.)
    /// </summary>
    [Column("context", TypeName = "jsonb")]
    public SignalContext? Context { get; set; }

    /// <summary>
    /// Page/URL where the signal was captured.
    /// </summary>
    [MaxLength(500)]
    [Column("page_url")]
    public string? PageUrl { get; set; }

    /// <summary>
    /// Referrer URL.
    /// </summary>
    [MaxLength(500)]
    [Column("referrer")]
    public string? Referrer { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Known signal types and their base weights.
/// </summary>
public static class SignalTypes
{
    // Passive signals (low intent)
    public const string PageView = "page_view";
    public const string CategoryBrowse = "category_browse";
    public const string ProductImpression = "product_impression";
    public const string Scroll = "scroll";

    // Active signals (medium intent)
    public const string ProductView = "product_view";
    public const string ProductClick = "product_click";
    public const string Search = "search";
    public const string FilterApplied = "filter_applied";
    public const string CompareProducts = "compare_products";

    // High-intent signals
    public const string AddToCart = "add_to_cart";
    public const string AddToWishlist = "add_to_wishlist";
    public const string RemoveFromCart = "remove_from_cart";
    public const string ViewCart = "view_cart";
    public const string BeginCheckout = "begin_checkout";

    // Conversion signals (highest intent)
    public const string Purchase = "purchase";
    public const string Review = "review";
    public const string Share = "share";

    // Engagement signals
    public const string TimeOnPage = "time_on_page";
    public const string Return = "return_visit";

    /// <summary>
    /// Base weights for each signal type.
    /// These are multiplied by context factors.
    /// </summary>
    public static readonly Dictionary<string, double> BaseWeights = new()
    {
        // Passive (0.01 - 0.05)
        { PageView, 0.01 },
        { CategoryBrowse, 0.03 },
        { ProductImpression, 0.02 },
        { Scroll, 0.01 },

        // Active (0.05 - 0.15)
        { ProductView, 0.10 },
        { ProductClick, 0.08 },
        { Search, 0.05 },
        { FilterApplied, 0.05 },
        { CompareProducts, 0.12 },

        // High-intent (0.15 - 0.40)
        { AddToCart, 0.35 },
        { AddToWishlist, 0.25 },
        { RemoveFromCart, -0.10 }, // Negative signal
        { ViewCart, 0.15 },
        { BeginCheckout, 0.40 },

        // Conversion (0.50 - 1.00)
        { Purchase, 1.00 },
        { Review, 0.60 },
        { Share, 0.50 },

        // Engagement
        { TimeOnPage, 0.05 }, // Multiplied by time factor
        { Return, 0.20 }
    };

    /// <summary>
    /// Get the base weight for a signal type.
    /// </summary>
    public static double GetBaseWeight(string signalType)
    {
        return BaseWeights.GetValueOrDefault(signalType, 0.05);
    }
}
