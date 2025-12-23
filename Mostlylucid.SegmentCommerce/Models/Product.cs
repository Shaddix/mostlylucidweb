namespace Mostlylucid.SegmentCommerce.Models;

public class ProductVariation
{
    public int Id { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? Sku { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool IsRecommended { get; set; }
    public bool IsTrending { get; set; }
    public double RelevanceScore { get; set; }
    public int SellerId { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public List<ProductVariation> Variations { get; set; } = [];
}

public class CartItem
{
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int? VariationId { get; set; }
    public ProductVariation? Variation { get; set; }
    
    /// <summary>
    /// Get the effective price (variation price if selected, otherwise product price).
    /// </summary>
    public decimal UnitPrice => Variation?.Price ?? Product.Price;
    
    /// <summary>
    /// Line total for this cart item.
    /// </summary>
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// Cart session data stored in session (without hydrated products).
/// </summary>
public class CartSessionData
{
    public List<CartItemData> Items { get; set; } = [];
}

public class CartItemData
{
    public int ProductId { get; set; }
    public int? VariationId { get; set; }
    public int Quantity { get; set; }
}

public class Cart
{
    public List<CartItem> Items { get; set; } = [];
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
    public decimal Total => Subtotal; // Can add shipping/tax later
    public int ItemCount => Items.Sum(i => i.Quantity);
}
