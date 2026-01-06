using Mostlylucid.Services.Umami;

namespace Mostlylucid.Blog.WatcherService;

/// <summary>
/// Background service that polls Umami for the most popular post every hour
/// </summary>
public class PopularPostsPollingService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PopularPostsPollingService> logger)
    : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Update every hour

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Popular Posts Polling Service started");

        // Wait a bit before starting to let the application fully initialize
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdatePopularPostAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Popular Posts Polling Service");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        logger.LogInformation("Popular Posts Polling Service stopped");
    }

    private async Task UpdatePopularPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var popularPostsService = scope.ServiceProvider.GetRequiredService<IPopularPostsService>();

            // Get top 5 posts (this also caches the most popular single post)
            var topPosts = await popularPostsService.GetTopPopularPostsAsync(5);

            if (topPosts.Count > 0)
            {
                logger.LogInformation(
                    "Updated {Count} popular posts. Top: {Title} with {Views} views",
                    topPosts.Count,
                    topPosts[0].Title,
                    topPosts[0].Views);
            }
            else
            {
                logger.LogWarning("No popular post data available");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating popular posts");
        }
    }
}
