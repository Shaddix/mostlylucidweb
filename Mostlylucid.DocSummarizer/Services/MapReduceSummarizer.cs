using System.Diagnostics;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

public class MapReduceSummarizer
{
    private readonly OllamaService _ollama;
    private readonly ProgressService _progress;
    private readonly bool _verbose;
    private readonly int _maxParallelism;
    private readonly int _contextWindow;

    /// <summary>
    /// Default max parallelism for LLM calls. Ollama processes one request at a time per model,
    /// so high values just queue requests. 8 is a good balance for throughput vs memory.
    /// </summary>
    public const int DefaultMaxParallelism = 8;
    
    /// <summary>
    /// Target percentage of context window to use for reduce phase input.
    /// Leave room for the prompt template and output generation.
    /// </summary>
    private const double ContextWindowTargetPercent = 0.6;
    
    /// <summary>
    /// Approximate characters per token for estimation (conservative estimate)
    /// </summary>
    private const double CharsPerToken = 4.0;

    public MapReduceSummarizer(OllamaService ollama, bool verbose = false, int maxParallelism = DefaultMaxParallelism, int contextWindow = 8192)
    {
        _ollama = ollama;
        _verbose = verbose;
        _progress = new ProgressService(verbose);
        _maxParallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
        _contextWindow = contextWindow;
    }
    
    /// <summary>
    /// Create a MapReduceSummarizer with auto-detected context window from the model
    /// </summary>
    public static async Task<MapReduceSummarizer> CreateAsync(OllamaService ollama, bool verbose = false, int maxParallelism = DefaultMaxParallelism)
    {
        var contextWindow = await ollama.GetContextWindowAsync();
        return new MapReduceSummarizer(ollama, verbose, maxParallelism, contextWindow);
    }

    public async Task<DocumentSummary> SummarizeAsync(string docId, List<DocumentChunk> chunks)
    {
        var sw = Stopwatch.StartNew();

        // Always show basic progress
        var parallelDesc = _maxParallelism <= 0 ? "unlimited" : _maxParallelism.ToString();
        Console.WriteLine($"Map Phase: Summarizing {chunks.Count} chunks ({parallelDesc} parallel)...");
        Console.Out.Flush();

        // Map phase: summarize each chunk in parallel with controlled concurrency
        List<ChunkSummary> chunkSummaries;
        
        if (_verbose)
        {
            _progress.Rule("Map Phase");
            _progress.Info($"Summarizing {chunks.Count} chunks ({parallelDesc} parallel, timeout: {OllamaService.DefaultTimeout.TotalMinutes:F0} min/chunk)");
            
            chunkSummaries = await _progress.WithLiveTableAsync(
                $"Processing {chunks.Count} Chunks",
                chunks,
                c => c.Order,
                c => string.IsNullOrEmpty(c.Heading) ? $"Chunk {c.Order}" : c.Heading,
                async chunk => await SummarizeChunkAsync(chunk),
                _maxParallelism);
        }
        else
        {
            // Use controlled parallelism to avoid resource exhaustion on large documents
            chunkSummaries = await ProcessChunksWithLimitedParallelismAsync(chunks);
        }

        // Reduce phase: merge into final summary
        Console.WriteLine($"Reduce Phase: Merging {chunkSummaries.Count} summaries...");
        Console.Out.Flush();
        
        if (_verbose)
        {
            _progress.Rule("Reduce Phase");
            _progress.Info($"Merging {chunkSummaries.Count} summaries into final document...");
        }

        var result = await _progress.WithStatusAsync(
            "Generating final summary...",
            async () => await ReduceAsync(chunkSummaries));

        sw.Stop();
        
        var headings = chunks.Select(c => c.Heading).Where(h => !string.IsNullOrEmpty(h)).ToList();
        var coverage = CalculateCoverage(chunkSummaries, headings);
        var citationRate = CalculateCitationRate(result.ExecutiveSummary);
        
        // Clear chunk summaries to free memory
        chunkSummaries.Clear();

        Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:F1}s");
        
        if (_verbose)
        {
            _progress.Success($"Completed in {sw.Elapsed.TotalSeconds:F1}s");
        }

        return result with
        {
            Trace = new SummarizationTrace(
                docId, chunks.Count, chunks.Count,
                headings, sw.Elapsed, coverage, citationRate)
        };
    }

    private async Task<List<ChunkSummary>> ProcessChunksWithLimitedParallelismAsync(List<DocumentChunk> chunks)
    {
        var results = new ChunkSummary[chunks.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxParallelism };
        var completed = 0;
        var lockObj = new object();
        
        await Parallel.ForEachAsync(
            chunks.Select((chunk, index) => (chunk, index)),
            options,
            async (item, ct) =>
            {
                results[item.index] = await SummarizeChunkAsync(item.chunk);
                
                // Thread-safe progress update
                int current;
                lock (lockObj)
                {
                    completed++;
                    current = completed;
                }
                Console.Write($"\r  Progress: {current}/{chunks.Count} chunks completed");
                Console.Out.Flush();
            });
        
        Console.WriteLine(); // New line after progress
        return results.ToList();
    }

    private async Task<ChunkSummary> SummarizeChunkAsync(DocumentChunk chunk)
    {
        // Truncate content for small models (~2000 chars max)
        const int maxContentLength = 2000;
        var content = chunk.Content.Length > maxContentLength 
            ? chunk.Content[..maxContentLength] + "..." 
            : chunk.Content;
        
        var prompt = $"""
            Section: {chunk.Heading}
            Content: {content}

            Write 2-3 bullet points summarizing the key facts. Include [{chunk.Id}] at the end of each bullet.
            """;

        var response = await _ollama.GenerateAsync(prompt);

        return new ChunkSummary(chunk.Id, chunk.Heading, response, chunk.Order);
    }

    private async Task<DocumentSummary> ReduceAsync(List<ChunkSummary> summaries, bool retry = false)
    {
        var ordered = summaries.OrderBy(s => s.Order).ToList();
        var validChunkIds = ordered.Select(s => s.ChunkId).ToHashSet();
        
        // Check if we need hierarchical reduction
        var estimatedTokens = EstimateTokens(ordered);
        var maxTokens = (int)(_contextWindow * ContextWindowTargetPercent);
        
        if (estimatedTokens > maxTokens && ordered.Count > 2)
        {
            // Hierarchical reduction needed
            return await HierarchicalReduceAsync(ordered, validChunkIds, retry);
        }
        
        // Single-pass reduction
        return await SingleReduceAsync(ordered, validChunkIds, retry, isFinal: true);
    }
    
    /// <summary>
    /// Hierarchical reduction: batch summaries, reduce each batch, then reduce the batch results
    /// </summary>
    private async Task<DocumentSummary> HierarchicalReduceAsync(List<ChunkSummary> summaries, HashSet<string> validChunkIds, bool retry)
    {
        var maxTokens = (int)(_contextWindow * ContextWindowTargetPercent);
        var batches = CreateBatches(summaries, maxTokens);
        
        if (_verbose)
        {
            _progress.Info($"Document too large for single reduction ({summaries.Count} summaries, ~{EstimateTokens(summaries):N0} tokens)");
            _progress.Info($"Using hierarchical reduction: {batches.Count} batches → intermediate summaries → final summary");
        }
        else
        {
            Console.WriteLine($"  Hierarchical reduction: {batches.Count} batches (document too large for single pass)");
        }
        
        // Reduce each batch to an intermediate summary
        var intermediateSummaries = new List<ChunkSummary>();
        var batchNum = 0;
        
        foreach (var batch in batches)
        {
            batchNum++;
            if (_verbose)
            {
                _progress.Info($"Reducing batch {batchNum}/{batches.Count} ({batch.Count} summaries)...");
            }
            else
            {
                Console.Write($"\r  Reducing batch {batchNum}/{batches.Count}...");
                Console.Out.Flush();
            }
            
            var batchResult = await SingleReduceAsync(batch, validChunkIds, retry: false, isFinal: false);
            
            // Create an intermediate summary that preserves citations from this batch
            var batchChunkIds = batch.Select(s => s.ChunkId).ToList();
            var intermediateHeading = batch.Count == 1 
                ? batch[0].Heading 
                : $"Sections {batch.First().Order + 1}-{batch.Last().Order + 1}";
            
            intermediateSummaries.Add(new ChunkSummary(
                $"batch-{batchNum}",
                intermediateHeading,
                batchResult.ExecutiveSummary,
                batch.First().Order
            ));
        }
        
        if (!_verbose)
        {
            Console.WriteLine(); // New line after batch progress
        }
        
        // Check if we need another level of reduction
        var intermediateTokens = EstimateTokens(intermediateSummaries);
        if (intermediateTokens > maxTokens && intermediateSummaries.Count > 2)
        {
            if (_verbose)
            {
                _progress.Info($"Intermediate summaries still too large (~{intermediateTokens:N0} tokens), adding another reduction level...");
            }
            return await HierarchicalReduceAsync(intermediateSummaries, validChunkIds, retry);
        }
        
        // Final reduction
        if (_verbose)
        {
            _progress.Info($"Final reduction of {intermediateSummaries.Count} intermediate summaries...");
        }
        else
        {
            Console.WriteLine($"  Final reduction of {intermediateSummaries.Count} intermediate summaries...");
        }
        
        return await SingleReduceAsync(intermediateSummaries, validChunkIds, retry, isFinal: true);
    }
    
    /// <summary>
    /// Create batches of summaries that fit within the token limit
    /// </summary>
    private List<List<ChunkSummary>> CreateBatches(List<ChunkSummary> summaries, int maxTokensPerBatch)
    {
        var batches = new List<List<ChunkSummary>>();
        var currentBatch = new List<ChunkSummary>();
        var currentTokens = 0;
        
        // Reserve some tokens for the prompt template
        var effectiveMax = (int)(maxTokensPerBatch * 0.85);
        
        foreach (var summary in summaries)
        {
            var summaryTokens = EstimateTokens(summary);
            
            if (currentBatch.Count > 0 && currentTokens + summaryTokens > effectiveMax)
            {
                // Start a new batch
                batches.Add(currentBatch);
                currentBatch = new List<ChunkSummary>();
                currentTokens = 0;
            }
            
            currentBatch.Add(summary);
            currentTokens += summaryTokens;
        }
        
        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }
        
        // If we ended up with just one batch, force split it
        if (batches.Count == 1 && summaries.Count > 2)
        {
            var midpoint = summaries.Count / 2;
            batches = new List<List<ChunkSummary>>
            {
                summaries.Take(midpoint).ToList(),
                summaries.Skip(midpoint).ToList()
            };
        }
        
        return batches;
    }
    
    /// <summary>
    /// Single-pass reduction of summaries
    /// </summary>
    private async Task<DocumentSummary> SingleReduceAsync(List<ChunkSummary> summaries, HashSet<string> validChunkIds, bool retry, bool isFinal)
    {
        var ordered = summaries.OrderBy(s => s.Order).ToList();
        
        // Truncate each summary for small models
        const int maxSummaryLength = 300;
        var sectionsText = string.Join("\n", ordered.Select(s =>
        {
            var truncated = s.Summary.Length > maxSummaryLength 
                ? s.Summary[..maxSummaryLength] + "..." 
                : s.Summary;
            return $"[{s.ChunkId}] {s.Heading}: {truncated}";
        }));
        
        // Use different prompts for intermediate vs final reduction
        string prompt;
        if (isFinal)
        {
            prompt = $"""
                Section summaries:
                {sectionsText}

                Write an executive summary (3-5 sentences) of the most important points.
                Then list any unclear items under "Open Questions:".
                """;
        }
        else
        {
            // Intermediate reduction - more condensed
            prompt = $"""
                Summaries:
                {sectionsText}

                Combine into 3-5 bullet points. Keep [chunk-N] references.
                """;
        }

        var response = await _ollama.GenerateAsync(prompt);
        
        // Skip citation validation for small models - they struggle with it
        
        return new DocumentSummary(
            response,
            ordered.Select(s => new TopicSummary(s.Heading, s.Summary, [s.ChunkId])).ToList(),
            isFinal ? ExtractOpenQuestions(response) : new List<string>(),
            new SummarizationTrace("", 0, 0, [], TimeSpan.Zero, 0, 0));
    }
    
    /// <summary>
    /// Estimate token count for a list of summaries
    /// </summary>
    private static int EstimateTokens(List<ChunkSummary> summaries)
    {
        return summaries.Sum(s => EstimateTokens(s));
    }
    
    /// <summary>
    /// Estimate token count for a single summary
    /// </summary>
    private static int EstimateTokens(ChunkSummary summary)
    {
        var text = $"## {summary.Heading} [{summary.ChunkId}]\n{summary.Summary}";
        return (int)(text.Length / CharsPerToken);
    }

    private static List<string> ExtractOpenQuestions(string response)
    {
        var lines = response.Split('\n');
        var inQuestions = false;
        var questions = new List<string>();

        foreach (var line in lines)
        {
            if (line.Contains("Open Questions", StringComparison.OrdinalIgnoreCase))
            {
                inQuestions = true;
                continue;
            }
            if (inQuestions && line.TrimStart().StartsWith('-'))
            {
                questions.Add(line.TrimStart('-', ' '));
            }
            if (inQuestions && line.StartsWith("##"))
            {
                break;
            }
        }
        return questions;
    }

    private static double CalculateCoverage(List<ChunkSummary> summaries, List<string> headings)
    {
        // Coverage = % of top-level headings that have a non-empty summary
        // For MapReduce, all chunks are processed so coverage is always 1.0
        // unless some chunks produced empty summaries
        if (headings.Count == 0) return 1.0;
        var coveredHeadings = summaries
            .Where(s => !string.IsNullOrWhiteSpace(s.Summary) && 
                       !s.Summary.Contains("Limited coverage", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Heading)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var covered = headings.Count(h => coveredHeadings.Contains(h));
        return (double)covered / headings.Count;
    }

    private static double CalculateCitationRate(string summary)
    {
        var bullets = summary.Split('\n').Count(l => l.TrimStart().StartsWith('-'));
        if (bullets == 0) return 0;
        var citations = System.Text.RegularExpressions.Regex.Matches(summary, @"\[chunk-\d+\]").Count;
        return (double)citations / bullets;
    }
}
