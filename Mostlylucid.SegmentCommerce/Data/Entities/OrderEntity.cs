using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Order entity representing a completed checkout.
/// Customer PII is NOT stored here - it's kept in transient cache only.
/// Only the order metadata and totals are persisted.
/// </summary>
[Table("orders")]
public class OrderEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Public order number for display (e.g., "ORD-2024-001234")
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Link to the anonymous profile that placed the order.
    /// </summary>
    [Column("profile_id")]
    public Guid? ProfileId { get; set; }

    /// <summary>
    /// Session key that completed checkout (for linking to transient cache).
    /// </summary>
    [MaxLength(128)]
    [Column("session_key")]
    public string? SessionKey { get; set; }

    /// <summary>
    /// Hash of customer email for deduplication (not the actual email).
    /// </summary>
    [MaxLength(64)]
    [Column("customer_hash")]
    public string? CustomerHash { get; set; }

    // ============ ORDER TOTALS ============

    [Column("subtotal", TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column("shipping_cost", TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; }

    [Column("tax_amount", TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column("discount_amount", TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column("total", TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Number of items in the order.
    /// </summary>
    [Column("item_count")]
    public int ItemCount { get; set; }

    // ============ PAYMENT (Non-PII) ============

    [Column("payment_method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;

    [Column("payment_status")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // ============ ORDER STATUS ============

    [Column("status")]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Column("fulfillment_status")]
    public FulfillmentStatus FulfillmentStatus { get; set; } = FulfillmentStatus.Unfulfilled;

    // ============ SHIPPING (Region only, no address) ============

    [MaxLength(100)]
    [Column("shipping_country")]
    public string? ShippingCountry { get; set; }

    [MaxLength(100)]
    [Column("shipping_region")]
    public string? ShippingRegion { get; set; }

    [MaxLength(50)]
    [Column("shipping_method")]
    public string? ShippingMethod { get; set; }

    // ============ METADATA ============

    [Column("metadata", TypeName = "jsonb")]
    public Dictionary<string, object>? Metadata { get; set; }

    // ============ TIMESTAMPS ============

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // ============ NAVIGATION ============

    public List<OrderItemEntity> Items { get; set; } = [];
}

/// <summary>
/// Individual line item in an order.
/// </summary>
[Table("order_items")]
public class OrderItemEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Required]
    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("variation_id")]
    public int? VariationId { get; set; }

    /// <summary>
    /// Product name at time of purchase (denormalized for history).
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("product_image_url")]
    public string? ProductImageUrl { get; set; }

    [MaxLength(50)]
    [Column("sku")]
    public string? Sku { get; set; }

    [MaxLength(50)]
    [Column("color")]
    public string? Color { get; set; }

    [MaxLength(50)]
    [Column("size")]
    public string? Size { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Unit price at time of purchase.
    /// </summary>
    [Column("unit_price", TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Original price (for showing discount).
    /// </summary>
    [Column("original_price", TypeName = "decimal(18,2)")]
    public decimal? OriginalPrice { get; set; }

    /// <summary>
    /// Discount applied to this item.
    /// </summary>
    [Column("discount_amount", TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Line total (quantity * unit_price - discount).
    /// </summary>
    [Column("line_total", TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ============ NAVIGATION ============

    public OrderEntity Order { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;
    public ProductVariationEntity? Variation { get; set; }
}

// ============ ENUMS ============

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    PayPal,
    ApplePay,
    GooglePay,
    BankTransfer,
    CashOnDelivery
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Refunded,
    PartiallyRefunded,
    Failed,
    Cancelled
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Completed,
    Cancelled,
    Refunded
}

public enum FulfillmentStatus
{
    Unfulfilled,
    PartiallyFulfilled,
    Fulfilled,
    Returned,
    PartiallyReturned
}
