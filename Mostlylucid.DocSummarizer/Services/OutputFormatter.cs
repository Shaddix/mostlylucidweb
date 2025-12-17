using System.Text;
using System.Text.Json;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Formats output in various formats
/// </summary>
public static class OutputFormatter
{
    /// <summary>
    /// Format a document summary
    /// </summary>
    public static string Format(DocumentSummary summary, OutputConfig config, string fileName)
    {
        return config.Format switch
        {
            OutputFormat.Console => FormatConsole(summary, config),
            OutputFormat.Text => FormatText(summary, config, fileName),
            OutputFormat.Markdown => FormatMarkdown(summary, config, fileName),
            OutputFormat.Json => FormatJson(summary),
            _ => FormatConsole(summary, config)
        };
    }

    /// <summary>
    /// Format batch summary
    /// </summary>
    public static string FormatBatch(BatchSummary batch, OutputConfig config)
    {
        return config.Format switch
        {
            OutputFormat.Console => FormatBatchConsole(batch, config),
            OutputFormat.Text => FormatBatchText(batch, config),
            OutputFormat.Markdown => FormatBatchMarkdown(batch, config),
            OutputFormat.Json => FormatBatchJson(batch),
            _ => FormatBatchConsole(batch, config)
        };
    }

    private static string FormatConsole(DocumentSummary summary, OutputConfig config)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine(summary.ExecutiveSummary);
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        if (config.IncludeTopics && summary.TopicSummaries.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Topic Summaries");
            sb.AppendLine();
            foreach (var topic in summary.TopicSummaries)
            {
                sb.AppendLine($"**{topic.Topic}** [{string.Join(", ", topic.SourceChunks)}]");
                sb.AppendLine(topic.Summary);
                sb.AppendLine();
            }
        }

        if (config.IncludeOpenQuestions && summary.OpenQuestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Open Questions");
            sb.AppendLine();
            foreach (var q in summary.OpenQuestions)
            {
                sb.AppendLine($"- {q}");
            }
        }

        if (config.IncludeTrace)
        {
            sb.AppendLine();
            sb.AppendLine("### Trace");
            sb.AppendLine();
            sb.AppendLine($"- Document: {summary.Trace.DocumentId}");
            sb.AppendLine($"- Chunks: {summary.Trace.TotalChunks} total, {summary.Trace.ChunksProcessed} processed");
            sb.AppendLine($"- Topics: {summary.Trace.Topics.Count}");
            sb.AppendLine($"- Time: {summary.Trace.TotalTime.TotalSeconds:F1}s");
            sb.AppendLine($"- Coverage: {summary.Trace.CoverageScore:P0}");
            sb.AppendLine($"- Citation rate: {summary.Trace.CitationRate:F2}");
        }

        return sb.ToString();
    }

    private static string FormatText(DocumentSummary summary, OutputConfig config, string fileName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"DOCUMENT SUMMARY: {fileName}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("EXECUTIVE SUMMARY");
        sb.AppendLine(new string('-', 80));
        sb.AppendLine(summary.ExecutiveSummary);
        sb.AppendLine();

        if (config.IncludeTopics && summary.TopicSummaries.Count > 0)
        {
            sb.AppendLine("TOPIC SUMMARIES");
            sb.AppendLine(new string('-', 80));
            foreach (var topic in summary.TopicSummaries)
            {
                sb.AppendLine($"{topic.Topic} [{string.Join(", ", topic.SourceChunks)}]");
                sb.AppendLine(topic.Summary);
                sb.AppendLine();
            }
        }

        if (config.IncludeOpenQuestions && summary.OpenQuestions.Count > 0)
        {
            sb.AppendLine("OPEN QUESTIONS");
            sb.AppendLine(new string('-', 80));
            foreach (var q in summary.OpenQuestions)
            {
                sb.AppendLine($"- {q}");
            }
            sb.AppendLine();
        }

        if (config.IncludeTrace)
        {
            sb.AppendLine("PROCESSING TRACE");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"Document: {summary.Trace.DocumentId}");
            sb.AppendLine($"Chunks: {summary.Trace.TotalChunks} total, {summary.Trace.ChunksProcessed} processed");
            sb.AppendLine($"Topics: {summary.Trace.Topics.Count}");
            sb.AppendLine($"Time: {summary.Trace.TotalTime.TotalSeconds:F1}s");
            sb.AppendLine($"Coverage: {summary.Trace.CoverageScore:P0}");
            sb.AppendLine($"Citation rate: {summary.Trace.CitationRate:F2}");
        }

        return sb.ToString();
    }

    private static string FormatMarkdown(DocumentSummary summary, OutputConfig config, string fileName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# Document Summary: {fileName}");
        sb.AppendLine();
        sb.AppendLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine();
        sb.AppendLine(summary.ExecutiveSummary);
        sb.AppendLine();

        if (config.IncludeTopics && summary.TopicSummaries.Count > 0)
        {
            sb.AppendLine("## Topic Summaries");
            sb.AppendLine();
            foreach (var topic in summary.TopicSummaries)
            {
                sb.AppendLine($"### {topic.Topic}");
                sb.AppendLine();
                sb.AppendLine($"*Sources: {string.Join(", ", topic.SourceChunks)}*");
                sb.AppendLine();
                sb.AppendLine(topic.Summary);
                sb.AppendLine();
            }
        }

        if (config.IncludeOpenQuestions && summary.OpenQuestions.Count > 0)
        {
            sb.AppendLine("## Open Questions");
            sb.AppendLine();
            foreach (var q in summary.OpenQuestions)
            {
                sb.AppendLine($"- {q}");
            }
            sb.AppendLine();
        }

        if (config.IncludeTrace)
        {
            sb.AppendLine("## Processing Trace");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Document | {summary.Trace.DocumentId} |");
            sb.AppendLine($"| Chunks | {summary.Trace.TotalChunks} total, {summary.Trace.ChunksProcessed} processed |");
            sb.AppendLine($"| Topics | {summary.Trace.Topics.Count} |");
            sb.AppendLine($"| Time | {summary.Trace.TotalTime.TotalSeconds:F1}s |");
            sb.AppendLine($"| Coverage | {summary.Trace.CoverageScore:P0} |");
            sb.AppendLine($"| Citation rate | {summary.Trace.CitationRate:F2} |");
        }

        return sb.ToString();
    }

    private static string FormatJson(DocumentSummary summary)
    {
        return JsonSerializer.Serialize(summary, DocSummarizerJsonContext.Default.DocumentSummary);
    }

    private static string FormatBatchConsole(BatchSummary batch, OutputConfig config)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("BATCH PROCESSING COMPLETE");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"Total files: {batch.TotalFiles}");
        sb.AppendLine($"Success: {batch.SuccessCount} ({batch.SuccessRate:P0})");
        sb.AppendLine($"Failed: {batch.FailureCount}");
        sb.AppendLine($"Total time: {batch.TotalTime.TotalSeconds:F1}s");
        sb.AppendLine($"Average time: {(batch.SuccessCount > 0 ? batch.TotalTime.TotalSeconds / batch.SuccessCount : 0):F1}s/file");
        sb.AppendLine();

        if (batch.FailureCount > 0 && config.Verbose)
        {
            sb.AppendLine("FAILED FILES:");
            foreach (var result in batch.Results.Where(r => !r.Success))
            {
                sb.AppendLine($"- {Path.GetFileName(result.FilePath)}: {result.Error}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatBatchText(BatchSummary batch, OutputConfig config)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("BATCH PROCESSING SUMMARY");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('-', 80));
        sb.AppendLine($"Total files: {batch.TotalFiles}");
        sb.AppendLine($"Success: {batch.SuccessCount} ({batch.SuccessRate:P0})");
        sb.AppendLine($"Failed: {batch.FailureCount}");
        sb.AppendLine($"Total time: {batch.TotalTime.TotalSeconds:F1}s");
        sb.AppendLine($"Average time: {(batch.SuccessCount > 0 ? batch.TotalTime.TotalSeconds / batch.SuccessCount : 0):F1}s/file");
        sb.AppendLine();

        sb.AppendLine("RESULTS:");
        foreach (var result in batch.Results)
        {
            var status = result.Success ? "OK" : "FAILED";
            sb.AppendLine($"[{status}] {Path.GetFileName(result.FilePath)} ({result.ProcessingTime.TotalSeconds:F1}s)");
            if (!result.Success && result.Error != null)
            {
                sb.AppendLine($"  Error: {result.Error}");
            }
        }

        return sb.ToString();
    }

    private static string FormatBatchMarkdown(BatchSummary batch, OutputConfig config)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Batch Processing Summary");
        sb.AppendLine();
        sb.AppendLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total files | {batch.TotalFiles} |");
        sb.AppendLine($"| Success | {batch.SuccessCount} ({batch.SuccessRate:P0}) |");
        sb.AppendLine($"| Failed | {batch.FailureCount} |");
        sb.AppendLine($"| Total time | {batch.TotalTime.TotalSeconds:F1}s |");
        sb.AppendLine($"| Average time | {(batch.SuccessCount > 0 ? batch.TotalTime.TotalSeconds / batch.SuccessCount : 0):F1}s/file |");
        sb.AppendLine();

        sb.AppendLine("## Results");
        sb.AppendLine();
        sb.AppendLine("| File | Status | Time |");
        sb.AppendLine("|------|--------|------|");
        foreach (var result in batch.Results)
        {
            var status = result.Success ? "✓" : "✗";
            sb.AppendLine($"| {Path.GetFileName(result.FilePath)} | {status} | {result.ProcessingTime.TotalSeconds:F1}s |");
        }

        if (batch.FailureCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Errors");
            sb.AppendLine();
            foreach (var result in batch.Results.Where(r => !r.Success))
            {
                sb.AppendLine($"### {Path.GetFileName(result.FilePath)}");
                sb.AppendLine();
                sb.AppendLine($"```");
                sb.AppendLine(result.Error);
                sb.AppendLine($"```");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatBatchJson(BatchSummary batch)
    {
        return JsonSerializer.Serialize(batch, DocSummarizerJsonContext.Default.BatchSummary);
    }

    /// <summary>
    /// Write output to appropriate destination
    /// </summary>
    public static async Task WriteOutputAsync(string content, OutputConfig config, string fileName, string? outputDir = null)
    {
        if (config.Format == OutputFormat.Console)
        {
            Console.WriteLine(content);
            return;
        }

        // Determine output directory
        var directory = outputDir ?? config.OutputDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Determine file extension
        var extension = config.Format switch
        {
            OutputFormat.Text => ".txt",
            OutputFormat.Markdown => ".md",
            OutputFormat.Json => ".json",
            _ => ".txt"
        };

        // Create output file path
        var baseFileName = Path.GetFileNameWithoutExtension(fileName);
        var outputPath = Path.Combine(directory, $"{baseFileName}_summary{extension}");

        // Write file
        await File.WriteAllTextAsync(outputPath, content);

        if (config.Verbose)
        {
            Console.WriteLine($"[Output] Written to: {outputPath}");
        }
    }
}
