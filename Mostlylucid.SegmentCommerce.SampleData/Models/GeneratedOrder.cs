using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

/// <summary>
/// Generated order for sample data.
/// Customer details are fake (Bogus) and only used for demo checkout simulation.
/// </summary>
public class GeneratedOrder
{
    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("profile_key")]
    public string? ProfileKey { get; set; }

    [JsonPropertyName("session_key")]
    public string? SessionKey { get; set; }

    // Fake customer data (for demo checkout display only)
    [JsonPropertyName("customer")]
    public GeneratedCheckoutCustomer Customer { get; set; } = new();

    // Order items
    [JsonPropertyName("items")]
    public List<GeneratedOrderItem> Items { get; set; } = [];

    // Totals
    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("shipping_cost")]
    public decimal ShippingCost { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    // Payment
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = "CreditCard";

    [JsonPropertyName("payment_status")]
    public string PaymentStatus { get; set; } = "Captured";

    // Status
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Completed";

    [JsonPropertyName("fulfillment_status")]
    public string FulfillmentStatus { get; set; } = "Fulfilled";

    // Shipping
    [JsonPropertyName("shipping_country")]
    public string? ShippingCountry { get; set; }

    [JsonPropertyName("shipping_region")]
    public string? ShippingRegion { get; set; }

    [JsonPropertyName("shipping_method")]
    public string? ShippingMethod { get; set; }

    // Timestamps
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Fake customer data for checkout simulation.
/// </summary>
public class GeneratedCheckoutCustomer
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("shipping_address")]
    public GeneratedAddress ShippingAddress { get; set; } = new();

    [JsonPropertyName("billing_same_as_shipping")]
    public bool BillingSameAsShipping { get; set; } = true;

    [JsonPropertyName("billing_address")]
    public GeneratedAddress? BillingAddress { get; set; }

    [JsonPropertyName("payment")]
    public GeneratedPayment Payment { get; set; } = new();
}

/// <summary>
/// Fake address for checkout simulation.
/// </summary>
public class GeneratedAddress
{
    [JsonPropertyName("line1")]
    public string Line1 { get; set; } = string.Empty;

    [JsonPropertyName("line2")]
    public string? Line2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = string.Empty;
}

/// <summary>
/// Fake payment data for checkout simulation.
/// </summary>
public class GeneratedPayment
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "card";

    [JsonPropertyName("card_brand")]
    public string? CardBrand { get; set; }

    [JsonPropertyName("card_last_four")]
    public string? CardLastFour { get; set; }

    [JsonPropertyName("card_expiry")]
    public string? CardExpiry { get; set; }

    [JsonPropertyName("cardholder_name")]
    public string? CardholderName { get; set; }
}

/// <summary>
/// Order line item.
/// </summary>
public class GeneratedOrderItem
{
    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("product_image_url")]
    public string? ProductImageUrl { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("original_price")]
    public decimal? OriginalPrice { get; set; }

    [JsonPropertyName("discount_amount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }
}
