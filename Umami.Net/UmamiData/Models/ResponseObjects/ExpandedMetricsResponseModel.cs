namespace Umami.Net.UmamiData.Models.ResponseObjects;

/// <summary>
/// Response model for the /api/websites/:websiteId/metrics/expanded endpoint
/// Provides detailed metrics with comprehensive engagement metrics
/// </summary>
public class ExpandedMetricsResponseModel
{
    /// <summary>
    /// Dimension value (e.g., "Mac OS", "/blog/post-1", etc.)
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Page hits count
    /// </summary>
    public int pageviews { get; set; }

    /// <summary>
    /// Unique visitor count
    /// </summary>
    public int visitors { get; set; }

    /// <summary>
    /// Unique visit count
    /// </summary>
    public int visits { get; set; }

    /// <summary>
    /// Single-page visit count (bounces)
    /// </summary>
    public int bounces { get; set; }

    /// <summary>
    /// Aggregate time on site in milliseconds
    /// </summary>
    public long totaltime { get; set; }
}
