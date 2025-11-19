using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Services.Blog;
using Mostlylucid.Shared.Models;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models.RequestObjects;

namespace Mostlylucid.Services.Umami;

public interface IPopularPostsService
{
    Task<PopularPost?> GetMostPopularPostAsync();
    PopularPost? GetCachedPopularPost();
}

public class PopularPost
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Views { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class PopularPostsService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PopularPostsService> logger) : IPopularPostsService
{
    private PopularPost? _cachedPopularPost;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<PopularPost?> GetMostPopularPostAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var umamiDataService = scope.ServiceProvider.GetRequiredService<UmamiDataService>();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddHours(-24);

            // Get URL metrics from Umami for the past 24 hours
            var metricsRequest = new MetricsRequest
            {
                StartAtDate = startDate,
                EndAtDate = endDate,
                Type = MetricType.url,
                Limit = 100 // Get top 100 URLs
            };

            var result = await umamiDataService.GetMetrics(metricsRequest);

            if (result?.Status != System.Net.HttpStatusCode.OK || result.Data == null || result.Data.Length == 0)
            {
                logger.LogWarning("Failed to get metrics from Umami or no data available");
                return _cachedPopularPost; // Return cached if available
            }

            // Filter for blog posts (URLs starting with /blog/)
            // and exclude language variants (those with dots in the path after /blog/)
            var blogPosts = result.Data
                .Where(m => m.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase))
                .Where(m =>
                {
                    // Extract the slug part after /blog/
                    var slug = m.x.Substring(6).Trim('/');
                    // Exclude if it contains a language indicator (e.g., /blog/slug.fr)
                    // We only want base posts without language extension
                    return !slug.Contains('.');
                })
                .OrderByDescending(m => m.y)
                .ToList();

            if (blogPosts.Count == 0)
            {
                logger.LogInformation("No blog posts found in metrics");
                return _cachedPopularPost;
            }

            // Aggregate all language variants of the same post
            var aggregatedPosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var post in result.Data.Where(m => m.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase)))
            {
                var slug = post.x.Substring(6).Trim('/');
                // Remove language extension if present (e.g., "slug.fr" -> "slug")
                var baseSlug = slug.Contains('.') ? slug.Substring(0, slug.LastIndexOf('.')) : slug;

                if (aggregatedPosts.ContainsKey(baseSlug))
                {
                    aggregatedPosts[baseSlug] += post.y;
                }
                else
                {
                    aggregatedPosts[baseSlug] = post.y;
                }
            }

            // Find the most popular post
            var mostPopular = aggregatedPosts.OrderByDescending(kvp => kvp.Value).FirstOrDefault();

            if (mostPopular.Key == null)
            {
                logger.LogInformation("No aggregated posts found");
                return _cachedPopularPost;
            }

            // Get the blog post details to get the title
            var queryModel = new BlogPostQueryModel(mostPopular.Key, "en");
            var blogPost = await blogService.GetPost(queryModel);

            var popularPost = new PopularPost
            {
                Url = $"/blog/{mostPopular.Key}",
                Title = blogPost?.Title ?? mostPopular.Key.Replace("-", " "),
                Views = mostPopular.Value,
                LastUpdated = DateTime.UtcNow
            };

            _cachedPopularPost = popularPost;
            logger.LogInformation(
                "Most popular post in last 24h: {Title} ({Url}) with {Views} views",
                popularPost.Title,
                popularPost.Url,
                popularPost.Views);

            return popularPost;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting popular posts from Umami");
            return _cachedPopularPost; // Return cached if available
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public PopularPost? GetCachedPopularPost()
    {
        return _cachedPopularPost;
    }
}
