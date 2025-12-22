using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

/// <summary>
/// Generated seller with persona and product catalog.
/// </summary>
public class GeneratedSeller
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public double Rating { get; set; } = 4.0;
    public int ReviewCount { get; set; }
    public bool IsVerified { get; set; }
    public string[] Specialties { get; set; } = [];
    public string[] Categories { get; set; } = [];
    public List<GeneratedProduct> Products { get; set; } = [];
    public float[]? Embedding { get; set; }
}

/// <summary>
/// Generated customer profile for testing personalization.
/// </summary>
public class GeneratedCustomer
{
    public string ProfileKey { get; set; } = string.Empty;
    public string Persona { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, double> Interests { get; set; } = new();
    public Dictionary<string, double> BrandAffinities { get; set; } = new();
    public PricePreference? PricePreference { get; set; }
    public List<string> RecentCategories { get; set; } = [];
    public List<GeneratedSignal> Signals { get; set; } = [];
    public float[]? Embedding { get; set; }
    public string? ProfileImagePath { get; set; }
}

/// <summary>
/// Price preference for a customer.
/// </summary>
public class PricePreference
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public bool PrefersDeals { get; set; }
    public bool PrefersLuxury { get; set; }
}

/// <summary>
/// Complete generated dataset.
/// </summary>
public class GeneratedDataset
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "2.0";
    public GenerationStats Stats { get; set; } = new();
    public List<GeneratedSeller> Sellers { get; set; } = [];
    public List<GeneratedCustomer> Customers { get; set; } = [];
}

/// <summary>
/// Generation statistics.
/// </summary>
public class GenerationStats
{
    public int TotalSellers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalImages { get; set; }
    public int TotalEmbeddings { get; set; }
    public TimeSpan Duration { get; set; }
}

#region LLM Response Models

/// <summary>
/// LLM response for seller generation.
/// </summary>
public class LlmSellerResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tagline")]
    public string Tagline { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("specialties")]
    public List<string> Specialties { get; set; } = [];
}

/// <summary>
/// LLM response for product generation.
/// </summary>
public class LlmProductResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("image_prompt")]
    public string ImagePrompt { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];
}

/// <summary>
/// LLM response for customer persona generation.
/// </summary>
public class LlmCustomerResponse
{
    [JsonPropertyName("persona")]
    public string Persona { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bio")]
    public string Bio { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("shopping_style")]
    public string ShoppingStyle { get; set; } = string.Empty;

    [JsonPropertyName("preferred_categories")]
    public List<string> PreferredCategories { get; set; } = [];
}

#endregion
