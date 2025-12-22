using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

/// <summary>
/// Root taxonomy model loaded from gadget-taxonomy.json
/// </summary>
public class GadgetTaxonomy
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public Dictionary<string, TaxonomyCategory> Categories { get; set; } = new();

    [JsonPropertyName("globalAttributes")]
    public GlobalAttributes GlobalAttributes { get; set; } = new();

    [JsonPropertyName("imageGeneration")]
    public ImageGenerationConfig ImageGeneration { get; set; } = new();

    /// <summary>
    /// Load taxonomy from the embedded JSON file.
    /// </summary>
    public static GadgetTaxonomy Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "Data", "gadget-taxonomy.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Taxonomy file not found: {path}");
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GadgetTaxonomy>(json)
               ?? throw new InvalidOperationException("Failed to deserialize taxonomy");
    }

    /// <summary>
    /// Get all product types across all categories flattened.
    /// </summary>
    public IEnumerable<(TaxonomyCategory Category, TaxonomySubcategory Subcategory, ProductType Product)> GetAllProductTypes()
    {
        foreach (var category in Categories.Values)
        {
            foreach (var subcategory in category.Subcategories.Values)
            {
                foreach (var product in subcategory.Products)
                {
                    yield return (category, subcategory, product);
                }
            }
        }
    }

    /// <summary>
    /// Get a random product type from a specific category.
    /// </summary>
    public ProductType? GetRandomProductType(string categorySlug, Random? random = null)
    {
        random ??= Random.Shared;

        if (!Categories.TryGetValue(categorySlug, out var category))
            return null;

        var allProducts = category.Subcategories.Values
            .SelectMany(s => s.Products)
            .ToList();

        return allProducts.Count > 0
            ? allProducts[random.Next(allProducts.Count)]
            : null;
    }
}

public class TaxonomyCategory
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("priceRange")]
    public PriceRange PriceRange { get; set; } = new();

    [JsonPropertyName("subcategories")]
    public Dictionary<string, TaxonomySubcategory> Subcategories { get; set; } = new();
}

public class TaxonomySubcategory
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("products")]
    public List<ProductType> Products { get; set; } = [];
}

public class ProductType
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("variants")]
    public List<string> Variants { get; set; } = [];

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];

    [JsonPropertyName("priceRange")]
    public PriceRange? PriceRange { get; set; }

    [JsonPropertyName("brands")]
    public List<string> Brands { get; set; } = [];

    [JsonPropertyName("materials")]
    public List<string> Materials { get; set; } = [];

    [JsonPropertyName("colours")]
    public List<string> Colours { get; set; } = [];

    [JsonPropertyName("imagePrompt")]
    public string? ImagePrompt { get; set; }

    // Optional type-specific properties
    [JsonPropertyName("switchTypes")]
    public List<string>? SwitchTypes { get; set; }

    [JsonPropertyName("capacities")]
    public List<string>? Capacities { get; set; }

    [JsonPropertyName("weights")]
    public List<string>? Weights { get; set; }

    [JsonPropertyName("dialColours")]
    public List<string>? DialColours { get; set; }

    [JsonPropertyName("bandTypes")]
    public List<string>? BandTypes { get; set; }

    /// <summary>
    /// Generate a random product instance from this type definition.
    /// </summary>
    public GeneratedProduct GenerateRandom(string categorySlug, Random? random = null)
    {
        random ??= Random.Shared;

        var variant = Variants.Count > 0 ? Variants[random.Next(Variants.Count)] : "";
        var brand = Brands.Count > 0 ? Brands[random.Next(Brands.Count)] : "GenericBrand";
        var featureCount = Features.Count > 0 ? random.Next(1, Math.Min(4, Features.Count + 1)) : 0;
        var selectedFeatures = Features.OrderBy(_ => random.Next()).Take(featureCount).ToList();
        var colour = Colours.Count > 0 ? Colours[random.Next(Colours.Count)] : "Black";
        var material = Materials.Count > 0 ? Materials[random.Next(Materials.Count)] : "";

        var priceRange = PriceRange ?? new PriceRange { Min = 29.99m, Max = 199.99m };
        var price = Math.Round((decimal)(random.NextDouble() * (double)(priceRange.Max - priceRange.Min) + (double)priceRange.Min), 2);

        // Occasionally add a sale price
        decimal? originalPrice = null;
        if (random.NextDouble() < 0.25)
        {
            originalPrice = Math.Round(price * (decimal)(1 + random.NextDouble() * 0.4 + 0.1), 2);
        }

        var name = GenerateProductName(brand, variant, random);
        var description = GenerateDescription(variant, selectedFeatures, material);
        var imagePrompt = GenerateImagePrompt(variant, colour, selectedFeatures);

        return new GeneratedProduct
        {
            Name = name,
            Description = description,
            Category = categorySlug,
            Price = price,
            OriginalPrice = originalPrice,
            Tags = GenerateTags(variant, selectedFeatures),
            IsTrending = random.NextDouble() < 0.15,
            IsFeatured = random.NextDouble() < 0.1,
            ImagePrompt = imagePrompt,
            ColourVariants = Colours.OrderBy(_ => random.Next()).Take(3).ToList()
        };
    }

    private string GenerateProductName(string brand, string variant, Random random)
    {
        var suffixes = new[] { "Pro", "Elite", "Plus", "Max", "Ultra", "X", "2.0", "Series", "Edition" };
        var suffix = random.NextDouble() < 0.6 ? $" {suffixes[random.Next(suffixes.Length)]}" : "";

        var formattedType = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Type.Replace("-", " "));
        var formattedVariant = !string.IsNullOrEmpty(variant)
            ? System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(variant.Replace("-", " "))
            : "";

        return $"{brand} {formattedVariant} {formattedType}{suffix}".Trim();
    }

    private string GenerateDescription(string variant, List<string> features, string material)
    {
        var featureText = string.Join(", ", features.Take(3));
        var materialText = !string.IsNullOrEmpty(material) ? $" Crafted with {material}." : "";

        // Variety of product quality descriptors
        var qualityDescriptors = new[]
        {
            "",  // No descriptor (50% chance)
            "",
            "",
            "",
            "",
            "Premium",
            "Quality",
            "Professional",
            "Essential",
            "Affordable"
        };
        
        var quality = qualityDescriptors[Random.Shared.Next(qualityDescriptors.Length)];
        var qualityPrefix = string.IsNullOrEmpty(quality) ? "" : $"{quality} ";

        return $"{qualityPrefix}{variant} {Type} featuring {featureText}.{materialText} Designed for exceptional performance and everyday reliability.";
    }

    private string GenerateImagePrompt(string variant, string colour, List<string> features)
    {
        var basePrompt = ImagePrompt ?? "Professional product photography, studio lighting, white background, high detail";

        return basePrompt
            .Replace("{variant}", variant)
            .Replace("{type}", Type)
            .Replace("{features}", string.Join(", ", features.Take(2)))
            + $", {colour} colour, commercial product shot, 8k quality";
    }

    private List<string> GenerateTags(string variant, List<string> features)
    {
        var tags = new List<string> { Type };

        if (!string.IsNullOrEmpty(variant))
            tags.Add(variant.ToLowerInvariant().Replace(" ", "-"));

        tags.AddRange(features.Take(3).Select(f => f.ToLowerInvariant().Replace(" ", "-")));

        return tags.Distinct().Take(5).ToList();
    }
}

public class PriceRange
{
    [JsonPropertyName("min")]
    public decimal Min { get; set; }

    [JsonPropertyName("max")]
    public decimal Max { get; set; }
}

public class GlobalAttributes
{
    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = [];

    [JsonPropertyName("availability")]
    public List<string> Availability { get; set; } = [];

    [JsonPropertyName("shipping")]
    public List<string> Shipping { get; set; } = [];

    [JsonPropertyName("warranty")]
    public List<string> Warranty { get; set; } = [];

    [JsonPropertyName("sustainability")]
    public List<string> Sustainability { get; set; } = [];
}

public class ImageGenerationConfig
{
    [JsonPropertyName("styles")]
    public List<string> Styles { get; set; } = [];

    [JsonPropertyName("negativePrompts")]
    public List<string> NegativePrompts { get; set; } = [];

    [JsonPropertyName("angleVariants")]
    public List<string> AngleVariants { get; set; } = [];
}
