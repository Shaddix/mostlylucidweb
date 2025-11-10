using Markdig;

namespace QdrantMarkdownSearch.Services;

/// <summary>
/// Background service that indexes markdown files on startup
/// This runs once when the application starts
/// </summary>
public class MarkdownIndexingService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarkdownIndexingService> _logger;
    private readonly string _markdownPath;

    public MarkdownIndexingService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MarkdownIndexingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _markdownPath = configuration["MarkdownPath"] ?? "MarkdownDocs";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting markdown indexing service");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vectorSearch = scope.ServiceProvider.GetRequiredService<IVectorSearchService>();

            // Initialize the Qdrant collection
            await vectorSearch.InitializeCollectionAsync(cancellationToken);

            // Get all markdown files
            var markdownFiles = Directory.GetFiles(_markdownPath, "*.md", SearchOption.AllDirectories);

            _logger.LogInformation("Found {Count} markdown files to index", markdownFiles.Length);

            var successCount = 0;
            var failCount = 0;

            foreach (var filePath in markdownFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Indexing cancelled");
                    break;
                }

                try
                {
                    await IndexFileAsync(vectorSearch, filePath, cancellationToken);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to index file: {FilePath}", filePath);
                    failCount++;
                }
            }

            _logger.LogInformation(
                "Markdown indexing completed. Success: {Success}, Failed: {Failed}",
                successCount,
                failCount
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Markdown indexing service failed");
        }
    }

    private async Task IndexFileAsync(
        IVectorSearchService vectorSearch,
        string filePath,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Extract title from markdown (first # heading)
        var title = ExtractTitle(content) ?? fileName;

        // Convert markdown to plain text for better search
        var plainText = Markdown.ToPlainText(content);

        // Use file path as unique ID
        var id = Guid.NewGuid().ToString();

        await vectorSearch.IndexDocumentAsync(
            id: id,
            fileName: fileName,
            title: title,
            content: plainText,
            ct: cancellationToken
        );

        _logger.LogDebug("Indexed: {FileName} with title: {Title}", fileName, title);
    }

    private string? ExtractTitle(string markdown)
    {
        var lines = markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# "))
            {
                return trimmed.Substring(2).Trim();
            }
        }

        return null;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Markdown indexing service stopped");
        return Task.CompletedTask;
    }
}
