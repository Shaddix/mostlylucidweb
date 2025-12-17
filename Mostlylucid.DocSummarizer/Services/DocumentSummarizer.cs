using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentSummarizer
{
    private readonly DoclingClient _docling;
    private readonly OllamaService _ollama;
    private readonly RagSummarizer _rag;
    private readonly ProgressService _progress;
    private readonly ProcessingConfig _processingConfig;
    private readonly bool _verbose;
    private readonly int _maxLlmParallelism;
    private int? _cachedContextWindow;
    private MapReduceSummarizer? _mapReduce;
    
    /// <summary>
    /// Threshold for using temp file streaming (1MB)
    /// </summary>
    private const int LargeFileSizeThreshold = 1024 * 1024;
    
    /// <summary>
    /// Temp directory for intermediate files
    /// </summary>
    private string? _tempDir;

    public DocumentSummarizer(
        string ollamaModel = "qwen2.5:1.5b",
        string doclingUrl = "http://localhost:5001",
        string qdrantHost = "localhost",
        bool verbose = false,
        DoclingConfig? doclingConfig = null,
        ProcessingConfig? processingConfig = null,
        QdrantConfig? qdrantConfig = null)
    {
        _verbose = verbose;
        _progress = new ProgressService(verbose);
        _docling = new DoclingClient(doclingConfig ?? new DoclingConfig { BaseUrl = doclingUrl });
        
        _processingConfig = processingConfig ?? new ProcessingConfig();
        _maxLlmParallelism = _processingConfig.MaxLlmParallelism > 0 
            ? _processingConfig.MaxLlmParallelism 
            : MapReduceSummarizer.DefaultMaxParallelism;
        
        _ollama = new OllamaService(ollamaModel);
        _rag = new RagSummarizer(_ollama, qdrantHost, verbose, _maxLlmParallelism, qdrantConfig);
    }
    
    /// <summary>
    /// Get or create temp directory for intermediate files
    /// </summary>
    private string GetTempDir()
    {
        if (_tempDir == null)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"docsummarizer_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            if (_verbose) Console.WriteLine($"[Temp] Using temp directory: {_tempDir}");
        }
        return _tempDir;
    }
    
    /// <summary>
    /// Clean up temp directory
    /// </summary>
    private void CleanupTempDir()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
                if (_verbose) Console.WriteLine("[Temp] Cleaned up temp directory");
            }
            catch
            {
                // Ignore cleanup errors
            }
            _tempDir = null;
        }
    }
    
    /// <summary>
    /// Get or create the MapReduce summarizer with context-window-aware settings
    /// </summary>
    private async Task<MapReduceSummarizer> GetMapReduceSummarizerAsync()
    {
        if (_mapReduce == null)
        {
            var contextWindow = await GetContextWindowAsync();
            _mapReduce = new MapReduceSummarizer(_ollama, _verbose, _maxLlmParallelism, contextWindow);
        }
        return _mapReduce;
    }
    
    /// <summary>
    /// Get the model's context window (cached)
    /// </summary>
    private async Task<int> GetContextWindowAsync()
    {
        if (_cachedContextWindow == null)
        {
            _cachedContextWindow = await _ollama.GetContextWindowAsync();
            if (_verbose)
            {
                Console.WriteLine($"Model context window: {_cachedContextWindow:N0} tokens");
            }
        }
        return _cachedContextWindow.Value;
    }

    public async Task<DocumentSummary> SummarizeAsync(
        string filePath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        var docId = Path.GetFileName(filePath);
        
        try
        {
            if (_verbose)
            {
                AnsiConsole.Write(new FigletText("DocSummarizer").Color(Color.Blue));
                _progress.Rule("Document Processing");
                _progress.Info($"Document: {docId}");
                _progress.Info($"Mode: {mode}");
                _progress.Info($"Timeout: {OllamaService.DefaultTimeout.TotalMinutes:F0} minutes per operation");
                if (!string.IsNullOrEmpty(focus)) _progress.Info($"Focus: {focus}");
                AnsiConsole.WriteLine();
            }

            // Check if it's already markdown - use streaming for large files
            string markdown;
            string? tempMarkdownPath = null;
            
            if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Reading markdown file...");
                Console.Out.Flush();
                
                // Check file size - stream to temp if large
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > LargeFileSizeThreshold)
                {
                    if (_verbose) Console.WriteLine($"[Memory] Large file ({fileInfo.Length / 1024:N0}KB), streaming...");
                    tempMarkdownPath = Path.Combine(GetTempDir(), "content.md");
                    File.Copy(filePath, tempMarkdownPath, overwrite: true);
                    markdown = await File.ReadAllTextAsync(tempMarkdownPath);
                }
                else
                {
                    markdown = await File.ReadAllTextAsync(filePath);
                }
            }
            else
            {
                // Always show conversion progress - this can take a long time
                Console.WriteLine($"Converting document with Docling...");
                Console.Out.Flush();
                
                markdown = await _progress.WithStatusAsync(
                    $"Converting {docId} with Docling (timeout: 5 min)...",
                    async () => await _docling.ConvertAsync(filePath));
                
                Console.WriteLine("Document converted to markdown");
                
                // For large converted content, write to temp to allow GC of the string
                if (markdown.Length > LargeFileSizeThreshold)
                {
                    if (_verbose) Console.WriteLine($"[Memory] Large content ({markdown.Length / 1024:N0}KB), caching to temp...");
                    tempMarkdownPath = Path.Combine(GetTempDir(), "content.md");
                    await File.WriteAllTextAsync(tempMarkdownPath, markdown);
                    // Force GC to reclaim the string memory before chunking
                    GC.Collect(0, GCCollectionMode.Optimized);
                }
            }

            // Chunk the document with context-aware sizing
            Console.WriteLine("Parsing document structure...");
            Console.Out.Flush();
            
            var chunker = await CreateChunkerAsync();
            var chunks = await _progress.WithStatusAsync(
                "Parsing document structure...",
                () =>
                {
                    var result = chunker.ChunkByStructure(markdown);
                    return Task.FromResult(result);
                });
            
            // Release markdown string after chunking
            markdown = null!;
            if (tempMarkdownPath != null) GC.Collect(0, GCCollectionMode.Optimized);
            
            Console.WriteLine($"Created {chunks.Count} chunks");
            Console.WriteLine();

            DocumentSummary result;
            try
            {
                result = mode switch
                {
                    SummarizationMode.MapReduce => await (await GetMapReduceSummarizerAsync()).SummarizeAsync(docId, chunks),
                    SummarizationMode.Rag => await _rag.SummarizeAsync(docId, chunks, focus),
                    SummarizationMode.Iterative => await SummarizeIterativeAsync(docId, chunks),
                    _ => throw new ArgumentException($"Unknown mode: {mode}")
                };
            }
            finally
            {
                // Clear chunks to free memory immediately
                chunks.Clear();
            }
            
            return result;
        }
        finally
        {
            // Clean up temp files
            CleanupTempDir();
        }
    }

    public async Task<string> QueryAsync(string filePath, string query)
    {
        var docId = Path.GetFileName(filePath);
        
        // Check if already markdown
        string markdown;
        if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            markdown = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            markdown = await _docling.ConvertAsync(filePath);
        }

        var chunker = await CreateChunkerAsync();
        var chunks = chunker.ChunkByStructure(markdown);
        
        // Index and query
        await _rag.IndexDocumentAsync(docId, chunks);
        
        var summary = await _rag.SummarizeAsync(docId, chunks, query);
        return summary.ExecutiveSummary;
    }

    private async Task<DocumentSummary> SummarizeIterativeAsync(string docId, List<DocumentChunk> chunks)
    {
        _progress.Rule("Iterative Summarization");
        _progress.Warning("Iterative mode is slower and may lose context on long documents (>10 chunks)");
        
        // Simple iterative approach - not recommended for long documents
        var ollama = new OllamaService();
        var summary = "";
        var orderedChunks = chunks.OrderBy(c => c.Order).ToList();

        if (_verbose)
        {
            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[blue]Processing chunks sequentially[/]", maxValue: orderedChunks.Count);
                    
                    foreach (var chunk in orderedChunks)
                    {
                        task.Description = $"[blue]Processing: {(chunk.Heading.Length > 30 ? chunk.Heading[..27] + "..." : chunk.Heading)}[/]";
                        
                        var prompt = summary.Length == 0
                            ? $"Summarize this section:\n\n{chunk.Content}\n\nSummary:"
                            : $"""
                                Current summary:
                                {summary}
                                
                                New section: {chunk.Heading}
                                {chunk.Content}
                                
                                Update the summary to incorporate this section. Be concise.
                                
                                Updated summary:
                                """;

                        summary = await ollama.GenerateAsync(prompt);
                        task.Increment(1);
                    }
                });
        }
        else
        {
            foreach (var chunk in orderedChunks)
            {
                var prompt = summary.Length == 0
                    ? $"Summarize this section:\n\n{chunk.Content}\n\nSummary:"
                    : $"""
                        Current summary:
                        {summary}
                        
                        New section: {chunk.Heading}
                        {chunk.Content}
                        
                        Update the summary to incorporate this section. Be concise.
                        
                        Updated summary:
                        """;

                summary = await ollama.GenerateAsync(prompt);
            }
        }

        _progress.Success("Iterative summarization complete");

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunks.Count, chunks.Count, [], TimeSpan.Zero, 1.0, 0));
    }

    /// <summary>
    /// Create a chunker with context-window-aware sizing
    /// </summary>
    private async Task<DocumentChunker> CreateChunkerAsync()
    {
        // Get context window from model (cache it for subsequent calls)
        if (_cachedContextWindow == null)
        {
            _cachedContextWindow = await _ollama.GetContextWindowAsync();
            if (_verbose)
            {
                Console.WriteLine($"Model context window: {_cachedContextWindow:N0} tokens");
            }
        }

        int targetChunkTokens;
        int minChunkTokens;

        // Use config values if specified, otherwise auto-calculate from context window
        if (_processingConfig.TargetChunkTokens > 0)
        {
            targetChunkTokens = _processingConfig.TargetChunkTokens;
            minChunkTokens = _processingConfig.MinChunkTokens > 0 
                ? _processingConfig.MinChunkTokens 
                : targetChunkTokens / 8;
        }
        else
        {
            // Auto-calculate: use ~25% of context window to leave room for prompt + response
            // Minimum 2000 tokens, maximum 16000 tokens per chunk
            targetChunkTokens = Math.Clamp(_cachedContextWindow.Value / 4, 2000, 16000);
            minChunkTokens = _processingConfig.MinChunkTokens > 0
                ? _processingConfig.MinChunkTokens
                : Math.Max(500, targetChunkTokens / 8);
        }

        if (_verbose)
        {
            Console.WriteLine($"Chunk sizing: target={targetChunkTokens}, min={minChunkTokens} tokens");
        }

        return new DocumentChunker(_processingConfig.MaxHeadingLevel, targetChunkTokens, minChunkTokens);
    }
}
