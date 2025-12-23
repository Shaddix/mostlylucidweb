namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Metadata for order entities. Contains additional order context
/// that doesn't fit in the main entity structure.
/// </summary>
public class OrderMetadata
{
    /// <summary>
    /// UTM campaign source that led to this order.
    /// </summary>
    public string? UtmSource { get; set; }
    
    /// <summary>
    /// UTM campaign medium.
    /// </summary>
    public string? UtmMedium { get; set; }
    
    /// <summary>
    /// UTM campaign name.
    /// </summary>
    public string? UtmCampaign { get; set; }
    
    /// <summary>
    /// Discount/coupon code applied.
    /// </summary>
    public string? CouponCode { get; set; }
    
    /// <summary>
    /// Affiliate or referral code.
    /// </summary>
    public string? AffiliateCode { get; set; }
    
    /// <summary>
    /// Customer notes or special instructions.
    /// </summary>
    public string? CustomerNotes { get; set; }
    
    /// <summary>
    /// Gift message if order is a gift.
    /// </summary>
    public string? GiftMessage { get; set; }
    
    /// <summary>
    /// Whether the order is a gift.
    /// </summary>
    public bool IsGift { get; set; }
    
    /// <summary>
    /// Device type used to place order (desktop, mobile, tablet).
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// Browser or app used.
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Metadata for interaction events. Contains context about the
/// interaction that doesn't fit in fixed columns.
/// </summary>
public class InteractionMetadata
{
    /// <summary>
    /// Search query that led to this interaction.
    /// </summary>
    public string? SearchQuery { get; set; }
    
    /// <summary>
    /// Position in search results or product list.
    /// </summary>
    public int? Position { get; set; }
    
    /// <summary>
    /// Scroll depth percentage (0-100).
    /// </summary>
    public int? ScrollDepth { get; set; }
    
    /// <summary>
    /// Time spent on page in seconds.
    /// </summary>
    public int? TimeOnPageSeconds { get; set; }
    
    /// <summary>
    /// Device type (desktop, mobile, tablet).
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// Viewport width in pixels.
    /// </summary>
    public int? ViewportWidth { get; set; }
    
    /// <summary>
    /// Viewport height in pixels.
    /// </summary>
    public int? ViewportHeight { get; set; }
    
    /// <summary>
    /// Source of the interaction (organic, paid, email, social).
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// Cart quantity when adding to cart.
    /// </summary>
    public int? Quantity { get; set; }
    
    /// <summary>
    /// Price at time of interaction.
    /// </summary>
    public decimal? Price { get; set; }
    
    /// <summary>
    /// Product variant selected (size, color, etc.).
    /// </summary>
    public string? VariantInfo { get; set; }
}

/// <summary>
/// Context data for behavioral signals. Contains additional detail
/// about the circumstances of a signal capture.
/// </summary>
public class SignalContext
{
    /// <summary>
    /// Scroll depth percentage (0-100).
    /// </summary>
    public int? ScrollDepth { get; set; }
    
    /// <summary>
    /// Time spent on page in seconds.
    /// </summary>
    public int? TimeOnPageSeconds { get; set; }
    
    /// <summary>
    /// Mouse hover duration in milliseconds.
    /// </summary>
    public int? HoverDurationMs { get; set; }
    
    /// <summary>
    /// Number of clicks in this session.
    /// </summary>
    public int? ClickCount { get; set; }
    
    /// <summary>
    /// Whether user interacted with product images.
    /// </summary>
    public bool ViewedImages { get; set; }
    
    /// <summary>
    /// Whether user read reviews.
    /// </summary>
    public bool ViewedReviews { get; set; }
    
    /// <summary>
    /// Whether user compared products.
    /// </summary>
    public bool ComparedProducts { get; set; }
    
    /// <summary>
    /// Search query that led here.
    /// </summary>
    public string? SearchQuery { get; set; }
    
    /// <summary>
    /// Filter or sort options applied.
    /// </summary>
    public string? AppliedFilters { get; set; }
    
    /// <summary>
    /// Price point viewed or interacted with.
    /// </summary>
    public decimal? PricePoint { get; set; }
    
    /// <summary>
    /// Device type.
    /// </summary>
    public string? DeviceType { get; set; }
}

/// <summary>
/// Attributes for taxonomy nodes. Contains extensible metadata
/// for product categories and taxonomy.
/// </summary>
public class TaxonomyAttributes
{
    /// <summary>
    /// Icon name or CSS class for display.
    /// </summary>
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display color (hex code).
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// SEO meta description.
    /// </summary>
    public string? MetaDescription { get; set; }
    
    /// <summary>
    /// SEO meta keywords.
    /// </summary>
    public string? MetaKeywords { get; set; }
    
    /// <summary>
    /// Banner image URL for category pages.
    /// </summary>
    public string? BannerImageUrl { get; set; }
    
    /// <summary>
    /// Whether this category is featured.
    /// </summary>
    public bool IsFeatured { get; set; }
    
    /// <summary>
    /// Whether this category is seasonal.
    /// </summary>
    public bool IsSeasonal { get; set; }
    
    /// <summary>
    /// Season this category is relevant for (if seasonal).
    /// </summary>
    public string? Season { get; set; }
    
    /// <summary>
    /// Target demographic for this category.
    /// </summary>
    public string? TargetDemographic { get; set; }
    
    /// <summary>
    /// Average price point for products in this category.
    /// </summary>
    public decimal? AveragePricePoint { get; set; }
}
