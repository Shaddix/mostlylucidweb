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
    /// Process all documents in a directory
    /// </summary>
    public async Task<BatchSummary> ProcessDirectoryAsync(
        string directoryPath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var sw = Stopwatch.StartNew();
        var results = new List<BatchResult>();

        // Find matching files
        var files = FindMatchingFiles(directoryPath);
        
        if (_verbose)
        {
            Console.WriteLine($"[Batch] Found {files.Count} files to process");
        }

        // Process files
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
                Console.WriteLine($"[Batch] Processing {processedCount}/{files.Count}: {Path.GetFileName(file)}");
            }

            var result = await ProcessFileAsync(file, mode, focus, cancellationToken);
            results.Add(result);

            // Stop on error if configured
            if (!result.Success && !_config.ContinueOnError)
            {
                break;
            }
        }

        sw.Stop();

        var summary = new BatchSummary(
            files.Count,
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            results,
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

        // Add file extension filters
        if (_config.FileExtensions.Count > 0)
        {
            foreach (var ext in _config.FileExtensions)
            {
                matcher.AddInclude($"**/*{ext}");
            }
        }

        // Execute matching
        var searchOption = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, "*.*", searchOption);

        var matchedFiles = new List<string>();
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(directoryPath, file);
            if (matcher.Match(relativePath).HasMatches)
            {
                // Additional extension check
                if (_config.FileExtensions.Count == 0 || 
                    _config.FileExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedFiles.Add(file);
                }
            }
        }

        return matchedFiles;
    }
}
