namespace Mostlylucid.SegmentCommerce.Dtos;

public enum ProductStatus
{
    Draft,
    Active,
    Archived
}

public enum AvailabilityStatus
{
    InStock,
    LowStock,
    OutOfStock,
    Preorder
}

public record SellerDto(
    Guid Id,
    string Name,
    double Rating,
    int ReviewCount,
    bool IsVerified,
    string? LogoUrl);

public record ProductVariationDto(
    int Id,
    string Color,
    string Size,
    decimal Price,
    decimal? OriginalPrice,
    decimal? CompareAtPrice,
    string? ImageUrl,
    string? Sku,
    string? Gtin,
    string? Barcode,
    int StockQuantity,
    AvailabilityStatus AvailabilityStatus);

public record TaxonomyNodeDto(
    int Id,
    string Name,
    string Handle,
    string Path,
    string? ShopifyTaxonomyId);

public record StoreDto(
    int Id,
    string Name,
    string Slug,
    bool IsActive);

public record ProductDto
{
    public int Id { get; init; }
    public string Handle { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal? OriginalPrice { get; init; }
    public decimal? CompareAtPrice { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string CategoryPath { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];
    public ProductStatus Status { get; init; } = ProductStatus.Active;
    public DateTime? PublishedAt { get; init; }
    public string? Brand { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public bool IsTrending { get; init; }
    public bool IsFeatured { get; init; }
    public SellerDto? Seller { get; init; }
    public List<ProductVariationDto> Variations { get; init; } = [];
    public List<TaxonomyNodeDto> Taxonomy { get; init; } = [];
    public List<StoreDto> Stores { get; init; } = [];
}
