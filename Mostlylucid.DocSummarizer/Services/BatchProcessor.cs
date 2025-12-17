using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Handles batch processing of multiple documents
/// </summary>
public class BatchProcessor
{
    private readonly BatchConfig _config;
    private readonly string? _errorLogPath;
    private readonly DocumentSummarizer _summarizer;
    private readonly bool _verbose;

    public BatchProcessor(DocumentSummarizer summarizer, BatchConfig config, bool verbose = false,
        string? errorLogPath = null)
    {
        _summarizer = summarizer;
        _config = config;
        _verbose = verbose;
        _errorLogPath = errorLogPath;
    }

    /// <summary>
    ///     Process all documents in a directory, saving each immediately
    /// </summary>
    /// <param name="directoryPath">Directory to process</param>
    /// <param name="mode">Summarization mode</param>
    /// <param name="focus">Optional focus query for RAG mode</param>
    /// <param name="onFileCompleted">Callback invoked after each file - MUST save output, result is discarded after</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<BatchSummary> ProcessDirectoryAsync(
        string directoryPath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null,
        Func<BatchResult, Task>? onFileCompleted = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var sw = Stopwatch.StartNew();

        // Only track stats, NOT full results - avoid OOM on large batches
        var successCount = 0;
        var failureCount = 0;
        var failedFiles = new List<(string Path, string Error, string? StackTrace)>();

        // Find matching files
        var files = FindMatchingFiles(directoryPath);
        var totalFiles = files.Count;

        AnsiConsole.MarkupLine($"[blue]Found {totalFiles} files to process[/]");
        AnsiConsole.WriteLine();

        if (totalFiles == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files found matching criteria[/]");
            return new BatchSummary(0, 0, 0, new List<BatchResult>(), sw.Elapsed);
        }

        // Process with Spectre Console progress bar
        // Use context guard to prevent nested interactive displays
        using var _ = ProgressService.EnterInteractiveContext();

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var mainTask = ctx.AddTask("[blue]Processing files[/]", maxValue: totalFiles);

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var fileName = Path.GetFileName(file);
                    mainTask.Description = $"[blue]{TruncateFileName(fileName, 40)}[/]";

                    var result = await ProcessFileAsync(file, mode, focus, cancellationToken);

                    // Save immediately via callback
                    if (onFileCompleted != null)
                        try
                        {
                            await onFileCompleted(result);
                        }
                        catch (Exception ex)
                        {
                            await LogErrorAsync(file, $"Failed to save output: {ex.Message}", ex.StackTrace);
                        }

                    // Track stats only
                    if (result.Success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                        if (result.Error != null)
                        {
                            failedFiles.Add((file, result.Error, result.StackTrace));
                            await LogErrorAsync(file, result.Error, result.StackTrace);
                        }
                    }

                    mainTask.Increment(1);

                    // Let GC reclaim the summary memory
                    result = null;

                    // Stop on error if configured
                    if (failureCount > 0 && !_config.ContinueOnError) break;
                }

                mainTask.Description = "[green]Complete[/]";
            });

        sw.Stop();

        // Display summary
        AnsiConsole.WriteLine();
        DisplayBatchSummary(totalFiles, successCount, failureCount, failedFiles, sw.Elapsed);

        // Return lightweight summary without full results
        var summary = new BatchSummary(
            totalFiles,
            successCount,
            failureCount,
            failedFiles.Select(f => new BatchResult(f.Path, false, null, f.Error, TimeSpan.Zero)).ToList(),
            sw.Elapsed);

        return summary;
    }

    private void DisplayBatchSummary(int total, int success, int failed,
        List<(string Path, string Error, string? StackTrace)> failedFiles, TimeSpan elapsed)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Batch Processing Summary[/]");

        table.AddColumn(new TableColumn("[bold]Metric[/]"));
        table.AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

        table.AddRow("Total Files", total.ToString());
        table.AddRow("Successful", $"[green]{success}[/]");
        table.AddRow("Failed", failed > 0 ? $"[red]{failed}[/]" : "0");
        table.AddRow("Success Rate", $"{(total > 0 ? (double)success / total * 100 : 0):F1}%");
        table.AddRow("Duration", $"{elapsed.TotalMinutes:F1} minutes");

        AnsiConsole.Write(table);

        // Show failed files if any
        if (failedFiles.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red bold]Failed Files:[/]");

            var errorTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn(new TableColumn("[bold]File[/]"))
                .AddColumn(new TableColumn("[bold]Error[/]"));

            foreach (var (path, error, _) in failedFiles.Take(20))
            {
                var fileName = Path.GetFileName(path);
                var shortError = error.Length > 60 ? error[..57] + "..." : error;
                errorTable.AddRow(
                    Markup.Escape(TruncateFileName(fileName, 40)),
                    $"[red]{Markup.Escape(shortError)}[/]");
            }

            if (failedFiles.Count > 20) errorTable.AddRow($"[grey]... and {failedFiles.Count - 20} more[/]", "");

            AnsiConsole.Write(errorTable);

            if (!string.IsNullOrEmpty(_errorLogPath))
                AnsiConsole.MarkupLine($"\n[yellow]Full error details logged to: {Markup.Escape(_errorLogPath)}[/]");
        }
    }

    private async Task LogErrorAsync(string filePath, string error, string? stackTrace)
    {
        // Always log to console in red
        AnsiConsole.MarkupLine(
            $"[red]  ERROR: {Markup.Escape(Path.GetFileName(filePath))}: {Markup.Escape(error.Length > 80 ? error[..77] + "..." : error)}[/]");

        // Log to file if configured
        if (!string.IsNullOrEmpty(_errorLogPath))
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] {filePath}\n  Error: {error}\n";
                if (!string.IsNullOrEmpty(stackTrace)) logEntry += $"  Stack: {stackTrace}\n";
                logEntry += "\n";

                await File.AppendAllTextAsync(_errorLogPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
    }

    private static string TruncateFileName(string fileName, int maxLength)
    {
        if (fileName.Length <= maxLength) return fileName;
        var ext = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var truncatedLength = maxLength - ext.Length - 3;
        if (truncatedLength < 5) return fileName[..maxLength];
        return nameWithoutExt[..truncatedLength] + "..." + ext;
    }

    /// <summary>
    ///     Process a single file with error handling
    /// </summary>
    private async Task<BatchResult> ProcessFileAsync(
        string filePath,
        SummarizationMode mode,
        string? focus,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var summary = await _summarizer.SummarizeAsync(filePath, mode, focus);
            sw.Stop();

            return new BatchResult(filePath, true, summary, null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Create detailed error with stack trace for logging
            return new BatchResult(filePath, false, null, ex.Message, sw.Elapsed, ex.StackTrace);
        }
    }

    /// <summary>
    ///     Find files matching the configured patterns
    /// </summary>
    private List<string> FindMatchingFiles(string directoryPath)
    {
        var matcher = new Matcher();

        // Add include patterns
        foreach (var pattern in _config.IncludePatterns) matcher.AddInclude(pattern);

        // Add exclude patterns
        foreach (var pattern in _config.ExcludePatterns) matcher.AddExclude(pattern);

        // Use EnumerateFiles for lazy enumeration - better for large directories
        var searchOption = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var matchedFiles = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.*", searchOption))
            // Case-insensitive extension check
            if (_config.FileExtensions.Count == 0 ||
                _config.FileExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                // Check exclude patterns
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var excluded = _config.ExcludePatterns.Any(p =>
                    relativePath.Contains(p, StringComparison.OrdinalIgnoreCase));

                if (!excluded) matchedFiles.Add(file);
            }

        return matchedFiles;
    }
}