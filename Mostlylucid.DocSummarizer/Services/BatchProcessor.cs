using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Handles batch processing of multiple documents
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
    /// Process all documents in a directory, saving each immediately
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

        var sw = Stopwatch.StartNew();

        var successCount = 0;
        var failureCount = 0;
        var failedFiles = new List<(string Path, string Error, string? StackTrace)>();

        var files = FindMatchingFiles(directoryPath);
        var totalFiles = files.Count;

        Console.WriteLine($"Found {totalFiles} files to process");
        Console.WriteLine();

        if (totalFiles == 0)
        {
            Console.WriteLine("No files found matching criteria");
            return new BatchSummary(0, 0, 0, new List<BatchResult>(), sw.Elapsed);
        }

        using var _ = ProgressService.EnterInteractiveContext();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (cancellationToken.IsCancellationRequested) break;

            var fileName = Path.GetFileName(file);
            var progress = (i + 1) * 100 / totalFiles;
            
            Console.WriteLine($"[{i + 1}/{totalFiles}] ({progress}%) Processing: {TruncateFileName(fileName, 50)}");
            Console.Out.Flush();

            var result = await ProcessFileAsync(file, mode, focus, cancellationToken);

            if (onFileCompleted != null)
            {
                try
                {
                    await onFileCompleted(result);
                }
                catch (Exception ex)
                {
                    await LogErrorAsync(file, $"Failed to save output: {ex.Message}", ex.StackTrace);
                }
            }

            if (result.Success)
            {
                successCount++;
                if (_verbose) Console.WriteLine($"  [OK] Completed in {result.ProcessingTime.TotalSeconds:F1}s");
            }
            else
            {
                failureCount++;
                Console.WriteLine($"  [ERROR] {result.Error}");
                if (result.Error != null)
                {
                    failedFiles.Add((file, result.Error, result.StackTrace));
                    await LogErrorAsync(file, result.Error, result.StackTrace);
                }
            }

            result = null; // Allow GC

            if (failureCount > 0 && !_config.ContinueOnError) break;
        }

        sw.Stop();

        Console.WriteLine();
        DisplayBatchSummary(totalFiles, successCount, failureCount, failedFiles, sw.Elapsed);

        return new BatchSummary(
            totalFiles,
            successCount,
            failureCount,
            failedFiles.Select(f => new BatchResult(f.Path, false, null, f.Error, TimeSpan.Zero)).ToList(),
            sw.Elapsed);
    }

    private void DisplayBatchSummary(int total, int success, int failed,
        List<(string Path, string Error, string? StackTrace)> failedFiles, TimeSpan elapsed)
    {
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("  BATCH PROCESSING SUMMARY");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"  Total Files:  {total}");
        Console.WriteLine($"  Successful:   {success}");
        Console.WriteLine($"  Failed:       {failed}");
        Console.WriteLine($"  Success Rate: {(total > 0 ? (double)success / total * 100 : 0):F1}%");
        Console.WriteLine($"  Duration:     {elapsed.TotalMinutes:F1} minutes");
        Console.WriteLine(new string('=', 50));

        if (failedFiles.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("FAILED FILES:");
            foreach (var (path, error, _) in failedFiles.Take(20))
            {
                var fileName = Path.GetFileName(path);
                var shortError = error.Length > 50 ? error[..47] + "..." : error;
                Console.WriteLine($"  - {TruncateFileName(fileName, 30)}: {shortError}");
            }

            if (failedFiles.Count > 20)
                Console.WriteLine($"  ... and {failedFiles.Count - 20} more");

            if (!string.IsNullOrEmpty(_errorLogPath))
                Console.WriteLine($"\nFull error details logged to: {_errorLogPath}");
        }
    }

    private async Task LogErrorAsync(string filePath, string error, string? stackTrace)
    {
        if (!string.IsNullOrEmpty(_errorLogPath))
        {
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
            return new BatchResult(filePath, false, null, ex.Message, sw.Elapsed, ex.StackTrace);
        }
    }

    private List<string> FindMatchingFiles(string directoryPath)
    {
        var searchOption = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var matchedFiles = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.*", searchOption))
        {
            if (_config.FileExtensions.Count == 0 ||
                _config.FileExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var excluded = _config.ExcludePatterns.Any(p =>
                    relativePath.Contains(p, StringComparison.OrdinalIgnoreCase));

                if (!excluded) matchedFiles.Add(file);
            }
        }

        return matchedFiles;
    }
}
