namespace Mostlylucid.AudienceSegmentation.Demo.Models;

/// <summary>
/// Represents a product in our ecommerce store
/// </summary>
public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<string> Tags { get; set; } = new();
    public string TargetAudience { get; set; } = string.Empty;

    /// <summary>
    /// Semantic embedding for this product (generated from name + description + tags)
    /// </summary>
    public float[]? Embedding { get; set; }
}
