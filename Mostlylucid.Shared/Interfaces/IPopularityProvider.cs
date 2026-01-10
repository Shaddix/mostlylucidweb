namespace Mostlylucid.Shared.Interfaces;

/// <summary>
/// Provides popularity data for ranking search results.
/// Abstraction to avoid circular dependencies between layers.
/// </summary>
public interface IPopularityProvider
{
    /// <summary>
    /// Get view count for a specific post slug.
    /// Returns 0 if not found or data not available.
    /// </summary>
    int GetViewCount(string slug);
}
