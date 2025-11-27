using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;

namespace Mostlylucid.Services.Images;

/// <summary>
/// Background service that periodically processes blog posts to download external images
/// </summary>
public class ImageDownloadBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImageDownloadBackgroundService> _logger;
    private readonly TimeSpan _processInterval = TimeSpan.FromHours(6); // Run every 6 hours
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromDays(1); // Run cleanup daily
    private DateTime _lastCleanup = DateTime.MinValue;

    public ImageDownloadBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ImageDownloadBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image Download Background Service started");

        // Wait a bit before starting to let the app fully initialize
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllPostsAsync(stoppingToken);

                // Run cleanup if it's been more than a day since last cleanup
                if (DateTime.UtcNow - _lastCleanup > _cleanupInterval)
                {
                    await CleanupOrphanedImagesAsync(stoppingToken);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Image Download Background Service");
            }

            // Wait before next run
            _logger.LogInformation("Image Download Background Service sleeping for {Interval}", _processInterval);
            await Task.Delay(_processInterval, stoppingToken);
        }

        _logger.LogInformation("Image Download Background Service stopped");
    }

    private async Task ProcessAllPostsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting to process all blog posts for external images");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MostlylucidDbContext>();
        var imageService = scope.ServiceProvider.GetRequiredService<ExternalImageDownloadService>();

        // Get all published, non-hidden posts
        var posts = await dbContext.BlogPosts
            .Where(p => !p.IsHidden)
            .OrderByDescending(p => p.PublishedDate)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} posts to process", posts.Count);

        var processedCount = 0;
        var imagesDownloaded = 0;

        foreach (var post in posts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation requested, stopping post processing");
                break;
            }

            try
            {
                var beforeCount = await dbContext.DownloadedImages
                    .CountAsync(x => x.PostSlug == post.Slug, cancellationToken);

                await imageService.ProcessPostAsync(post, cancellationToken);

                var afterCount = await dbContext.DownloadedImages
                    .CountAsync(x => x.PostSlug == post.Slug, cancellationToken);

                var newImages = afterCount - beforeCount;
                if (newImages > 0)
                {
                    imagesDownloaded += newImages;
                    _logger.LogInformation("Processed post {Slug}: downloaded {NewImages} new images", post.Slug, newImages);
                }

                processedCount++;

                // Small delay to avoid hammering external servers
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing post {Slug}", post.Slug);
            }
        }

        _logger.LogInformation(
            "Completed processing {ProcessedCount} posts, downloaded {ImagesDownloaded} new images",
            processedCount,
            imagesDownloaded);

        // Get statistics
        var stats = await imageService.GetStatisticsAsync(cancellationToken);
        _logger.LogInformation(
            "Image statistics: {TotalImages} total images, {TotalSize:N0} bytes, {PostsWithImages} posts with images",
            stats.TotalImages,
            stats.TotalSize,
            stats.PostsWithImages);
    }

    private async Task CleanupOrphanedImagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cleanup of orphaned images");

        using var scope = _serviceProvider.CreateScope();
        var imageService = scope.ServiceProvider.GetRequiredService<ExternalImageDownloadService>();

        await imageService.CleanupOrphanedImagesAsync(daysOld: 7, cancellationToken);

        _logger.LogInformation("Orphaned image cleanup completed");
    }
}
