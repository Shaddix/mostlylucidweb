using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentSummarizer
{
    /// <summary>
    ///     Threshold for using temp file streaming (1MB)
    /// </summary>
    private const int LargeFileSizeThreshold = 1024 * 1024;

    private readonly DoclingClient _docling;
    private readonly int _maxLlmParallelism;
    private readonly OllamaService _ollama;
    private readonly ProcessingConfig _processingConfig;
    private readonly ProgressService _progress;
    private readonly RagSummarizer _rag;
    private readonly bool _verbose;
    private int? _cachedContextWindow;
    private MapReduceSummarizer? _mapReduce;

    /// <summary>
    ///     Temp directory for intermediate files
    /// </summary>
    private string? _tempDir;

    public DocumentSummarizer(
        string ollamaModel = "llama3.2:3b",
        string doclingUrl = "http://localhost:5001",
        string qdrantHost = "localhost",
        bool verbose = false,
        DoclingConfig? doclingConfig = null,
        ProcessingConfig? processingConfig = null,
        QdrantConfig? qdrantConfig = null,
        SummaryTemplate? template = null,
        OllamaConfig? ollamaConfig = null)
    {
        _verbose = verbose;
        _progress = new ProgressService(verbose);
        _docling = new DoclingClient(doclingConfig ?? new DoclingConfig { BaseUrl = doclingUrl });

        _processingConfig = processingConfig ?? new ProcessingConfig();
        _maxLlmParallelism = _processingConfig.MaxLlmParallelism > 0
            ? _processingConfig.MaxLlmParallelism
            : MapReduceSummarizer.DefaultMaxParallelism;

        Template = template ?? SummaryTemplate.Presets.Default;

        // Use timeout from config if provided
        var ollamaTimeout = ollamaConfig != null
            ? TimeSpan.FromSeconds(ollamaConfig.TimeoutSeconds)
            : OllamaService.DefaultTimeout;
        _ollama = new OllamaService(ollamaModel, timeout: ollamaTimeout);
        _rag = new RagSummarizer(_ollama, qdrantHost, verbose, _maxLlmParallelism, qdrantConfig, Template);
    }

    /// <summary>
    ///     Current template being used
    /// </summary>
    public SummaryTemplate Template { get; private set; }

    /// <summary>
    ///     Set the template for summarization
    /// </summary>
    public void SetTemplate(SummaryTemplate template)
    {
        Template = template;
        _rag.SetTemplate(template);
        _mapReduce?.SetTemplate(template);
    }

    /// <summary>
    ///     Get or create temp directory for intermediate files
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
    ///     Clean up temp directory
    /// </summary>
    private void CleanupTempDir()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, true);
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
    ///     Get or create the MapReduce summarizer with context-window-aware settings
    /// </summary>
    private async Task<MapReduceSummarizer> GetMapReduceSummarizerAsync()
    {
        if (_mapReduce == null)
        {
            var contextWindow = await GetContextWindowAsync();
            _mapReduce = new MapReduceSummarizer(_ollama, _verbose, _maxLlmParallelism, contextWindow, Template);
        }

        return _mapReduce;
    }

    /// <summary>
    ///     Get the model's context window (cached)
    /// </summary>
    private async Task<int> GetContextWindowAsync()
    {
        if (_cachedContextWindow == null)
        {
            _cachedContextWindow = await _ollama.GetContextWindowAsync();
            if (_verbose) Console.WriteLine($"Model context window: {_cachedContextWindow:N0} tokens");
        }

        return _cachedContextWindow.Value;
    }

    /// <summary>
    /// Summarize and return both the summary and the source chunks (for quality analysis)
    /// </summary>
    public async Task<(DocumentSummary summary, List<DocumentChunk> chunks)> SummarizeWithChunksAsync(
        string filePath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        var (summary, chunks, _) = await SummarizeInternalAsync(filePath, mode, focus);
        return (summary, chunks);
    }
    
    /// <summary>
    /// Summarize from pre-converted chunks (for benchmark mode - avoids re-running Docling)
    /// </summary>
    public async Task<DocumentSummary> SummarizeFromChunksAsync(
        string docId,
        List<DocumentChunk> chunks,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        if (_verbose)
        {
            Console.WriteLine($"[Benchmark] Using {chunks.Count} pre-converted chunks");
        }
        
        DocumentSummary result = mode switch
        {
            SummarizationMode.MapReduce => await (await GetMapReduceSummarizerAsync()).SummarizeAsync(docId, chunks),
            SummarizationMode.Rag => await _rag.SummarizeAsync(docId, chunks, focus),
            SummarizationMode.Iterative => await SummarizeIterativeAsync(docId, chunks),
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
        
        return result;
    }
    
    /// <summary>
    /// Convert a document to chunks without summarizing (for benchmark pre-processing)
    /// </summary>
    public async Task<List<DocumentChunk>> ConvertToChunksAsync(string filePath)
    {
        var docId = Path.GetFileName(filePath);
        
        // Check if it's a direct-read format (markdown or plain text)
        string markdown;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var isDirectRead = extension is ".md" or ".txt" or ".text";

        if (isDirectRead)
        {
            var formatName = extension == ".md" ? "markdown" : "text";
            Console.WriteLine($"Reading {formatName} file...");
            markdown = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            Console.WriteLine("Converting document with Docling (one-time for benchmark)...");
            Console.Out.Flush();
            markdown = await _docling.ConvertAsync(filePath);
            Console.WriteLine("Document converted to markdown");
        }

        // Chunk the document
        Console.WriteLine("Parsing document structure...");
        var chunker = await CreateChunkerAsync();
        var chunks = chunker.ChunkByStructure(markdown);
        Console.WriteLine($"Created {chunks.Count} chunks");
        
        return chunks;
    }
    
    public async Task<DocumentSummary> SummarizeAsync(
        string filePath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        var (summary, _, _) = await SummarizeInternalAsync(filePath, mode, focus);
        return summary;
    }
    
    private async Task<(DocumentSummary summary, List<DocumentChunk> chunks, string docId)> SummarizeInternalAsync(
        string filePath,
        SummarizationMode mode,
        string? focus)
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
                _progress.Info($"Timeout: {_ollama.Timeout.TotalMinutes:F0} minutes per LLM operation");
                if (!string.IsNullOrEmpty(focus)) _progress.Info($"Focus: {focus}");
                AnsiConsole.WriteLine();
            }

            // Check if it's a direct-read format (markdown or plain text)
            string markdown;
            string? tempMarkdownPath = null;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var isDirectRead = extension is ".md" or ".txt" or ".text";

            if (isDirectRead)
            {
                var formatName = extension == ".md" ? "markdown" : "text";
                Console.WriteLine($"Reading {formatName} file...");
                Console.Out.Flush();

                // Check file size - stream to temp if large
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > LargeFileSizeThreshold)
                {
                    if (_verbose)
                        Console.WriteLine($"[Memory] Large file ({fileInfo.Length / 1024:N0}KB), streaming...");
                    tempMarkdownPath = Path.Combine(GetTempDir(), "content.md");
                    File.Copy(filePath, tempMarkdownPath, true);
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
                Console.WriteLine("Converting document with Docling...");
                Console.Out.Flush();

                markdown = await _progress.WithStatusAsync(
                    $"Converting {docId} with Docling (timeout: 5 min)...",
                    async () => await _docling.ConvertAsync(filePath));

                Console.WriteLine("Document converted to markdown");

                // For large converted content, write to temp to allow GC of the string
                if (markdown.Length > LargeFileSizeThreshold)
                {
                    if (_verbose)
                        Console.WriteLine(
                            $"[Memory] Large content ({markdown.Length / 1024:N0}KB), caching to temp...");
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

            DocumentSummary result = mode switch
            {
                SummarizationMode.MapReduce => await (await GetMapReduceSummarizerAsync()).SummarizeAsync(docId,
                    chunks),
                SummarizationMode.Rag => await _rag.SummarizeAsync(docId, chunks, focus),
                SummarizationMode.Iterative => await SummarizeIterativeAsync(docId, chunks),
                _ => throw new ArgumentException($"Unknown mode: {mode}")
            };

            return (result, chunks, docId);
        }
        finally
        {
            // Clean up temp files
            CleanupTempDir();
        }
    }

    /// <summary>
    ///     Summarize a document with progress reporting for TUI mode
    /// </summary>
    public async Task<DocumentSummary> SummarizeWithProgressAsync(
        string filePath,
        SummarizationMode mode,
        string? focus,
        IProgressReporter progress)
    {
        var docId = Path.GetFileName(filePath);

        try
        {
            progress.ReportStage("Reading document...", 0.05f);

            // Check if it's a direct-read format (markdown or plain text)
            string markdown;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var isDirectRead = extension is ".md" or ".txt" or ".text";

            if (isDirectRead)
            {
                progress.ReportLog($"Reading {extension} file directly");
                markdown = await File.ReadAllTextAsync(filePath);
            }
            else
            {
                progress.ReportStage("Converting with Docling...", 0.1f);
                progress.ReportLlmActivity($"Docling: Converting {docId}");
                markdown = await _docling.ConvertAsync(filePath);
                progress.ReportLog("Document converted to markdown");
            }

            // Chunk the document
            progress.ReportStage("Parsing structure...", 0.2f);
            var chunker = await CreateChunkerAsync();
            var chunks = chunker.ChunkByStructure(markdown);
            progress.ReportLog($"Created {chunks.Count} chunks");
            progress.ReportChunkProgress(0, chunks.Count);

            // Release markdown string after chunking
            markdown = null!;

            DocumentSummary result;
            try
            {
                progress.ReportStage("Summarizing...", 0.3f);

                result = mode switch
                {
                    SummarizationMode.MapReduce => await SummarizeMapReduceWithProgressAsync(docId, chunks, progress),
                    SummarizationMode.Rag => await SummarizeRagWithProgressAsync(docId, chunks, focus, progress),
                    SummarizationMode.Iterative => await SummarizeIterativeWithProgressAsync(docId, chunks, progress),
                    _ => throw new ArgumentException($"Unknown mode: {mode}")
                };
            }
            finally
            {
                chunks.Clear();
            }

            progress.ReportStage("Complete", 1.0f);
            return result;
        }
        finally
        {
            CleanupTempDir();
        }
    }

    private async Task<DocumentSummary> SummarizeMapReduceWithProgressAsync(
        string docId,
        List<DocumentChunk> chunks,
        IProgressReporter progress)
    {
        var summarizer = await GetMapReduceSummarizerAsync();
        var total = chunks.Count;

        progress.ReportLlmActivity($"MapReduce: Processing {total} chunks");
        progress.ReportChunkProgress(0, total);

        // The summarizer runs in parallel internally - we just report overall progress
        var result = await summarizer.SummarizeAsync(docId, chunks);

        progress.ReportChunkProgress(total, total);
        progress.ReportLlmActivity("MapReduce: Complete");

        return result;
    }

    private async Task<DocumentSummary> SummarizeRagWithProgressAsync(
        string docId,
        List<DocumentChunk> chunks,
        string? focus,
        IProgressReporter progress)
    {
        progress.ReportLlmActivity("RAG: Indexing document...");
        progress.ReportStage("Indexing for RAG...", 0.4f);

        await _rag.IndexDocumentAsync(docId, chunks);

        progress.ReportLlmActivity("RAG: Generating summary...");
        progress.ReportStage("Generating RAG summary...", 0.6f);

        var result = await _rag.SummarizeAsync(docId, chunks, focus);

        return result;
    }

    private async Task<DocumentSummary> SummarizeIterativeWithProgressAsync(
        string docId,
        List<DocumentChunk> chunks,
        IProgressReporter progress)
    {
        var ollama = new OllamaService();
        var summary = "";
        var orderedChunks = chunks.OrderBy(c => c.Order).ToList();
        var total = orderedChunks.Count;

        for (var i = 0; i < orderedChunks.Count; i++)
        {
            var chunk = orderedChunks[i];
            progress.ReportChunkProgress(i, total);
            progress.ReportLlmActivity($"Iterative: Chunk {i + 1}/{total}");
            progress.ReportStage($"Processing chunk {i + 1}/{total}...", 0.3f + 0.6f * i / total);

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

        progress.ReportChunkProgress(total, total);

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunks.Count, chunks.Count, [], TimeSpan.Zero, 1.0, 0));
    }

    public async Task<string> QueryAsync(string filePath, string query)
    {
        var docId = Path.GetFileName(filePath);

        // Check if direct-read format (markdown or plain text)
        string markdown;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var isDirectRead = extension is ".md" or ".txt" or ".text";

        if (isDirectRead)
            markdown = await File.ReadAllTextAsync(filePath);
        else
            markdown = await _docling.ConvertAsync(filePath);

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

        // Use Progress display only if verbose AND not already in an interactive context
        if (_verbose && !ProgressService.IsInInteractiveContext)
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
                        task.Description =
                            $"[blue]Processing: {(chunk.Heading.Length > 30 ? chunk.Heading[..27] + "..." : chunk.Heading)}[/]";

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
        else
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

        _progress.Success("Iterative summarization complete");

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunks.Count, chunks.Count, [], TimeSpan.Zero, 1.0, 0));
    }

    /// <summary>
    ///     Create a chunker with context-window-aware sizing
    /// </summary>
    private async Task<DocumentChunker> CreateChunkerAsync()
    {
        // Get context window from model (cache it for subsequent calls)
        if (_cachedContextWindow == null)
        {
            _cachedContextWindow = await _ollama.GetContextWindowAsync();
            if (_verbose) Console.WriteLine($"Model context window: {_cachedContextWindow:N0} tokens");
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

        if (_verbose) Console.WriteLine($"Chunk sizing: target={targetChunkTokens}, min={minChunkTokens} tokens");

        return new DocumentChunker(_processingConfig.MaxHeadingLevel, targetChunkTokens, minChunkTokens);
    }
}