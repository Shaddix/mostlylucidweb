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
    private readonly DocumentSummarizer _summarizer;
    private readonly BatchConfig _config;
    private readonly bool _verbose;

    public BatchProcessor(DocumentSummarizer summarizer, BatchConfig config, bool verbose = false)
    {
        _summarizer = summarizer;
        _config = config;
        _verbose = verbose;
    }

    /// <summary>
    /// Process all documents in a directory, saving each immediately
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
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var sw = Stopwatch.StartNew();
        
        // Only track stats, NOT full results - avoid OOM on large batches
        var successCount = 0;
        var failureCount = 0;
        var failedFiles = new List<(string Path, string Error)>();

        // Find matching files
        var files = FindMatchingFiles(directoryPath);
        var totalFiles = files.Count;
        
        if (_verbose)
        {
            Console.WriteLine($"[Batch] Found {totalFiles} files to process");
        }

        // Process files one at a time, save immediately, discard from memory
        var processedCount = 0;
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            processedCount++;
            if (_verbose)
            {
                Console.WriteLine($"[Batch] Processing {processedCount}/{totalFiles}: {Path.GetFileName(file)}");
            }

            var result = await ProcessFileAsync(file, mode, focus, cancellationToken);
            
            // Save immediately via callback
            if (onFileCompleted != null)
            {
                await onFileCompleted(result);
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
                    failedFiles.Add((file, result.Error));
                }
            }
            
            // Let GC reclaim the summary memory
            result = null;

            // Stop on error if configured
            if (failureCount > 0 && !_config.ContinueOnError)
            {
                break;
            }
        }

        sw.Stop();

        // Return lightweight summary without full results
        var summary = new BatchSummary(
            totalFiles,
            successCount,
            failureCount,
            failedFiles.Select(f => new BatchResult(f.Path, false, null, f.Error, TimeSpan.Zero)).ToList(),
            sw.Elapsed);

        return summary;
    }

    /// <summary>
    /// Process a single file with error handling
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
            
            if (_verbose)
            {
                Console.WriteLine($"  [Error] {ex.Message}");
            }
            
            return new BatchResult(filePath, false, null, ex.Message, sw.Elapsed);
        }
    }

    /// <summary>
    /// Find files matching the configured patterns
    /// </summary>
    private List<string> FindMatchingFiles(string directoryPath)
    {
        var matcher = new Matcher();
        
        // Add include patterns
        foreach (var pattern in _config.IncludePatterns)
        {
            matcher.AddInclude(pattern);
        }

        // Add exclude patterns
        foreach (var pattern in _config.ExcludePatterns)
        {
            matcher.AddExclude(pattern);
        }

        // Execute matching - simpler approach that's case-insensitive
        var searchOption = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*.*", searchOption);

        var matchedFiles = new List<string>();
        foreach (var file in files)
        {
            // Case-insensitive extension check
            if (_config.FileExtensions.Count == 0 || 
                _config.FileExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                // Check exclude patterns
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var excluded = _config.ExcludePatterns.Any(p => 
                    relativePath.Contains(p, StringComparison.OrdinalIgnoreCase));
                
                if (!excluded)
                {
                    matchedFiles.Add(file);
                }
            }
        }

        return matchedFiles;
    }
}
