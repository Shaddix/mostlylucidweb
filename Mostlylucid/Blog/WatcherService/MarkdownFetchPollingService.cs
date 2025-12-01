using Microsoft.EntityFrameworkCore;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.MarkdownTranslator.Models;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Shared;
using Mostlylucid.Shared.Config.Markdown;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Services;
using Serilog.Events;

namespace Mostlylucid.Blog.WatcherService;

/// <summary>
/// Background service that polls remote markdown URLs and regenerates posts when content changes
/// </summary>
public class MarkdownFetchPollingService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<MarkdownFetchPollingService> logger,
    MarkdownConfig markdownConfig,
    IStartupCoordinator startupCoordinator)
    : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Markdown Fetch Polling Service started, waiting for other services...");

        // Wait for all other blog services to be ready with a timeout
        try
        {
            var allReady = await startupCoordinator.WaitForAllServicesAsync(
                TimeSpan.FromMinutes(10), stoppingToken);

            if (allReady)
            {
                logger.LogInformation("All services ready, Markdown Fetch Polling Service starting");
            }
            else
            {
                var pending = startupCoordinator.GetPendingServices();
                logger.LogWarning(
                    "Timeout waiting for services, starting anyway. Pending: {Services}",
                    string.Join(", ", pending));
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Signal that we're ready
        startupCoordinator.SignalReady(StartupServiceNames.MarkdownFetchPolling);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndUpdateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Markdown Fetch Polling Service");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        logger.LogInformation("Markdown Fetch Polling Service stopped");
    }

    private async Task PollAndUpdateAsync(CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("Polling Markdown Fetches");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IMostlylucidDBContext>();
            var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();
            var markdownBlogService = scope.ServiceProvider.GetRequiredService<IMarkdownBlogService>();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();

            // Get all enabled fetches
            var fetches = await context.MarkdownFetches
                .Where(f => f.IsEnabled)
                .Include(f => f.BlogPost)
                .ToListAsync(cancellationToken);

            if (fetches.Count == 0)
            {
                logger.LogDebug("No enabled markdown fetches found");
                activity?.Activity?.SetTag("FetchCount", 0);
                activity?.Complete();
                return;
            }

            logger.LogInformation("Found {Count} enabled markdown fetches to check", fetches.Count);
            activity?.Activity?.SetTag("FetchCount", fetches.Count);

            int updatedCount = 0;
            int errorCount = 0;

            foreach (var fetch in fetches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Check if it's time to poll this URL
                    if (fetch.LastFetchedAt.HasValue)
                    {
                        var timeSinceLastFetch = DateTimeOffset.UtcNow - fetch.LastFetchedAt.Value;
                        if (timeSinceLastFetch.TotalHours < fetch.PollFrequencyHours)
                        {
                            logger.LogDebug(
                                "Skipping {Url} - not due for polling (last fetched {Hours:F2}h ago, frequency: {Frequency}h)",
                                fetch.Url,
                                timeSinceLastFetch.TotalHours,
                                fetch.PollFrequencyHours);
                            continue;
                        }
                    }

                    logger.LogInformation(
                        "Polling {Url} for blog post {PostId} (slug: {Slug})",
                        fetch.Url,
                        fetch.BlogPostId,
                        fetch.BlogPost?.Slug ?? "unknown");

                    // Fetch the content
                    var result = await fetchService.FetchMarkdownAsync(
                        fetch.Url,
                        fetch.PollFrequencyHours,
                        fetch.BlogPostId ?? 0);

                    if (!result.Success)
                    {
                        logger.LogWarning(
                            "Failed to fetch {Url}: {Error}",
                            fetch.Url,
                            result.ErrorMessage);
                        errorCount++;

                        // If too many consecutive failures, consider disabling
                        if (fetch.ConsecutiveFailures >= 10)
                        {
                            logger.LogWarning(
                                "Disabling fetch for {Url} after {Count} consecutive failures",
                                fetch.Url,
                                fetch.ConsecutiveFailures);
                            fetch.IsEnabled = false;
                            await context.SaveChangesAsync(cancellationToken);
                        }

                        continue;
                    }

                    // Check if content changed (service already updated the entity)
                    var updatedFetch = await context.MarkdownFetches
                        .FirstOrDefaultAsync(f => f.Id == fetch.Id, cancellationToken);

                    if (updatedFetch != null && updatedFetch.ContentHash != fetch.ContentHash)
                    {
                        logger.LogInformation(
                            "Content changed for {Url}, regenerating blog post {Slug}",
                            fetch.Url,
                            fetch.BlogPost?.Slug);

                        // Regenerate the blog post
                        await RegenerateBlogPostAsync(
                            fetch.BlogPost,
                            markdownBlogService,
                            blogService,
                            cancellationToken);

                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error polling fetch {Id} for URL {Url}",
                        fetch.Id,
                        fetch.Url);
                    errorCount++;
                }
            }

            activity?.Activity?.SetTag("UpdatedCount", updatedCount);
            activity?.Activity?.SetTag("ErrorCount", errorCount);
            activity?.Complete();

            logger.LogInformation(
                "Polling complete: {Updated} posts updated, {Errors} errors",
                updatedCount,
                errorCount);
        }
        catch (Exception ex)
        {
            activity?.Complete(LogEventLevel.Error, ex);
            logger.LogError(ex, "Error in PollAndUpdateAsync");
        }
    }

    private async Task RegenerateBlogPostAsync(
        BlogPostEntity? blogPost,
        IMarkdownBlogService markdownBlogService,
        IBlogService blogService,
        CancellationToken cancellationToken)
    {
        if (blogPost == null)
        {
            logger.LogWarning("Cannot regenerate blog post - entity is null");
            return;
        }

        try
        {
            // Derive the markdown file path from the blog post
            var filePath = GetMarkdownFilePath(blogPost);
            if (!File.Exists(filePath))
            {
                logger.LogWarning(
                    "Markdown file not found for post {Slug} at path {Path}",
                    blogPost.Slug,
                    filePath);
                return;
            }

            logger.LogInformation(
                "Re-reading markdown file for post {Slug} from {Path}",
                blogPost.Slug,
                filePath);

            // Re-process the markdown file (this will re-fetch remote content)
            var blogModel = await markdownBlogService.GetPage(filePath);
            // Skip invalid files (no valid title/heading)
            if (blogModel == null)
            {
                logger.LogWarning("Skipping invalid markdown file (no valid title): {FilePath}", filePath);
                return;
            }
            blogModel.Language = blogPost.LanguageEntity?.Name ?? Constants.EnglishLanguage;

            // Update the blog post in the database
            await blogService.SavePost(blogModel);

            // Trigger translation if it's an English post
            if (blogModel.Language == Constants.EnglishLanguage)
            {
                var translateService = serviceScopeFactory.CreateScope()
                    .ServiceProvider.GetService<IBackgroundTranslateService>();

                if (translateService != null)
                {
                    await translateService.TranslateForAllLanguages(
                        new PageTranslationModel
                        {
                            OriginalFileName = filePath,
                            OriginalMarkdown = blogModel.Markdown,
                            Persist = true
                        });
                }
            }

            logger.LogInformation(
                "Successfully regenerated blog post {Slug}",
                blogPost.Slug);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error regenerating blog post {Slug}",
                blogPost.Slug);
            throw;
        }
    }

    private string GetMarkdownFilePath(BlogPostEntity blogPost)
    {
        var language = blogPost.LanguageEntity?.Name ?? Constants.EnglishLanguage;
        var slug = blogPost.Slug;

        if (language == Constants.EnglishLanguage)
        {
            // English posts are in the main markdown directory
            return Path.Combine(markdownConfig.MarkdownPath, $"{slug}.md");
        }
        else
        {
            // Translated posts are in the translated directory with format: slug.language.md
            return Path.Combine(markdownConfig.MarkdownTranslatedPath, $"{slug}.{language}.md");
        }
    }
}
