using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.ArchiveOrg;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Services;
using Serilog;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Archive.org Downloader starting...");

    // Build host
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddArchiveOrgServices(configuration);
        })
        .Build();

    // Parse command
    var command = args.Length > 0 ? args[0].ToLower() : "help";

    switch (command)
    {
        case "download":
            await RunDownloadAsync(host.Services);
            break;

        case "convert":
            await RunConvertAsync(host.Services);
            break;

        case "full":
            await RunFullPipelineAsync(host.Services);
            break;

        case "help":
        default:
            PrintHelp();
            break;
    }

    Log.Information("Archive.org Downloader completed");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

static async Task RunDownloadAsync(IServiceProvider services)
{
    var downloader = services.GetRequiredService<IArchiveDownloader>();
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ArchiveOrgOptions>>().Value;
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting download from Archive.org");
    logger.LogInformation("Target URL: {Url}", options.TargetUrl);
    logger.LogInformation("Output Directory: {Dir}", options.OutputDirectory);

    if (options.EndDate.HasValue)
        logger.LogInformation("End Date: {Date:yyyy-MM-dd}", options.EndDate);
    if (options.StartDate.HasValue)
        logger.LogInformation("Start Date: {Date:yyyy-MM-dd}", options.StartDate);
    else
        logger.LogInformation("Start Date: (none - fetching ALL available archives)");

    logger.LogInformation("Rate Limit: {Ms}ms between requests", options.RateLimitMs);

    var progress = new Progress<DownloadProgress>(p =>
    {
        if (p.ProcessedRecords % 10 == 0 || p.ProcessedRecords == p.TotalRecords)
        {
            logger.LogInformation(
                "Download Progress: {Processed}/{Total} ({Percent:F1}%) - Success: {Success}, Failed: {Failed}",
                p.ProcessedRecords, p.TotalRecords, p.PercentComplete,
                p.SuccessfulDownloads, p.FailedDownloads);
        }
    });

    var results = await downloader.DownloadAllAsync(progress);

    logger.LogInformation("Download complete. Total: {Total}, Success: {Success}, Failed: {Failed}",
        results.Count,
        results.Count(r => r.Success),
        results.Count(r => !r.Success));
}

static async Task RunConvertAsync(IServiceProvider services)
{
    var converter = services.GetRequiredService<IHtmlToMarkdownConverter>();
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MarkdownConversionOptions>>().Value;
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting HTML to Markdown conversion");
    logger.LogInformation("Input Directory: {Dir}", options.InputDirectory);
    logger.LogInformation("Output Directory: {Dir}", options.OutputDirectory);

    var progress = new Progress<ConversionProgress>(p =>
    {
        logger.LogInformation(
            "Conversion Progress: {Processed}/{Total} ({Percent:F1}%) - Success: {Success}, Failed: {Failed}",
            p.ProcessedFiles, p.TotalFiles, p.PercentComplete,
            p.SuccessfulConversions, p.FailedConversions);
    });

    var articles = await converter.ConvertAllAsync(progress);

    logger.LogInformation("Conversion complete. Total articles: {Count}", articles.Count);

    foreach (var article in articles)
    {
        logger.LogInformation("  - {Title} [{Categories}] -> {Path}",
            article.Title,
            string.Join(", ", article.Categories),
            article.OutputFilePath);
    }
}

static async Task RunFullPipelineAsync(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Running full pipeline: Download + Convert");

    await RunDownloadAsync(services);
    await RunConvertAsync(services);

    logger.LogInformation("Full pipeline complete");
}

static void PrintHelp()
{
    Console.WriteLine("""
                      Archive.org Downloader & Markdown Converter
                      ==========================================

                      Commands:
                        download    Download archived pages from Archive.org
                        convert     Convert downloaded HTML to Markdown
                        full        Run full pipeline (download + convert)
                        help        Show this help message

                      Configuration (appsettings.json):

                        {
                          "ArchiveOrg": {
                            "TargetUrl": "https://example.com",
                            "EndDate": "2024-01-01",
                            "StartDate": null,              // null = ALL pages (greedy mode)
                            "OutputDirectory": "./archive-output",
                            "RateLimitMs": 5000,            // 5 seconds between requests
                            "UniqueUrlsOnly": true,
                            "IncludePatterns": [],
                            "ExcludePatterns": [".*\\.js$", ".*\\.css$"]
                          },
                          "MarkdownConversion": {
                            "InputDirectory": "./archive-output",
                            "OutputDirectory": "./markdown-output",
                            "ContentSelector": "article",   // CSS selector for main content
                            "GenerateTags": true,
                            "ExtractDates": true
                          },
                          "Ollama": {
                            "BaseUrl": "http://localhost:11434",
                            "Model": "llama3.2",
                            "Enabled": true,
                            "MaxTags": 5
                          }
                        }

                      Examples:
                        dotnet run -- download
                        dotnet run -- convert
                        dotnet run -- full

                        # Override config via command line:
                        dotnet run -- download --ArchiveOrg:TargetUrl=https://myblog.com --ArchiveOrg:EndDate=2023-12-31
                      """);
}

// Make Program class accessible for generic logger
public partial class Program;
