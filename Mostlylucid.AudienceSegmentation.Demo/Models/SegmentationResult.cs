namespace Mostlylucid.AudienceSegmentation.Demo.Models;

/// <summary>
/// Result of segmentation analysis
/// </summary>
public class SegmentationResult
{
    public Customer Customer { get; set; } = new();
    public CustomerSegment CurrentSegment { get; set; } = new();
    public List<Product> RecommendedProducts { get; set; } = new();
    public Dictionary<string, double> SegmentScores { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
