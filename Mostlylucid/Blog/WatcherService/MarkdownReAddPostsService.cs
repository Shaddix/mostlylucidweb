using Mostlylucid.Blog.ViewServices;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;
using Mostlylucid.Shared.Services;
using Serilog.Events;

namespace Mostlylucid.Blog.WatcherService;

/// <summary>
/// Startup service that re-processes and saves ALL markdown posts to the database.
/// Only runs when Markdown:ReAddPosts is set to true in configuration.
/// Useful for testing, after schema changes, or to force re-indexing.
/// </summary>
public class MarkdownReAddPostsService(
    MarkdownConfig markdownConfig,
    IServiceScopeFactory serviceScopeFactory,
    IStartupCoordinator startupCoordinator,
    ILogger<MarkdownReAddPostsService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!markdownConfig.ReAddPosts)
        {
            logger.LogInformation("ReAddPosts is disabled, skipping bulk post re-add");
            // Signal ready immediately since we're not doing anything
            startupCoordinator.SignalReady(StartupServiceNames.MarkdownReAddPosts);
            return;
        }

        logger.LogWarning("ReAddPosts is ENABLED - re-processing ALL markdown posts on startup");

        // Run in background so it doesn't block startup
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to let other services initialize
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                await ReAddAllPostsAsync(cancellationToken);
            }
            finally
            {
                // Signal ready when complete (even if there were errors)
                startupCoordinator.SignalReady(StartupServiceNames.MarkdownReAddPosts);
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReAddAllPostsAsync(CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("ReAdd All Posts");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();
            var markdownBlogService = scope.ServiceProvider.GetRequiredService<IMarkdownBlogService>();
            var semanticSearchService = scope.ServiceProvider.GetService<ISemanticSearchService>();

            // Get all English markdown files
            var markdownFiles = Directory.GetFiles(markdownConfig.MarkdownPath, "*.md", SearchOption.TopDirectoryOnly);
            logger.LogInformation("Found {Count} English markdown files to process", markdownFiles.Length);

            int processed = 0;
            int failed = 0;

            foreach (var filePath in markdownFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var slug = Path.GetFileNameWithoutExtension(filePath);
                try
                {
                    var blogModel = await markdownBlogService.GetPage(filePath);
                    blogModel.Language = MarkdownBaseService.EnglishLanguage;
                    await blogService.SavePost(blogModel);

                    // Index in semantic search
                    if (semanticSearchService != null)
                    {
                        await IndexPostAsync(semanticSearchService, blogModel, MarkdownBaseService.EnglishLanguage);
                    }

                    processed++;
                    if (processed % 10 == 0)
                    {
                        logger.LogInformation("Processed {Count}/{Total} English posts", processed, markdownFiles.Length);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Failed to process {Slug}", slug);
                }
            }

            logger.LogInformation("Completed English posts: {Processed} processed, {Failed} failed", processed, failed);

            // Now process translated files
            var translatedFiles = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, "*.md", SearchOption.TopDirectoryOnly);
            logger.LogInformation("Found {Count} translated markdown files to process", translatedFiles.Length);

            int translatedProcessed = 0;
            int translatedFailed = 0;

            foreach (var filePath in translatedFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                if (!fileNameWithoutExt.Contains(".")) continue;

                var parts = fileNameWithoutExt.Split('.');
                var slug = parts[0];
                var language = parts[^1];

                try
                {
                    var blogModel = await markdownBlogService.GetPage(filePath);
                    blogModel.Language = language;
                    await blogService.SavePost(blogModel);

                    // Index in semantic search
                    if (semanticSearchService != null)
                    {
                        await IndexPostAsync(semanticSearchService, blogModel, language);
                    }

                    translatedProcessed++;
                    if (translatedProcessed % 50 == 0)
                    {
                        logger.LogInformation("Processed {Count}/{Total} translated posts", translatedProcessed, translatedFiles.Length);
                    }
                }
                catch (Exception ex)
                {
                    translatedFailed++;
                    logger.LogError(ex, "Failed to process translated post {Slug} ({Language})", slug, language);
                }
            }

            stopwatch.Stop();

            activity?.Activity?.SetTag("English Processed", processed);
            activity?.Activity?.SetTag("English Failed", failed);
            activity?.Activity?.SetTag("Translated Processed", translatedProcessed);
            activity?.Activity?.SetTag("Translated Failed", translatedFailed);
            activity?.Activity?.SetTag("Duration", stopwatch.Elapsed.ToString());
            activity?.Complete();

            logger.LogWarning(
                "ReAddPosts completed in {Duration}. English: {EnglishProcessed}/{EnglishTotal}, Translated: {TranslatedProcessed}/{TranslatedTotal}",
                stopwatch.Elapsed,
                processed, markdownFiles.Length,
                translatedProcessed, translatedFiles.Length);
        }
        catch (Exception ex)
        {
            activity?.Complete(LogEventLevel.Error, ex);
            logger.LogError(ex, "Error during ReAddPosts");
        }
    }

    private async Task IndexPostAsync(ISemanticSearchService semanticSearchService, Shared.Models.BlogPostDto post, string language)
    {
        try
        {
            var document = new BlogPostDocument
            {
                Id = $"{post.Slug}_{language}",
                Slug = post.Slug,
                Title = post.Title,
                Content = post.PlainTextContent,
                Language = language,
                Categories = post.Categories?.ToList() ?? new List<string>(),
                PublishedDate = post.PublishedDate
            };

            await semanticSearchService.IndexPostAsync(document);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to index post {Slug} ({Language}) in semantic search", post.Slug, language);
        }
    }
}
