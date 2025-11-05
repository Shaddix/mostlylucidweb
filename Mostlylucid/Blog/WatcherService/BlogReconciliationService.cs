using Mostlylucid.Blog.ViewServices;
using Mostlylucid.MarkdownTranslator;
using Mostlylucid.MarkdownTranslator.Models;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config;
using Mostlylucid.Shared.Config.Markdown;
using Serilog.Events;

namespace Mostlylucid.Blog.WatcherService;

/// <summary>
/// Background service that periodically reconciles the file system with the database
/// Ensures DB entries match markdown files and vice versa
/// Also retries missed translations from overloaded translation service
/// </summary>
public class BlogReconciliationService(
    MarkdownConfig markdownConfig,
    TranslateServiceConfig translateServiceConfig,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BlogReconciliationService> logger)
    : BackgroundService
{
    private readonly TimeSpan _reconciliationInterval = TimeSpan.FromHours(1); // Run every hour

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 5 minutes before first run to let the app initialize
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during blog reconciliation");
            }

            await Task.Delay(_reconciliationInterval, stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("Blog Reconciliation");
        logger.LogInformation("Starting blog reconciliation");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();
            var markdownBlogService = scope.ServiceProvider.GetRequiredService<IMarkdownBlogService>();

            // Get all markdown files from file system
            var markdownFiles = Directory.GetFiles(markdownConfig.MarkdownPath, "*.md", SearchOption.TopDirectoryOnly);
            var translatedFiles = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, "*.md", SearchOption.TopDirectoryOnly);

            // Build a dictionary of expected entries: slug+language -> file path
            var expectedEntries = new Dictionary<string, string>();

            foreach (var file in markdownFiles)
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                var key = $"{slug}|{MarkdownBaseService.EnglishLanguage}";
                expectedEntries[key] = file;
            }

            foreach (var file in translatedFiles)
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                if (fileNameWithoutExt.Contains("."))
                {
                    var parts = fileNameWithoutExt.Split('.');
                    var slug = parts[0];
                    var language = parts[^1]; // Last part is language
                    var key = $"{slug}|{language}";
                    expectedEntries[key] = file;
                }
            }

            // Get all posts from database
            var allPosts = await blogService.GetAllPosts();

            // Find orphaned DB entries (in DB but not in file system)
            var orphanedPosts = allPosts
                .Where(post =>
                {
                    var key = $"{post.Slug}|{post.Language}";
                    return !expectedEntries.ContainsKey(key);
                })
                .ToList();

            if (orphanedPosts.Any())
            {
                logger.LogWarning("Found {Count} orphaned posts in database", orphanedPosts.Count);
                foreach (var orphan in orphanedPosts)
                {
                    logger.LogInformation("Deleting orphaned post: {Slug} ({Language})", orphan.Slug, orphan.Language);
                    await blogService.Delete(orphan.Slug, orphan.Language);
                    activity?.Activity?.AddTag($"Deleted Orphan", $"{orphan.Slug}|{orphan.Language}");
                }
            }

            // Find missing DB entries (in file system but not in DB)
            var existingKeys = allPosts.Select(p => $"{p.Slug}|{p.Language}").ToHashSet();
            var missingEntries = expectedEntries
                .Where(kvp => !existingKeys.Contains(kvp.Key))
                .ToList();

            if (missingEntries.Any())
            {
                logger.LogWarning("Found {Count} files not in database", missingEntries.Count);
                foreach (var (key, filePath) in missingEntries)
                {
                    var parts = key.Split('|');
                    var slug = parts[0];
                    var language = parts[1];

                    logger.LogInformation("Adding missing post: {Slug} ({Language})", slug, language);
                    try
                    {
                        var blogModel = await markdownBlogService.GetPage(filePath);
                        blogModel.Language = language;
                        await blogService.SavePost(blogModel);
                        activity?.Activity?.AddTag($"Added Missing", $"{slug}|{language}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error adding missing post {Slug} ({Language})", slug, language);
                    }
                }
            }

            activity?.Activity?.SetTag("Orphaned Deleted", orphanedPosts.Count);
            activity?.Activity?.SetTag("Missing Added", missingEntries.Count);
            activity?.Complete();

            logger.LogInformation(
                "Blog reconciliation completed. Deleted {OrphanedCount} orphaned posts, added {MissingCount} missing posts",
                orphanedPosts.Count, missingEntries.Count);

            // Retry missed translations if translation service is enabled
            if (translateServiceConfig.Enabled)
            {
                await RetryMissedTranslationsAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            activity?.Complete(LogEventLevel.Error, ex);
            logger.LogError(ex, "Error during reconciliation");
        }
    }

    private async Task RetryMissedTranslationsAsync(CancellationToken cancellationToken)
    {
        using var activity = Log.Logger.StartActivity("Retry Missed Translations");
        logger.LogInformation("Checking for missed translations");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var translateService = scope.ServiceProvider.GetService<IBackgroundTranslateService>();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();

            if (translateService == null || !translateService.TranslationServiceUp)
            {
                logger.LogWarning("Translation service not available, skipping missed translation retry");
                return;
            }

            // Get all English posts from database
            var allPosts = await blogService.GetAllPosts();
            var englishPosts = allPosts.Where(p => p.Language == MarkdownBaseService.EnglishLanguage).ToList();

            int retriedCount = 0;

            foreach (var post in englishPosts)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Check each configured language for missing translations
                foreach (var language in translateServiceConfig.Languages)
                {
                    // Check if translation exists in database
                    var translationExists = allPosts.Any(p =>
                        p.Slug == post.Slug &&
                        p.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

                    // If translation doesn't exist in DB, retry translation
                    if (!translationExists)
                    {
                        logger.LogInformation(
                            "Missing translation for {Slug} in {Language}, retrying",
                            post.Slug, language);

                        try
                        {
                            // Get the original markdown file
                            var markdownFile = Path.Combine(markdownConfig.MarkdownPath, $"{post.Slug}.md");

                            if (File.Exists(markdownFile))
                            {
                                var markdown = await File.ReadAllTextAsync(markdownFile, cancellationToken);

                                await translateService.TranslateForAllLanguages(
                                    new PageTranslationModel
                                    {
                                        OriginalFileName = markdownFile,
                                        OriginalMarkdown = markdown,
                                        Persist = true
                                    });

                                retriedCount++;
                            }
                            else
                            {
                                logger.LogWarning(
                                    "Original markdown file not found for {Slug}",
                                    post.Slug);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Failed to retry translation for {Slug} to {Language}",
                                post.Slug, language);
                        }

                        // Small delay to avoid overwhelming the translation service
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
            }

            activity?.Activity?.SetTag("Translations Retried", retriedCount);
            activity?.Complete();

            logger.LogInformation(
                "Missed translation retry completed. Retried {Count} translations",
                retriedCount);
        }
        catch (Exception ex)
        {
            activity?.Complete(LogEventLevel.Error, ex);
            logger.LogError(ex, "Error retrying missed translations");
        }
    }
}
