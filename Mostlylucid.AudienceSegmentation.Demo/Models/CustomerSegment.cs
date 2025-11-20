namespace Mostlylucid.AudienceSegmentation.Demo.Models;

/// <summary>
/// Represents a customer segment discovered through clustering
/// </summary>
public class CustomerSegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> CharacteristicKeywords { get; set; } = new();
    public List<string> CustomerIds { get; set; } = new();

    /// <summary>
    /// Centroid embedding for this segment
    /// </summary>
    public float[]? CentroidEmbedding { get; set; }

    /// <summary>
    /// Products that appeal most to this segment
    /// </summary>
    public List<string> RecommendedProductIds { get; set; } = new();

    /// <summary>
    /// Average value of customers in this segment
    /// </summary>
    public decimal AverageCustomerValue { get; set; }

    /// <summary>
    /// Number of customers in this segment
    /// </summary>
    public int Size => CustomerIds.Count;
}
