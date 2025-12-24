using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

/// <summary>
/// Root model for Shopify taxonomy attributes.json
/// </summary>
public class ShopifyAttributesRoot
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<ShopifyAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Get attribute by handle.
    /// </summary>
    public ShopifyAttribute? GetByHandle(string handle) =>
        Attributes.FirstOrDefault(a => a.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get attribute by ID.
    /// </summary>
    public ShopifyAttribute? GetById(string id) =>
        Attributes.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Get random values for an attribute.
    /// </summary>
    public List<ShopifyAttributeValue> GetRandomValues(string handle, int count, Random? random = null)
    {
        random ??= Random.Shared;
        var attr = GetByHandle(handle);
        if (attr == null || attr.Values.Count == 0)
            return [];

        return attr.Values
            .OrderBy(_ => random.Next())
            .Take(Math.Min(count, attr.Values.Count))
            .ToList();
    }
}

/// <summary>
/// A product attribute in the Shopify taxonomy.
/// </summary>
public class ShopifyAttribute
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("extended_attributes")]
    public List<ShopifyExtendedAttribute> ExtendedAttributes { get; set; } = [];

    [JsonPropertyName("values")]
    public List<ShopifyAttributeValue> Values { get; set; } = [];

    /// <summary>
    /// Get a random value for this attribute.
    /// </summary>
    public ShopifyAttributeValue? GetRandomValue(Random? random = null)
    {
        random ??= Random.Shared;
        return Values.Count > 0 ? Values[random.Next(Values.Count)] : null;
    }
}

/// <summary>
/// Extended attribute reference.
/// </summary>
public class ShopifyExtendedAttribute
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;
}

/// <summary>
/// A value for a product attribute.
/// </summary>
public class ShopifyAttributeValue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("handle")]
    public string Handle { get; set; } = string.Empty;
}

/// <summary>
/// Root model for standalone attribute_values.json (flat list of all values)
/// </summary>
public class ShopifyAttributeValuesRoot
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<ShopifyAttributeValue> Values { get; set; } = [];
}
