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
}

public class Cart
{
    public List<CartItem> Items { get; set; } = [];
    public decimal Total => Items.Sum(i => i.Product.Price * i.Quantity);
    public int ItemCount => Items.Sum(i => i.Quantity);
}
