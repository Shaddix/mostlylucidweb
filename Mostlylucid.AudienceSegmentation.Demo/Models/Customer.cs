namespace Mostlylucid.AudienceSegmentation.Demo.Models;

/// <summary>
/// Represents a customer with their behavioral data
/// </summary>
public class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<string> ViewedProducts { get; set; } = new();
    public List<string> PurchasedProducts { get; set; } = new();
    public List<string> SearchQueries { get; set; } = new();
    public Dictionary<string, int> CategoryInterests { get; set; } = new();

    /// <summary>
    /// Semantic embedding representing customer preferences
    /// (aggregate of their interactions)
    /// </summary>
    public float[]? ProfileEmbedding { get; set; }

    /// <summary>
    /// Current segment this customer belongs to
    /// </summary>
    public string? CurrentSegment { get; set; }

    /// <summary>
    /// Timestamp of last activity
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}
