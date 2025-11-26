using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Middleware;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;
using Mostlylucid.Shared.Models;
using Mostlylucid.Shared.Services;
using Polly;
using Serilog.Events;

namespace Mostlylucid.Blog.WatcherService;

public class MarkdownDirectoryWatcherService(
    MarkdownConfig markdownConfig,
    IServiceScopeFactory serviceScopeFactory,
    IStartupCoordinator startupCoordinator,
    IMemoryCache memoryCache,
    IOutputCacheStore outputCacheStore,
    ILogger<MarkdownDirectoryWatcherService> logger)
    : IHostedService
{
    private Task _awaitChangeTask = Task.CompletedTask;
    private FileSystemWatcher _fileSystemWatcher;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _fileSystemWatcher = new FileSystemWatcher
        {
            Path = markdownConfig.MarkdownPath,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                           NotifyFilters.Size,
            Filter = "*.md", // Watch all markdown files
            IncludeSubdirectories = true // Enable watching subdirectories
        };
        // Subscribe to events
        _fileSystemWatcher.EnableRaisingEvents = true;

        _awaitChangeTask = Task.Run(() => AwaitChanges(cancellationToken), cancellationToken);
        logger.LogInformation("Started watching directory {Directory}", markdownConfig.MarkdownPath);

        // Signal ready - watcher is set up and listening
        startupCoordinator.SignalReady(StartupServiceNames.MarkdownDirectoryWatcher);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop watching
        _fileSystemWatcher.EnableRaisingEvents = false;
        _fileSystemWatcher.Dispose();

        Console.WriteLine($"Stopped watching directory: {markdownConfig.MarkdownPath}");

        return Task.CompletedTask;
    }

    private async Task AwaitChanges(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var fileEvent = _fileSystemWatcher.WaitForChanged(WatcherChangeTypes.All);
            if (fileEvent.ChangeType == WatcherChangeTypes.Changed ||
                fileEvent.ChangeType == WatcherChangeTypes.Created)
            {
                await OnChangedAsync(fileEvent);
            }
            else if (fileEvent.ChangeType == WatcherChangeTypes.Deleted)
            {
                await OnDeletedAsync(fileEvent);
            }
            else if (fileEvent.ChangeType == WatcherChangeTypes.Renamed)
            {
                await OnRenamedAsync(fileEvent);
            }
        }
    }

    private async Task OnChangedAsync(WaitForChangedResult e)
    {
        if (e.Name == null) return;

        using var activity = Log.Logger.StartActivity("Markdown File Changed {Name}", e.Name);
        var retryPolicy = Policy
            .Handle<IOException>() // Only handle IO exceptions (like file in use)
            .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromMilliseconds(500 * retryAttempt),
                (exception, timeSpan, retryCount, context) =>
                {
                    activity?.Activity?.SetTag("Retry Attempt", retryCount);
                    // Log the retry attempt
                    logger.LogWarning("File is in use, retrying attempt {RetryCount} after {TimeSpan}", retryCount,
                        timeSpan);
                });

        try
        {
            var fileName = e.Name;
            var isTranslated = Path.GetFileNameWithoutExtension(e.Name).Contains(".");
            var language = MarkdownBaseService.EnglishLanguage;
            var directory = markdownConfig.MarkdownPath;

            if (isTranslated)
            {
                language = Path.GetFileNameWithoutExtension(e.Name).Split('.').Last();
                fileName = Path.GetFileName(fileName);
                directory = markdownConfig.MarkdownTranslatedPath;
            }

            var filePath = Path.Combine(directory, fileName);
            var scope = serviceScopeFactory.CreateScope();
            var markdownBlogService = scope.ServiceProvider.GetRequiredService<IMarkdownBlogService>();

            // Use the Polly retry policy for executing the operation
            await retryPolicy.ExecuteAsync(async () =>
            {
                // Get the blog service first to potentially set processing context
                var blogService = scope.ServiceProvider.GetRequiredService<IBlogService>();

                // Get the markdown content first (before rendering)
                var markdown = await File.ReadAllTextAsync(filePath);

                // Extract slug from filename
                var slug = Path.GetFileNameWithoutExtension(fileName);
                if (isTranslated)
                {
                    slug = slug.Split('.').First();
                }

                // Use SavePost(slug, language, markdown) which handles processing context correctly
                var savedModel = await blogService.SavePost(slug, language, markdown);
                activity?.Activity?.SetTag("Page Processed", savedModel.Slug);
                activity?.Activity?.SetTag("Page Saved", savedModel.Slug);

                // Invalidate broken link mapping caches
                BrokenLinkArchiveMiddleware.InvalidateLinkCaches(memoryCache);

                // Evict OutputCache for all blog pages (tag-based eviction)
                await outputCacheStore.EvictByTagAsync("blog", CancellationToken.None);
                logger.LogDebug("Invalidated broken link cache and OutputCache for slug {Slug}", savedModel.Slug);

                // Index in semantic search ONLY if file is in main Markdown directory (not subdirectories)
                // Check if e.Name contains no directory separators - meaning it's in the root directory
                if (!e.Name.Contains(Path.DirectorySeparatorChar) && !e.Name.Contains(Path.AltDirectorySeparatorChar))
                {
                    await IndexPostForSemanticSearchAsync(scope, savedModel, language);
                }

                if (language == MarkdownBaseService.EnglishLanguage && !string.IsNullOrEmpty(savedModel.Markdown))
                {
                    var translateService = scope.ServiceProvider.GetRequiredService<IBackgroundTranslateService>();
                    await translateService.TranslateForAllLanguages(
                        new PageTranslationModel()
                            { OriginalFileName = filePath, OriginalMarkdown = savedModel.Markdown, Persist = true });
                }
            });

            activity?.Complete();
        }
        catch (Exception exception)
        {
            activity?.Complete(LogEventLevel.Error, exception);
        }
    }

    private async Task OnDeletedAsync(WaitForChangedResult e)
    {
        if (e.Name == null) return;
        using var activity = Log.Logger.StartActivity("Markdown File Deleting {Name}", e.Name);
        try
        {
            var isTranslated = Path.GetFileNameWithoutExtension(e.Name).Contains(".");
            var language = MarkdownBaseService.EnglishLanguage;
            var slug = Path.GetFileNameWithoutExtension(e.Name);
            if (isTranslated)
            {
                var name = Path.GetFileNameWithoutExtension(e.Name).Split('.');
                language = name.Last();
                slug = name.First();
            }
            else
            {
                // Delete all translated versions
                var translatedFiles = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, $"{slug}.*.*");
                _fileSystemWatcher.EnableRaisingEvents = false;
                foreach (var file in translatedFiles)
                {
                    File.Delete(file);
                }
                _fileSystemWatcher.EnableRaisingEvents = true;
            }

            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogViewService>();
            await blogService.Delete(slug, language);

            // Delete from semantic search ONLY if file was in main Markdown directory (not subdirectories)
            if (!e.Name.Contains(Path.DirectorySeparatorChar) && !e.Name.Contains(Path.AltDirectorySeparatorChar))
            {
                await DeletePostFromSemanticSearchAsync(scope, slug, language);
            }

            activity?.Activity?.SetTag("Page Deleted", slug);
            activity?.Complete();
            logger.LogInformation("Deleted blog post {Slug} in {Language}", slug, language);
        }
        catch (Exception exception)
        {
            activity?.Complete(LogEventLevel.Error, exception);
            logger.LogError(exception, "Error deleting blog post {Slug}", e.Name);
        }
    }

    private async Task OnRenamedAsync(WaitForChangedResult e)
    {
        if (e.Name == null || e.OldName == null) return;

        using var activity = Log.Logger.StartActivity("Markdown File Renamed from {OldName} to {NewName}", e.OldName, e.Name);
        try
        {
            // Extract old slug
            var oldSlug = Path.GetFileNameWithoutExtension(e.OldName);
            var oldIsTranslated = oldSlug.Contains(".");
            var oldLanguage = MarkdownBaseService.EnglishLanguage;

            if (oldIsTranslated)
            {
                var parts = oldSlug.Split('.');
                oldSlug = parts.First();
                oldLanguage = parts.Last();
            }

            // Delete old entry (and translated versions if it's the main file)
            using var scope = serviceScopeFactory.CreateScope();
            var blogService = scope.ServiceProvider.GetRequiredService<IBlogViewService>();

            if (!oldIsTranslated)
            {
                // Delete all translated versions of the old slug
                var translatedFiles = Directory.GetFiles(markdownConfig.MarkdownTranslatedPath, $"{oldSlug}.*.*");
                _fileSystemWatcher.EnableRaisingEvents = false;
                foreach (var file in translatedFiles)
                {
                    File.Delete(file);
                }
                _fileSystemWatcher.EnableRaisingEvents = true;
            }

            await blogService.Delete(oldSlug, oldLanguage);
            logger.LogInformation("Deleted old blog post {OldSlug} in {Language}", oldSlug, oldLanguage);

            // Delete from semantic search ONLY if old file was in main Markdown directory
            if (!e.OldName.Contains(Path.DirectorySeparatorChar) && !e.OldName.Contains(Path.AltDirectorySeparatorChar))
            {
                await DeletePostFromSemanticSearchAsync(scope, oldSlug, oldLanguage);
            }

            // Now process the new file as if it was created
            await OnChangedAsync(new WaitForChangedResult
            {
                ChangeType = WatcherChangeTypes.Created,
                Name = e.Name
            });

            activity?.Activity?.SetTag("Old Slug", oldSlug);
            activity?.Activity?.SetTag("New Name", e.Name);
            activity?.Complete();
        }
        catch (Exception exception)
        {
            activity?.Complete(LogEventLevel.Error, exception);
            logger.LogError(exception, "Error handling renamed file from {OldName} to {NewName}", e.OldName, e.Name);
        }
    }

    /// <summary>
    /// Index a blog post in the semantic search index
    /// </summary>
    private async Task IndexPostForSemanticSearchAsync(IServiceScope scope, BlogPostDto post, string language)
    {
        try
        {
            var semanticSearchService = scope.ServiceProvider.GetService<ISemanticSearchService>();
            if (semanticSearchService == null)
            {
                // Semantic search not configured
                return;
            }

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
            logger.LogInformation("Indexed post {Slug} ({Language}) in semantic search", post.Slug, language);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to index post {Slug} ({Language}) in semantic search", post.Slug, language);
        }
    }

    /// <summary>
    /// Remove a blog post from the semantic search index
    /// </summary>
    private async Task DeletePostFromSemanticSearchAsync(IServiceScope scope, string slug, string language)
    {
        try
        {
            var semanticSearchService = scope.ServiceProvider.GetService<ISemanticSearchService>();
            if (semanticSearchService == null)
            {
                // Semantic search not configured
                return;
            }

            await semanticSearchService.DeletePostAsync(slug, language);
            logger.LogInformation("Deleted post {Slug} ({Language}) from semantic search index", slug, language);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete post {Slug} ({Language}) from semantic search", slug, language);
        }
    }
}