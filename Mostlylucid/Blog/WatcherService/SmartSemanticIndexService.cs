using Mostlylucid.Blog.ViewServices;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;
using Mostlylucid.Shared.Services;

namespace Mostlylucid.Blog.WatcherService;

/// <summary>
/// Intelligently reindexes semantic search on deployment if Qdrant collection is empty.
/// Ensures vector search "just works" after deployment without manual configuration.
/// </summary>
public class SmartSemanticIndexService(
    MarkdownConfig markdownConfig,
    IServiceScopeFactory serviceScopeFactory,
    IStartupCoordinator startupCoordinator,
    ILogger<SmartSemanticIndexService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Run in background so it doesn't block startup
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to let semantic search initialize
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                using var scope = serviceScopeFactory.CreateScope();
                var vectorStoreService = scope.ServiceProvider.GetService<IVectorStoreService>();

                if (vectorStoreService == null)
                {
                    logger.LogInformation("Semantic search not available, skipping smart indexing");
                    return;
                }

                // Always reindex on startup - it's idempotent because IndexPostAsync checks content hashes
                // This ensures vector search "just works" after deployment without manual intervention
                logger.LogInformation("Running smart semantic indexing on startup (content hash comparison ensures only changed posts are reindexed)...");
                await ReindexAllPostsAsync(cancellationToken);
                logger.LogInformation("Smart semantic indexing complete");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during smart semantic indexing");
            }
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReindexAllPostsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();
            var markdownBlogService = scope.ServiceProvider.GetRequiredService<IMarkdownBlogService>();
            var semanticSearchService = scope.ServiceProvider.GetService<ISemanticSearchService>();

            if (semanticSearchService == null)
            {
                logger.LogWarning("Semantic search service not available");
                return;
            }

            // Get all English markdown files (we only index English posts)
            var markdownFiles = Directory.GetFiles(markdownConfig.MarkdownPath, "*.md", SearchOption.TopDirectoryOnly);
            logger.LogInformation("Found {Count} English markdown files to index", markdownFiles.Length);

            int processed = 0;
            int failed = 0;

            foreach (var filePath in markdownFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var slug = Path.GetFileNameWithoutExtension(filePath);
                try
                {
                    var blogModel = await markdownBlogService.GetPage(filePath);
                    if (blogModel == null)
                    {
                        logger.LogDebug("Skipping invalid markdown file: {FilePath}", filePath);
                        continue;
                    }

                    // Index in semantic search
                    await IndexPostAsync(semanticSearchService, blogModel);

                    processed++;
                    if (processed % 25 == 0)
                    {
                        logger.LogInformation("Indexed {Count}/{Total} posts in semantic search", processed, markdownFiles.Length);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogWarning(ex, "Failed to index {Slug} in semantic search", slug);
                }
            }

            stopwatch.Stop();

            logger.LogInformation(
                "Smart semantic indexing completed in {Duration}. {Processed} indexed, {Failed} failed",
                stopwatch.Elapsed,
                processed,
                failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during smart semantic reindexing");
        }
    }

    private async Task IndexPostAsync(ISemanticSearchService semanticSearchService, Shared.Models.BlogPostDto post)
    {
        var document = new BlogPostDocument
        {
            Id = post.Slug,
            Slug = post.Slug,
            Title = post.Title,
            Content = post.PlainTextContent,
            PublishedDate = post.PublishedDate,
            Languages = GetAvailableLanguages(post.Slug),
            Categories = post.Categories
        };

        await semanticSearchService.IndexPostAsync(document);
    }

    /// <summary>
    /// Get all available languages for a given post slug by scanning the translated directory
    /// </summary>
    private string[] GetAvailableLanguages(string slug)
    {
        var languages = new List<string> { "en" }; // English is always available

        var translatedPath = markdownConfig.MarkdownTranslatedPath;
        if (!Directory.Exists(translatedPath))
            return languages.ToArray();

        var translatedFiles = Directory.GetFiles(translatedPath, $"{slug}.*.md", SearchOption.TopDirectoryOnly);

        foreach (var file in translatedFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('.');
            if (parts.Length >= 2)
            {
                var langCode = parts[^1];
                if (langCode.Length == 2 && langCode != "en")
                {
                    languages.Add(langCode);
                }
            }
        }

        return languages.OrderBy(l => l).ToArray();
    }
}
