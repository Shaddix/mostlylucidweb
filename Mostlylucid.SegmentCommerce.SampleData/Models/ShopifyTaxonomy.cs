using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

/// <summary>
/// Root model for Shopify taxonomy categories.json
/// </summary>
public class ShopifyTaxonomy
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("verticals")]
    public List<ShopifyVertical> Verticals { get; set; } = [];

    /// <summary>
    /// Get all categories flattened across all verticals.
    /// </summary>
    public IEnumerable<ShopifyCategory> GetAllCategories()
    {
        foreach (var vertical in Verticals)
        {
            foreach (var category in vertical.Categories)
            {
                yield return category;
            }
        }
    }

    /// <summary>
    /// Get all leaf categories (categories with no children).
    /// </summary>
    public IEnumerable<ShopifyCategory> GetLeafCategories()
    {
        return GetAllCategories().Where(c => c.Children.Count == 0);
    }

    /// <summary>
    /// Get random categories for product generation.
    /// </summary>
    public List<ShopifyCategory> GetRandomCategories(int count, Random? random = null)
    {
        random ??= Random.Shared;
        var leaves = GetLeafCategories().ToList();
        
        if (leaves.Count <= count)
            return leaves;

        return leaves
            .OrderBy(_ => random.Next())
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get a random category from a specific vertical.
    /// </summary>
    public ShopifyCategory? GetRandomCategoryFromVertical(string verticalName, Random? random = null)
    {
        random ??= Random.Shared;
        var vertical = Verticals.FirstOrDefault(v => 
            v.Name.Equals(verticalName, StringComparison.OrdinalIgnoreCase));
        
        if (vertical == null)
            return null;

        var leaves = vertical.Categories.Where(c => c.Children.Count == 0).ToList();
        return leaves.Count > 0 ? leaves[random.Next(leaves.Count)] : null;
    }
}

/// <summary>
/// Top-level vertical (e.g., "Animals & Pet Supplies", "Electronics", etc.)
/// </summary>
public class ShopifyVertical
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<ShopifyCategory> Categories { get; set; } = [];
}

/// <summary>
/// Individual category in the Shopify taxonomy.
/// </summary>
public class ShopifyCategory
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("attributes")]
    public List<ShopifyCategoryAttribute> Attributes { get; set; } = [];

    [JsonPropertyName("children")]
    public List<ShopifyCategoryChild> Children { get; set; } = [];

    [JsonPropertyName("ancestors")]
    public List<ShopifyCategoryAncestor> Ancestors { get; set; } = [];

    /// <summary>
    /// Get the category path as segments (e.g., ["Electronics", "Computers", "Laptops"])
    /// </summary>
    public string[] GetPathSegments() => 
        FullName.Split(" > ", StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Get the top-level category name.
    /// </summary>
    public string GetTopCategory()
    {
        var segments = GetPathSegments();
        return segments.Length > 0 ? segments[0] : Name;
    }

    /// <summary>
    /// Generate a slug from the category name.
    /// </summary>
    public string GetSlug() => 
        Name.ToLowerInvariant()
            .Replace(" & ", "-and-")
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace(",", "");
}

/// <summary>
/// Attribute associated with a category.
/// </summary>
public class ShopifyCategoryAttribute
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("extended")]
    public bool Extended { get; set; }
}

/// <summary>
/// Child category reference.
/// </summary>
public class ShopifyCategoryChild
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Ancestor category reference.
/// </summary>
public class ShopifyCategoryAncestor
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
