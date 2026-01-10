using Mostlylucid.Shared.Interfaces;

namespace Mostlylucid.Services.Umami;

/// <summary>
/// Adapter that provides popularity data from Umami analytics for search ranking.
/// </summary>
public class UmamiPopularityProvider : IPopularityProvider
{
    private readonly IPopularPostsService _popularPostsService;

    public UmamiPopularityProvider(IPopularPostsService popularPostsService)
    {
        _popularPostsService = popularPostsService;
    }

    public int GetViewCount(string slug)
    {
        try
        {
            // Get cached popular posts (no API call)
            var popularPosts = _popularPostsService.GetCachedTopPopularPosts(100);

            // Find matching slug (handles /blog/slug format)
            var popularPost = popularPosts.FirstOrDefault(p =>
                p.Url.EndsWith($"/{slug}", StringComparison.OrdinalIgnoreCase) ||
                p.Url.EndsWith(slug, StringComparison.OrdinalIgnoreCase));

            return popularPost?.Views ?? 0;
        }
        catch
        {
            // Graceful fallback - don't break search if analytics unavailable
            return 0;
        }
    }
}
