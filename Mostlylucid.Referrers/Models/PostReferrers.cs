namespace Mostlylucid.Referrers.Models;

/// <summary>
/// Collection of referrers for a specific blog post
/// </summary>
public class PostReferrers
{
    /// <summary>
    /// The slug of the blog post
    /// </summary>
    public string PostSlug { get; set; } = string.Empty;

    /// <summary>
    /// The title of the blog post
    /// </summary>
    public string PostTitle { get; set; } = string.Empty;

    /// <summary>
    /// List of verified referrers to this post
    /// </summary>
    public List<Referrer> Referrers { get; set; } = [];

    /// <summary>
    /// Total number of referrer hits across all sources
    /// </summary>
    public int TotalHits => Referrers.Sum(r => r.HitCount);

    /// <summary>
    /// When this data was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
