using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Models;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;

namespace Mostlylucid.Services.SemanticSearch;

/// <summary>
/// Background service that indexes markdown files for semantic search.
/// Only indexes files in the main Markdown directory (NOT subdirectories like drafts, translated, etc.)
/// </summary>
public class SemanticIndexingBackgroundService : BackgroundService
{
    private readonly ILogger<SemanticIndexingBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly MarkdownConfig _markdownConfig;
    private readonly SemanticSearchConfig _semanticSearchConfig;
    private readonly TimeSpan _indexInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30);

    public SemanticIndexingBackgroundService(
        ILogger<SemanticIndexingBackgroundService> logger,
        IServiceProvider serviceProvider,
        ISemanticSearchService semanticSearchService,
        MarkdownConfig markdownConfig,
        SemanticSearchConfig semanticSearchConfig)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _semanticSearchService = semanticSearchService;
        _markdownConfig = markdownConfig;
        _semanticSearchConfig = semanticSearchConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_semanticSearchConfig.Enabled)
        {
            _logger.LogInformation("Semantic search is disabled, indexing service will not run");
            return;
        }

        _logger.LogInformation("Semantic indexing background service starting...");

        // Wait for other services to initialize
        await Task.Delay(_startupDelay, stoppingToken);

        // Initialize the semantic search service
        try
        {
            await _semanticSearchService.InitializeAsync(stoppingToken);
            _logger.LogInformation("Semantic search initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize semantic search service");
            return;
        }

        // Initial indexing
        await IndexAllMarkdownFilesAsync(stoppingToken);

        // Periodic re-indexing to catch any changes
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_indexInterval, stoppingToken);
                await IndexAllMarkdownFilesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic indexing");
            }
        }

        _logger.LogInformation("Semantic indexing background service stopped");
    }

    private async Task IndexAllMarkdownFilesAsync(CancellationToken stoppingToken)
    {
        var markdownPath = _markdownConfig.MarkdownPath;

        if (!Directory.Exists(markdownPath))
        {
            _logger.LogWarning("Markdown directory does not exist: {Path}", markdownPath);
            return;
        }

        // Get ONLY files in the main directory, NOT subdirectories
        var markdownFiles = Directory.GetFiles(markdownPath, "*.md", SearchOption.TopDirectoryOnly);

        _logger.LogInformation("Found {Count} markdown files to index in {Path}", markdownFiles.Length, markdownPath);

        var indexedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var markdownRenderingService = scope.ServiceProvider.GetRequiredService<MarkdownRenderingService>();

        foreach (var filePath in markdownFiles)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                var result = await IndexMarkdownFileAsync(filePath, markdownRenderingService, stoppingToken);
                if (result == IndexResult.Indexed)
                    indexedCount++;
                else if (result == IndexResult.Skipped)
                    skippedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Error indexing file: {FilePath}", filePath);
            }

            // Small delay to avoid overwhelming the embedding service
            await Task.Delay(100, stoppingToken);
        }

        _logger.LogInformation(
            "Indexing complete: {Indexed} indexed, {Skipped} skipped (unchanged), {Errors} errors",
            indexedCount, skippedCount, errorCount);
    }

    private async Task<IndexResult> IndexMarkdownFileAsync(string filePath, MarkdownRenderingService markdownRenderingService, CancellationToken stoppingToken)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Skip translated files (they have language suffix like .es.md, .fr.md)
        if (fileName.Contains('.'))
        {
            var parts = fileName.Split('.');
            if (parts.Length >= 2 && parts[^1].Length == 2)
            {
                // This is likely a translated file, skip it
                return IndexResult.Skipped;
            }
        }

        var markdown = await File.ReadAllTextAsync(filePath, stoppingToken);
        var fileInfo = new FileInfo(filePath);

        // Parse the markdown to get blog post data
        var blogPost = markdownRenderingService.GetPageFromMarkdown(markdown, fileInfo.LastWriteTimeUtc, filePath);

        // Skip invalid files (no valid title/heading)
        if (blogPost == null)
        {
            _logger.LogDebug("Skipping invalid file (no valid title): {FilePath}", filePath);
            return IndexResult.Skipped;
        }

        // Skip hidden posts
        if (blogPost.IsHidden)
        {
            _logger.LogDebug("Skipping hidden post: {Slug}", blogPost.Slug);
            return IndexResult.Skipped;
        }

        // Compute content hash
        var contentHash = ComputeContentHash(blogPost.PlainTextContent);

        // Get available languages for this post
        var languages = GetAvailableLanguages(blogPost.Slug);

        // Check if reindexing is needed
        var needsReindex = await _semanticSearchService.NeedsReindexingAsync(
            blogPost.Slug,
            contentHash,
            stoppingToken);

        if (!needsReindex)
        {
            // Content unchanged, but still update the languages array in case new translations exist
            var vectorStoreService = _serviceProvider.GetService<IVectorStoreService>();
            if (vectorStoreService != null)
            {
                await vectorStoreService.UpdateLanguagesAsync(blogPost.Slug, languages, stoppingToken);
                _logger.LogDebug("Updated languages for unchanged post: {Slug} -> [{Languages}]",
                    blogPost.Slug, string.Join(", ", languages));
            }
            return IndexResult.Skipped;
        }

        // Create document for indexing (ID is just slug since only English is indexed)
        var document = new BlogPostDocument
        {
            Id = blogPost.Slug,
            Slug = blogPost.Slug,
            Title = blogPost.Title,
            Content = blogPost.PlainTextContent,
            ContentHash = contentHash,
            PublishedDate = blogPost.PublishedDate,
            Languages = languages,
            Categories = blogPost.Categories.ToArray()
        };

        await _semanticSearchService.IndexPostAsync(document, stoppingToken);
        _logger.LogInformation("Indexed post: {Slug}", blogPost.Slug);

        return IndexResult.Indexed;
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Get all available languages for a given post slug by scanning the translated directory
    /// </summary>
    private string[] GetAvailableLanguages(string slug)
    {
        var languages = new List<string> { "en" }; // English is always available (the source)

        var translatedPath = _markdownConfig.MarkdownTranslatedPath;
        if (!Directory.Exists(translatedPath))
            return languages.ToArray();

        // Look for files matching pattern: {slug}.{lang}.md
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

    private enum IndexResult
    {
        Indexed,
        Skipped
    }
}
