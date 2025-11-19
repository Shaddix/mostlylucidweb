using Umami.Net.UmamiData.Helpers;

namespace Umami.Net.UmamiData.Models.RequestObjects;

/// <summary>
/// Request model for the /api/websites/:websiteId/events/series endpoint
/// </summary>
public class EventsSeriesRequest : BaseRequest
{
    /// <summary>
    /// Time unit for data bucketing (minute, hour, day, month, year)
    /// </summary>
    [QueryStringParameter("unit", true)]
    public Unit Unit { get; set; } = Unit.day;

    /// <summary>
    /// Timezone for the data (e.g., "America/Los_Angeles")
    /// </summary>
    [QueryStringParameter("timezone")]
    [TimeZoneValidator]
    public string? Timezone { get; set; }

    /// <summary>
    /// Event name to filter by (optional)
    /// </summary>
    [QueryStringParameter("event")]
    public string? EventName { get; set; }
}
