using System.Diagnostics;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Batch processor that uses Terminal.Gui for reactive progress display
/// </summary>
public class TuiBatchProcessor
{
    private readonly BatchConfig _config;
    private readonly string? _errorLogPath;
    private readonly DocumentSummarizer _summarizer;

    public TuiBatchProcessor(
        DocumentSummarizer summarizer,
        BatchConfig config,
        string? errorLogPath = null)
    {
        _summarizer = summarizer;
        _config = config;
        _errorLogPath = errorLogPath;
    }

    /// <summary>
    ///     Process all documents in a directory with TUI progress display
    /// </summary>
    public async Task<BatchSummary> ProcessDirectoryAsync(
        string directoryPath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null,
        Func<BatchResult, Task>? onFileCompleted = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        // Find matching files first
        var files = FindMatchingFiles(directoryPath);

        if (files.Count == 0)
        {
            Console.WriteLine("No files found matching criteria");
            return new BatchSummary(0, 0, 0, new List<BatchResult>(), TimeSpan.Zero);
        }

        var startTime = DateTime.UtcNow;
        var results = new List<BatchResult>();
        var successCount = 0;
        var failureCount = 0;

        using var tui = new TuiProgressService();

        await tui.RunBatchAsync(async progress =>
        {
            tui.SetTotalFiles(files.Count);
            tui.Log($"Found {files.Count} files to process");
            tui.Log($"Mode: {mode}, Focus: {focus ?? "none"}");

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tui.Log("Cancellation requested", LogLevel.Warning);
                    break;
                }

                var fileName = Path.GetFileName(file);
                tui.SetCurrentFile(fileName);

                var result = await ProcessFileAsync(file, mode, focus, progress, cancellationToken);
                results.Add(result);

                // Save immediately via callback
                if (onFileCompleted != null && result.Success)
                    try
                    {
                        await onFileCompleted(result);
                        tui.Log($"Saved output for {fileName}");
                    }
                    catch (Exception ex)
                    {
                        tui.Log($"Failed to save output: {ex.Message}", LogLevel.Error);
                    }

                if (result.Success)
                {
                    successCount++;
                    tui.FileCompleted(true);
                }
                else
                {
                    failureCount++;
                    tui.FileCompleted(false, result.Error);
                    await LogErrorAsync(file, result.Error ?? "Unknown error", result.StackTrace);
                }

                // Stop on error if configured
                if (!result.Success && !_config.ContinueOnError)
                {
                    tui.Log("Stopping due to error (ContinueOnError=false)", LogLevel.Warning);
                    break;
                }
            }

            tui.Log($"Batch complete: {successCount} succeeded, {failureCount} failed", LogLevel.Success);
        }, cancellationToken);

        var elapsed = DateTime.UtcNow - startTime;

        return new BatchSummary(
            files.Count,
            successCount,
            failureCount,
            results,
            elapsed);
    }

    /// <summary>
    ///     Process a single file with progress reporting
    /// </summary>
    private async Task<BatchResult> ProcessFileAsync(
        string filePath,
        SummarizationMode mode,
        string? focus,
        IProgressReporter progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            progress.ReportStage("Converting document...", 0.1f);

            // Create a summarizer that reports to our progress
            var summary = await _summarizer.SummarizeWithProgressAsync(
                filePath,
                mode,
                focus,
                progress);

            sw.Stop();
            progress.ReportStage("Complete", 1.0f);

            return new BatchResult(filePath, true, summary, null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            progress.ReportStage($"Error: {ex.Message}");
            progress.ReportLog($"Error processing {Path.GetFileName(filePath)}: {ex.Message}", LogLevel.Error);

            return new BatchResult(filePath, false, null, ex.Message, sw.Elapsed, ex.StackTrace);
        }
    }

    private async Task LogErrorAsync(string filePath, string error, string? stackTrace)
    {
        if (string.IsNullOrEmpty(_errorLogPath)) return;

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

    /// <summary>
    ///     Find files matching the configured patterns
    /// </summary>
    private List<string> FindMatchingFiles(string directoryPath)
    {
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