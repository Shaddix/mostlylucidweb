namespace Mostlylucid.Referrers.Models;

/// <summary>
/// Represents a referrer that linked to a blog post
/// </summary>
public class Referrer
{
    /// <summary>
    /// The full URL of the referring page
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The domain of the referrer (e.g., "alvinashcraft.com")
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the referrer (extracted from domain or page title)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Number of visits from this referrer
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// First time this referrer was seen
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// Most recent time this referrer was seen
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Whether this referrer has been verified as legitimate (not a bot)
    /// </summary>
    public bool IsVerified { get; set; }
}
