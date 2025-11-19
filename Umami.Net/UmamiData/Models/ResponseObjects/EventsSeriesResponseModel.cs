namespace Umami.Net.UmamiData.Models.ResponseObjects;

/// <summary>
/// Response model for the /api/websites/:websiteId/events/series endpoint
/// </summary>
public class EventsSeriesResponseModel
{
    /// <summary>
    /// Event name identifier
    /// </summary>
    public string x { get; set; } = string.Empty;

    /// <summary>
    /// ISO 8601 timestamp
    /// </summary>
    public string t { get; set; } = string.Empty;

    /// <summary>
    /// Event count for the period
    /// </summary>
    public int y { get; set; }
}
