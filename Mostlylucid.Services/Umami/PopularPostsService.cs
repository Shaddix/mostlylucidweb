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
    Task<PopularPost?> GetCachedPopularPost();
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
            var startDate = endDate.AddHours(-24); // Last 24 hours

            // Get URL metrics from Umami for the past 24 hours
            // Note: Umami v3 uses 'path' instead of 'url'
            var metricsRequest = new MetricsRequest
            {
                StartAtDate = startDate,
                EndAtDate = endDate,
                Type = MetricType.path,
                Unit = Unit.hour, // Use hour for 24-hour period (like Umami admin does)
                Timezone = "UTC", // Explicit timezone
                Limit = 100 // Limit to top 100
            };

            var result = await umamiDataService.GetMetrics(metricsRequest);

            if (result?.Status != System.Net.HttpStatusCode.OK)
            {
                logger.LogWarning("Failed to get metrics from Umami: {Status}", result?.Status);
                return _cachedPopularPost;
            }

            if (result.Data == null || result.Data.Length == 0)
            {
                logger.LogWarning("No data returned from Umami API");
                return _cachedPopularPost;
            }

            logger.LogInformation("Received {Count} total paths from Umami", result.Data.Length);

            // Log first few paths to see what we're getting
            var samplePaths = result.Data.Take(10).Select(m => $"{m.x} ({m.y} views)");
            logger.LogInformation("Sample paths: {Paths}", string.Join(", ", samplePaths));

            // Filter for blog posts (URLs starting with /blog/)
            var blogPosts = result.Data
                .Where(m => m.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            logger.LogInformation("Found {Count} blog post paths after filtering", blogPosts.Count);

            if (blogPosts.Count == 0)
            {
                logger.LogWarning("No blog posts found in metrics (no paths starting with /blog/)");
                return _cachedPopularPost;
            }

            // Aggregate all language variants of the same post
            var aggregatedPosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var post in blogPosts)
            {
                if (!post.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase))
                    continue;

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

    public async Task<PopularPost?> GetCachedPopularPost()
    {
        return _cachedPopularPost ?? await GetMostPopularPostAsync();
    }
}
