using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services.Onnx;

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
    private readonly IEmbeddingService _embedder;
    private readonly ProcessingConfig _processingConfig;
    private readonly SummaryLengthConfig _lengthConfig;
    private readonly ProgressService _progress;
    private readonly RagSummarizer _rag;
    private readonly bool _verbose;
    private int? _cachedContextWindow;
    private MapReduceSummarizer? _mapReduce;
    
    // BERT config for lazy initialization (model downloaded on first use)
    private readonly OnnxConfig _onnxConfig;
    private readonly BertConfig _bertConfig;

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
        OllamaConfig? ollamaConfig = null,
        OnnxConfig? onnxConfig = null,
        EmbeddingBackend embeddingBackend = EmbeddingBackend.Onnx,
        BertConfig? bertConfig = null)
    {
        _verbose = verbose;
        _progress = new ProgressService(verbose);
        _docling = new DoclingClient(doclingConfig ?? new DoclingConfig { BaseUrl = doclingUrl });

        _processingConfig = processingConfig ?? new ProcessingConfig();
        _lengthConfig = _processingConfig.SummaryLength ?? new SummaryLengthConfig();
        _maxLlmParallelism = _processingConfig.MaxLlmParallelism > 0
            ? _processingConfig.MaxLlmParallelism
            : MapReduceSummarizer.DefaultMaxParallelism;

        Template = template ?? SummaryTemplate.Presets.Default;

        // Use timeout from config if provided
        var ollamaTimeout = ollamaConfig != null
            ? TimeSpan.FromSeconds(ollamaConfig.TimeoutSeconds)
            : OllamaService.DefaultTimeout;
        
        // Get classifier model from config (defaults to tinyllama for fast classification)
        var classifierModel = ollamaConfig?.ClassifierModel;
        
        _ollama = new OllamaService(ollamaModel, timeout: ollamaTimeout, classifierModel: classifierModel);
        
        // Store ONNX and BERT config for lazy initialization
        _onnxConfig = onnxConfig ?? new OnnxConfig();
        _bertConfig = bertConfig ?? new BertConfig();
        
        // Create embedding service based on backend choice
        _embedder = embeddingBackend == EmbeddingBackend.Onnx
            ? new OnnxEmbeddingService(_onnxConfig, verbose)
            : new OllamaEmbeddingService(_ollama);
        
        _rag = new RagSummarizer(_ollama, _embedder, qdrantHost, verbose, _maxLlmParallelism, qdrantConfig, Template, null, _lengthConfig);
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
        var docId = NormalizeDocId(filePath);

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
            markdown = await SpectreProgressService.RunConversionWithProgressAsync(
                _docling,
                filePath,
                "Converting for benchmark");
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
        var docId = NormalizeDocId(filePath);


        try
        {
        if (_verbose)
            {
                PrintBanner();
                _progress.WriteDivider("Document Processing");
                _progress.Info($"Document: {docId}");
                _progress.Info($"Mode: {mode}");
                _progress.Info($"Timeout: {_ollama.Timeout.TotalMinutes:F0} minutes per LLM operation");
                if (!string.IsNullOrEmpty(focus)) _progress.Info($"Focus: {focus}");
                Console.WriteLine();
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
                // Use Spectre progress for conversion with live updates from DoclingClient
                Console.WriteLine("Converting document with Docling...");
                Console.Out.Flush();

                // If PDF/DOCX and split processing enabled, stream with pipelined BertRag
                var isPdfOrDocx = extension is ".pdf" or ".docx";
                var canPipeline = isPdfOrDocx && _processingConfig.EnableSplitProcessing;

                if (canPipeline && mode is SummarizationMode.BertRag or SummarizationMode.Auto)
                {
                    var pipelineResult = await SummarizeBertRagPipelinedAsync(filePath, docId, focus);
                    return (pipelineResult.summary, new List<DocumentChunk>(), pipelineResult.docId);
                }

                markdown = await SpectreProgressService.RunConversionWithProgressAsync(
                    _docling,
                    filePath,
                    $"Converting {docId}");

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


            var totalWords = CountWords(chunks);
            if (totalWords < _lengthConfig.MinWordsForSummary)
            {
                if (_verbose)
                {
                    _progress.Warning($"Document has {totalWords} words; below summary threshold {_lengthConfig.MinWordsForSummary}. Returning original text.");
                }

                var direct = BuildDirectSummary(docId, chunks, totalWords);
                return (direct, chunks, docId);
            }

            // For BERT modes, we need the markdown content
            // Re-read from temp file if we cached it, or reconstruct from chunks
            string? markdownForBert = null;
            if (mode is SummarizationMode.Bert or SummarizationMode.BertHybrid or SummarizationMode.BertRag or SummarizationMode.Auto)
            {
                if (tempMarkdownPath != null && File.Exists(tempMarkdownPath))
                {
                    markdownForBert = await File.ReadAllTextAsync(tempMarkdownPath);
                }
                else
                {
                    // Reconstruct from chunks (less ideal but works)
                    markdownForBert = string.Join("\n\n", chunks.Select(c => 
                        string.IsNullOrEmpty(c.Heading) ? c.Content : $"## {c.Heading}\n\n{c.Content}"));
                }
            }

            // Auto mode: select best mode based on document and available resources
            var effectiveMode = mode;
            if (mode == SummarizationMode.Auto)
            {
                var llmAvailable = await IsLlmAvailableAsync();
                effectiveMode = await AutoSelectModeAsync(chunks, focus, llmAvailable);
                if (_verbose) Console.WriteLine($"[Auto] Selected mode: {effectiveMode}");
            }

            DocumentSummary result = effectiveMode switch
            {
                SummarizationMode.MapReduce => await (await GetMapReduceSummarizerAsync()).SummarizeAsync(docId, chunks),
                SummarizationMode.Rag => await _rag.SummarizeAsync(docId, chunks, focus),
                SummarizationMode.Iterative => await SummarizeIterativeAsync(docId, chunks),
                SummarizationMode.Bert => await SummarizeBertAsync(markdownForBert!, chunks),
                SummarizationMode.BertHybrid => await SummarizeBertHybridAsync(markdownForBert!, chunks, docId),
                SummarizationMode.BertRag => await SummarizeBertRagAsync(markdownForBert!, docId, focus),
                SummarizationMode.Auto => throw new InvalidOperationException("Auto mode should have been resolved"),
                _ => throw new ArgumentException($"Unknown mode: {effectiveMode}")
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
        var docId = NormalizeDocId(filePath);

        try
        {
        if (_verbose)

                Console.WriteLine($"[{progress}%] Processing: {heading}");

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
        
        // Build chunk index before clearing
        var chunkIndex = orderedChunks.Select(ChunkIndexEntry.FromChunk).ToList();
        
        // Clear the ordered chunks copy to free memory
        orderedChunks.Clear();

        _progress.Success("Iterative summarization complete");

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunkCount, chunkCount, [], TimeSpan.Zero, 1.0, 0, chunkIndex));
    }

    /// <summary>
    /// Pure BERT extractive summarization - no LLM required.
    /// Uses ONNX embeddings to find the most important sentences.
    /// Model is downloaded on first use only.
    /// </summary>
    private async Task<DocumentSummary> SummarizeBertAsync(string markdown, List<DocumentChunk> chunks)
    {
        _progress.WriteDivider("BERT Extractive Summarization");
        if (_verbose) Console.WriteLine("[BERT] Using local ONNX model (no LLM required)");
        
        // Lazy-create BERT summarizer (downloads model on first use)
        using var bertSummarizer = new BertSummarizer(_onnxConfig, _bertConfig, _verbose);
        
        // Detect content type for position weighting
        var contentType = DetectContentType(chunks);
        if (_verbose) Console.WriteLine($"[BERT] Content type: {contentType}");
        
        var result = await bertSummarizer.SummarizeAsync(markdown, contentType);
        
        _progress.Success("BERT extraction complete");
        return result;
    }

    /// <summary>
    /// Hybrid mode: BERT extracts key sentences, LLM polishes into fluent prose.
    /// Best of both worlds - grounded extraction + fluent output.
    /// </summary>
    private async Task<DocumentSummary> SummarizeBertHybridAsync(string markdown, List<DocumentChunk> chunks, string docId)
    {
        _progress.WriteDivider("BERT Hybrid Summarization");
        if (_verbose) Console.WriteLine("[Hybrid] BERT extraction + LLM polishing");
        
        // Step 1: BERT extraction
        using var bertSummarizer = new BertSummarizer(_onnxConfig, _bertConfig, _verbose);
        var contentType = DetectContentType(chunks);
        
        if (_verbose) Console.WriteLine($"[Hybrid] Content type: {contentType}");
        if (_verbose) Console.WriteLine("[Hybrid] Step 1: Extracting key sentences with BERT...");
        
        var bertResult = await bertSummarizer.SummarizeAsync(markdown, contentType);
        
        // Step 2: LLM polish - rewrite extracted sentences into fluent prose
        if (_verbose) Console.WriteLine("[Hybrid] Step 2: Polishing with LLM...");
        
        var extractedText = bertResult.ExecutiveSummary;
        var topicTexts = bertResult.TopicSummaries
            .Select(t => $"## {t.Topic}\n{t.Summary}")
            .ToList();
        
        var polishPrompt = $"""
            You are given key sentences extracted from a document. 
            Rewrite them into a fluent, coherent summary.
            Preserve the meaning and key facts. Do not add new information.
            Keep citations like [s1], [s2] etc. in the output.
            
            EXTRACTED CONTENT:
            {extractedText}
            
            {(topicTexts.Count > 0 ? "TOPIC SECTIONS:\n" + string.Join("\n\n", topicTexts) : "")}
            
            FLUENT SUMMARY:
            """;
        
        var polishedSummary = await _ollama.GenerateAsync(polishPrompt, temperature: 0.3);
        
        // Build final result with polished summary but BERT's grounding
        var result = new DocumentSummary(
            polishedSummary,
            bertResult.TopicSummaries,
            bertResult.OpenQuestions,
            bertResult.Trace with { DocumentId = docId },
            bertResult.Entities);
        
        _progress.Success("Hybrid summarization complete");
        return result;
    }

    /// <summary>
    /// BERT→RAG pipeline: production-grade summarization.
    /// 
    /// Architecture:
    /// 1. Extract: Parse into segments with embeddings + salience scores
    /// 2. Retrieve: Dual-score ranking (query similarity + salience)
    /// 3. Synthesize: LLM generates fluent summary from retrieved segments
    /// 
    /// Properties:
    /// - LLM only at synthesis (no LLM-in-the-loop evaluation)
    /// - Deterministic extraction (reproducible, debuggable)
    /// - Perfect citations (every claim traceable to source segment)
    /// - Scales to any document size
    /// </summary>
    private async Task<(DocumentSummary summary, string docId)> SummarizeBertRagPipelinedAsync(
        string filePath,
        string docId,
        string? focusQuery)
    {
        _progress.WriteDivider("BERT→RAG Pipeline (Pipelined)");
        if (_verbose)
        {
            Console.WriteLine("[BertRag] Pipelined extraction: Docling chunks → streaming embeddings");
        }

        using var bertRag = new PipelinedBertRagSummarizer(
            _onnxConfig,
            _ollama,
            extractionConfig: new ExtractionConfig
            {
                MmrLambda = _bertConfig.Lambda,
                ExtractionRatio = _bertConfig.ExtractionRatio,
                MinSegments = _bertConfig.MinSentences,
                MaxSegments = _bertConfig.MaxSentences * 3,
                FallbackBucketSize = 10,
                IncludeCodeBlocks = true,
                IncludeListItems = true
            },
            retrievalConfig: new RetrievalConfig
            {
                Alpha = 0.6,
                TopK = 25,
                FallbackCount = 5,
                MinSimilarity = 0.3
            },
            template: Template,
            verbose: _verbose);

        // Wire chunk callback
        _docling.OnChunkComplete = (chunkIdx, startPage, endPage, markdown) =>
        {
            bertRag.OnChunkReady(docId, chunkIdx, markdown);
        };

        try
        {
            // Trigger conversion (will stream chunks)
            await SpectreProgressService.RunConversionWithProgressAsync(
                _docling,
                filePath,
                $"Converting {docId} (streaming)");
        }
        finally
        {
            _docling.OnChunkComplete = null;
        }

        var result = await bertRag.FinalizeAsync(docId, focusQuery);
        _progress.Success("BERT→RAG pipelined pipeline complete");
        return (result, docId);
    }

    private async Task<DocumentSummary> SummarizeBertRagAsync(string markdown, string docId, string? focusQuery)
    {
        _progress.WriteDivider("BERT→RAG Pipeline");
        if (_verbose)
        {
            Console.WriteLine("[BertRag] Production-grade summarization pipeline");
            Console.WriteLine("[BertRag] Phase 1: Extract → Phase 2: Retrieve → Phase 3: Synthesize");
        }
        
        // Detect content type for position weighting
        var contentType = DetectContentTypeFromMarkdown(markdown);
        if (_verbose) Console.WriteLine($"[BertRag] Content type: {contentType}");
        
        // Create and run the pipeline
        using var bertRag = new BertRagSummarizer(
            _onnxConfig,
            _ollama,
            extractionConfig: new ExtractionConfig
            {
                MmrLambda = _bertConfig.Lambda,
                ExtractionRatio = _bertConfig.ExtractionRatio,
                MinSegments = _bertConfig.MinSentences,
                MaxSegments = _bertConfig.MaxSentences * 3, // Allow more segments for RAG retrieval
                FallbackBucketSize = 10,
                IncludeCodeBlocks = true,
                IncludeListItems = true
            },
            retrievalConfig: new RetrievalConfig
            {
                Alpha = 0.6, // 60% query similarity, 40% salience
                TopK = 25,   // Retrieve more for synthesis
                FallbackCount = 5,
                MinSimilarity = 0.3
            },
            template: Template,
            verbose: _verbose);
        
        var result = await bertRag.SummarizeAsync(docId, markdown, focusQuery, contentType);
        
        _progress.Success("BERT→RAG pipeline complete");
        return result;
    }

    /// <summary>
    /// Detect content type from raw markdown (for cases where we don't have chunks yet)
    /// </summary>
    private ContentType DetectContentTypeFromMarkdown(string markdown)
    {
        var sampleText = markdown.Length > 3000 ? markdown[..3000] : markdown;
        var sampleLower = sampleText.ToLowerInvariant();
        
        // Fiction indicators
        var fictionScore = 0;
        if (sampleLower.Contains("said") || sampleLower.Contains("replied")) fictionScore += 2;
        if (sampleLower.Contains("chapter")) fictionScore += 3;
        if (System.Text.RegularExpressions.Regex.IsMatch(sampleLower, @"\b(he|she)\s+(walked|looked|felt|thought)\b")) fictionScore += 2;
        if (sampleLower.Contains("\"") && sampleLower.Split('"').Length > 4) fictionScore += 2;
        
        // Technical indicators
        var technicalScore = 0;
        if (sampleLower.Contains("function") || sampleLower.Contains("class") || sampleLower.Contains("method")) technicalScore += 2;
        if (sampleLower.Contains("```") || sampleLower.Contains("`")) technicalScore += 3;
        if (sampleLower.Contains("install") || sampleLower.Contains("configure")) technicalScore += 2;
        if (System.Text.RegularExpressions.Regex.IsMatch(sampleLower, @"\b(api|http|json|xml)\b")) technicalScore += 2;
        
        if (fictionScore > technicalScore + 2) return ContentType.Narrative;
        if (technicalScore > fictionScore + 2) return ContentType.Expository;
        return ContentType.Unknown;
    }

    /// <summary>
    /// Auto-select the best summarization mode based on document size and available resources.
    /// 
    /// Thresholds (approximate):
    /// - Tiny (&lt;500 words, ~1 page): Iterative - just LLM, no extraction overhead
    /// - Small (500-1500 words, 1-2 pages): Iterative - simple and effective
    /// - Medium (1500-8000 words, 2-15 pages): BertHybrid - BERT extract + LLM polish
    /// - Large (8000+ words, 15+ pages): BertRag - production pipeline with recall-safe pre-filtering
    /// 
    /// BertRag is also used when a focus query is provided (retrieval-optimized).
    /// </summary>
    private async Task<SummarizationMode> AutoSelectModeAsync(
        List<DocumentChunk> chunks, 
        string? focus,
        bool llmAvailable)
    {
        var totalWords = CountWords(chunks);
        var chunkCount = chunks.Count;
        
        if (_verbose) Console.WriteLine($"[Auto] Document: {totalWords:N0} words, {chunkCount} chunks");
        
        // If focus query provided, use BertRag (best for focused retrieval)
        if (!string.IsNullOrEmpty(focus))
        {
            if (_verbose) Console.WriteLine("[Auto] Focus query provided -> BertRag (query-optimized retrieval)");
            return SummarizationMode.BertRag;
        }
        
        // If no LLM available, use pure BERT
        if (!llmAvailable)
        {
            if (_verbose) Console.WriteLine("[Auto] No LLM available -> Bert (extractive only)");
            return SummarizationMode.Bert;
        }
        
        // Tiny documents (<500 words, ~1 page): Just use LLM directly
        // No need for extraction overhead - the whole doc fits in context
        if (totalWords < 500)
        {
            if (_verbose) Console.WriteLine("[Auto] Tiny document (<500 words) -> Iterative (no extraction needed)");
            return SummarizationMode.Iterative;
        }
        
        // Small documents (500-1500 words, 1-3 pages): Iterative
        // Simple and effective, LLM can see the whole document
        if (totalWords < 1500)
        {
            if (_verbose) Console.WriteLine("[Auto] Small document (500-1500 words) -> Iterative");
            return SummarizationMode.Iterative;
        }
        
        // Medium documents (1500-8000 words, 3-15 pages): BertHybrid
        // BERT extraction to find key sentences, LLM to polish
        if (totalWords < 8000)
        {
            if (_verbose) Console.WriteLine("[Auto] Medium document (1500-8000 words) -> BertHybrid");
            return SummarizationMode.BertHybrid;
        }
        
        // Large documents (8000+ words, 15+ pages): BertRag
        // Production pipeline with recall-safe pre-filtering for speed
        if (_verbose) Console.WriteLine("[Auto] Large document (8000+ words) -> BertRag (production pipeline)");
        return SummarizationMode.BertRag;
    }

    /// <summary>
    /// Check if Ollama LLM is available
    /// </summary>
    private async Task<bool> IsLlmAvailableAsync()
    {
        try
        {
            return await _ollama.IsAvailableAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect content type (fiction vs expository) for position weighting
    /// </summary>
    private ContentType DetectContentType(List<DocumentChunk> chunks)
    {
        // Use heuristics from first few chunks
        var sampleText = string.Join(" ", chunks.Take(3).Select(c => c.Content)).ToLowerInvariant();
        
        // Fiction indicators
        var fictionScore = 0;
        if (sampleText.Contains("said") || sampleText.Contains("replied")) fictionScore += 2;
        if (sampleText.Contains("chapter")) fictionScore += 3;
        if (System.Text.RegularExpressions.Regex.IsMatch(sampleText, @"\b(he|she)\s+(walked|looked|felt|thought)\b")) fictionScore += 2;
        if (sampleText.Contains("\"") && sampleText.Split('"').Length > 4) fictionScore += 2; // Dialogue
        
        // Technical indicators
        var technicalScore = 0;
        if (sampleText.Contains("function") || sampleText.Contains("class") || sampleText.Contains("method")) technicalScore += 2;
        if (sampleText.Contains("```") || sampleText.Contains("`")) technicalScore += 3;
        if (sampleText.Contains("install") || sampleText.Contains("configure")) technicalScore += 2;
        if (System.Text.RegularExpressions.Regex.IsMatch(sampleText, @"\b(api|http|json|xml)\b")) technicalScore += 2;
        
        if (fictionScore > technicalScore + 2) return ContentType.Narrative;
        if (technicalScore > fictionScore + 2) return ContentType.Expository;
        return ContentType.Unknown;
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

        // Template-aware tuning: book reports benefit from larger, fewer chunks
        var defaultProcessing = new ProcessingConfig();
        var isBookReport = Template.Name.Equals("bookreport", StringComparison.OrdinalIgnoreCase);
        var headingLevel = _processingConfig.MaxHeadingLevel;

        if (isBookReport)
        {
            var targetIsDefault = _processingConfig.TargetChunkTokens == defaultProcessing.TargetChunkTokens;
            var minIsDefault = _processingConfig.MinChunkTokens == defaultProcessing.MinChunkTokens;
            var headingIsDefault = _processingConfig.MaxHeadingLevel == defaultProcessing.MaxHeadingLevel;

            if (targetIsDefault)
            {
                // Use MUCH larger chunks for fiction to keep narrative flow
                // Target ~8-10 chunks for a full novel (~8000-10000 tokens per chunk)
                targetChunkTokens = Math.Max(targetChunkTokens, 10000);
            }

            if (minIsDefault)
            {
                // Ensure merged sections stay substantial for prose
                minChunkTokens = Math.Max(minChunkTokens, 2000);
            }

            if (headingIsDefault)
            {
                // Avoid over-splitting on subheadings for fiction/non-technical text
                headingLevel = 1;
            }
        }

        if (_verbose) Console.WriteLine($"Chunk sizing: target={targetChunkTokens}, min={minChunkTokens} tokens, headings≤{headingLevel}");

        return new DocumentChunker(headingLevel, targetChunkTokens, minChunkTokens);

    }

    private DocumentSummary BuildDirectSummary(string docId, List<DocumentChunk> chunks, int totalWords)
    {
        var fullText = string.Join("\n\n", chunks.Select(c => c.Content.Trim())).Trim();
        var maxPreviewLength = 2000;
        var preview = fullText.Length > maxPreviewLength
            ? fullText[..maxPreviewLength].TrimEnd() + "…"
            : fullText;

        var message = $"Document is {totalWords} words (< {_lengthConfig.MinWordsForSummary}); returning original text.";
        var executive = string.IsNullOrWhiteSpace(preview)
            ? message
            : $"{message}\n\n{preview}";

        var topicSummary = new TopicSummary(
            "Original Text",
            preview,
            chunks.Select(c => c.Id).ToList());

        var chunkIndex = chunks.Select(ChunkIndexEntry.FromChunk).ToList();
        var trace = new SummarizationTrace(
            docId,
            chunks.Count,
            chunks.Count,
            new List<string> { "Original Text" },
            TimeSpan.Zero,
            1.0,
            0,
            chunkIndex);

        return new DocumentSummary(
            executive,
            new List<TopicSummary> { topicSummary },
            new List<string>(),
            trace);
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var count = 0;
        var inWord = false;

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'' || ch == '’')
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
            {
                inWord = false;
            }
            else
            {
                inWord = false;
            }
        }

        return count;
    }

    private static int CountWords(IEnumerable<DocumentChunk> chunks)
    {
        var total = 0;
        foreach (var chunk in chunks)
        {
            total += CountWords(chunk.Content);
        }
        return total;
    }

    /// <summary>
    /// Print styled banner - now handled by SpectreProgressService
    /// </summary>
    private static string NormalizeDocId(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var sanitized = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return sanitized.Length > 40 ? sanitized[..40] : sanitized;
    }

    private static void PrintBanner()
    {
        // Banner now handled by SpectreProgressService.WriteHeader() in Program.cs
    }
}
