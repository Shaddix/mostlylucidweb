namespace Mostlylucid.Referrers.Config;

/// <summary>
/// Configuration for the referrer tracking service
/// </summary>
public class ReferrerConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Referrers";

    /// <summary>
    /// Whether referrer tracking is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Domains to exclude from referrer tracking (e.g., search engines, email providers)
    /// </summary>
    public List<string> ExcludedDomains { get; set; } =
    [
        "google.com",
        "google.co.uk",
        "bing.com",
        "yahoo.com",
        "duckduckgo.com",
        "baidu.com",
        "yandex.com",
        "mail.google.com",
        "outlook.live.com",
        "mail.yahoo.com"
    ];

    /// <summary>
    /// Maximum number of referrers to display per post
    /// </summary>
    public int MaxReferrersPerPost { get; set; } = 10;

    /// <summary>
    /// Minimum number of hits from a referrer before it's displayed
    /// </summary>
    public int MinHitsToDisplay { get; set; } = 1;

    /// <summary>
    /// Cache duration in minutes for referrer data
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to use Umami analytics as a data source
    /// </summary>
    public bool UseUmamiSource { get; set; } = true;
}
