using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services.Onnx;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Pipelined BERT-RAG summarizer that processes Docling chunks as they arrive.
/// 
/// Unlike the standard BertRagSummarizer which waits for all content before extraction,
/// this version:
/// 1. Receives chunks incrementally via OnChunkReady callback
/// 2. Parses and pre-filters each chunk immediately
/// 3. Embeds candidates as they accumulate (in background)
/// 4. Runs MMR only once at the end when all chunks are done
/// 
/// This overlaps Docling conversion (I/O-bound) with embedding (CPU-bound),
/// significantly reducing total latency for large PDFs.
/// </summary>
public class PipelinedBertRagSummarizer : IDisposable
{
    private readonly OnnxEmbeddingService _embeddingService;
    private readonly OnnxEmbeddingService _queryEmbedder;
    private readonly MarkdigDocumentParser _parser;
    private readonly OllamaService _ollama;
    private readonly ExtractionConfig _extractionConfig;
    private readonly RetrievalConfig _retrievalConfig;
    private readonly bool _verbose;
    
    // Thread-safe accumulation of segments from incoming chunks
    private readonly ConcurrentBag<Segment> _accumulatedSegments = new();
    private readonly ConcurrentQueue<Segment> _pendingEmbedding = new();
    private readonly SemaphoreSlim _embeddingLock = new(1, 1);
    
    private int _segmentCounter = 0;
    private int _chunksReceived = 0;
    private ContentType _detectedContentType = ContentType.Unknown;
    private bool _embeddingTaskStarted = false;
    private Task? _backgroundEmbeddingTask;
    private CancellationTokenSource? _embeddingCts;
    
    private List<string> _lastChunkSignatures = new();
    private readonly Random _rand = new(1234); // stable but non-cryptographic
    
    // Embedding budget controls (tuned for higher coverage on small-chunk docs)
    private readonly int _globalEmbedBudget = 1200;
    private readonly int _bootstrapChunks = 4;
    private readonly double _bootstrapRate = 0.25;
    private readonly int _bootstrapCap = 180;
    private readonly int _chunkCap = 100;
    private readonly int _trickleEveryChunks = 3;
    private readonly int _trickleCount = 5;
    private readonly int _expectedChunks = 12;
    private readonly double _tailStartFraction = 0.6;
    private readonly int _tailReserve = 0;
    private int _embeddedBudgetUsed = 0;
    private bool _tailReleased = false;
    
    private static readonly Regex MultiSpace = new("\\s+", RegexOptions.Compiled);
    
    public SummaryTemplate Template { get; private set; }
    public int ChunksReceived => _chunksReceived;
    public int SegmentCount => _accumulatedSegments.Count;

    public PipelinedBertRagSummarizer(
        OnnxConfig onnxConfig,
        OllamaService ollama,
        ExtractionConfig? extractionConfig = null,
        RetrievalConfig? retrievalConfig = null,
        SummaryTemplate? template = null,
        bool verbose = false)
    {
        _embeddingService = new OnnxEmbeddingService(onnxConfig, verbose);
        _queryEmbedder = new OnnxEmbeddingService(onnxConfig, verbose);
        _parser = new MarkdigDocumentParser();
        _ollama = ollama;
        _extractionConfig = extractionConfig ?? new ExtractionConfig();
        _retrievalConfig = retrievalConfig ?? new RetrievalConfig();
        Template = template ?? SummaryTemplate.Presets.Default;
        _verbose = verbose;
    }

    /// <summary>
    /// Call this as each Docling chunk completes conversion.
    /// Parses the markdown into segments and queues them for embedding.
    /// </summary>
    public void OnChunkReady(string docId, int chunkIndex, string markdown)
    {
        _chunksReceived++;

        // Sanitize chunk text to strip gibberish encodings before parsing
        var cleanMarkdown = SanitizeText(markdown);
        
        // Parse this chunk into segments
        var segments = ParseChunkToSegments(docId, cleanMarkdown, chunkIndex);
        
        if (_verbose && segments.Count > 0)
            if (!ProgressService.IsInInteractiveContext)
                AnsiConsole.MarkupLine($"[dim]Chunk {chunkIndex}: parsed {segments.Count} segments[/]");
        
        // Guaranteed coverage: headings + first/last 2 sentences per chunk
        var guaranteed = new HashSet<Segment>();
        foreach (var h in segments.Where(s => s.Type == SegmentType.Heading)) guaranteed.Add(h);
        var orderedSentences = segments.Where(s => s.Type == SegmentType.Sentence).OrderBy(s => s.Index).ToList();
        foreach (var s in orderedSentences.Take(2)) guaranteed.Add(s);
        foreach (var s in orderedSentences.TakeLast(2)) guaranteed.Add(s);
        
        var remainder = segments.Except(guaranteed).ToList();
        var toEmbed = new List<Segment>();
        var remainingBudget = _globalEmbedBudget - _embeddedBudgetUsed - _pendingEmbedding.Count;
        if (remainingBudget < 0) remainingBudget = 0;
        
        var tailStart = (int)(_expectedChunks * _tailStartFraction);
        var inTail = _chunksReceived >= tailStart;
        if (inTail) _tailReleased = true;
        var reserved = _tailReleased ? 0 : _tailReserve;
        var effectiveBudget = Math.Max(0, remainingBudget - reserved);
        
        var isBootstrap = _chunksReceived < _bootstrapChunks;
        var budgetAvailable = effectiveBudget > 0;
        var shouldTrickle = !budgetAvailable && (_chunksReceived % _trickleEveryChunks == 0);
        
        if (budgetAvailable)
        {
            if (isBootstrap)
            {
                var sampleCount = Math.Min(_bootstrapCap, (int)Math.Ceiling(_bootstrapRate * remainder.Count));
                var sampled = remainder
                    .OrderBy(s => StableHash(s.ContentHash))
                    .Take(sampleCount)
                    .ToList();
                toEmbed = guaranteed.Concat(sampled).ToList();
            }
            else
            {
                // Centroid-guided scoring with per-chunk cap
                var centroid = GetCurrentCentroid();
                var scored = remainder
                    .Select(s => new { Segment = s, Score = ScoreSegment(s, centroid) })
                    .OrderByDescending(x => x.Score)
                    .Take(_chunkCap)
                    .Select(x => x.Segment)
                    .ToList();
                toEmbed = guaranteed.Concat(scored).ToList();
            }
            
            // Clip to effective budget
            if (toEmbed.Count > effectiveBudget)
                toEmbed = toEmbed.Take(effectiveBudget).ToList();
        }
        else if (shouldTrickle)
        {
            var centroid = GetCurrentCentroid();
            var trickle = remainder
                .Select(s => new { Segment = s, Score = ScoreSegment(s, centroid) })
                .OrderByDescending(x => x.Score)
                .Take(_trickleCount)
                .Select(x => x.Segment)
                .ToList();
            toEmbed = guaranteed.Concat(trickle).ToList();
        }
        
        // Add to accumulated and enqueue for embedding
        foreach (var segment in segments)
        {
            _accumulatedSegments.Add(segment);
        }
        foreach (var segment in toEmbed)
        {
            _pendingEmbedding.Enqueue(segment);
        }
        
        if (_verbose)
        {
            var pct = segments.Count == 0 ? 0 : (double)toEmbed.Count / segments.Count;
            if (!ProgressService.IsInInteractiveContext)
                AnsiConsole.MarkupLine($"[dim]Chunk {chunkIndex}: queued {toEmbed.Count}/{segments.Count} ({pct:P0}) for embedding, pending={_pendingEmbedding.Count}, embedded={_embeddedBudgetUsed}/{_globalEmbedBudget}[/]");
        }
        
        // Detect content type from first chunk
        if (_chunksReceived == 1 && segments.Count > 0)
        {
            _detectedContentType = SegmentExtractor.DetectContentTypeFromSegments(segments);
            if (_verbose && _detectedContentType != ContentType.Unknown)
                if (!ProgressService.IsInInteractiveContext)
                    AnsiConsole.MarkupLine($"[dim]Detected content type: {_detectedContentType}[/]");
        }
        
        // Start background embedding if not already started
        StartBackgroundEmbeddingIfNeeded();
    }

    /// <summary>
    /// Parse a single chunk's markdown into segments with unique IDs
    /// </summary>
    private List<Segment> ParseChunkToSegments(string docId, string markdown, int chunkIndex)
    {
        var parsedDoc = _parser.Parse(markdown);
        var segments = new List<Segment>();
        var headingPath = new Stack<string>();
        
        foreach (var section in parsedDoc.Sections)
        {
            // Update heading path
            while (headingPath.Count >= section.Level && headingPath.Count > 0)
                headingPath.Pop();
            if (!string.IsNullOrEmpty(section.Heading))
                headingPath.Push(section.Heading);
            
            var currentHeadingPath = string.Join(" > ", headingPath.Reverse());
            
            // Add heading
            // EXCEPTION: First H1 heading is ALWAYS included (it's the document title)
            var isDocumentTitle = section.Level == 1 && segments.Count == 0 && chunkIndex == 0;
            if (!string.IsNullOrEmpty(section.Heading) && 
                (section.Heading.Length >= _extractionConfig.MinSegmentLength || isDocumentTitle))
            {
                var idx = Interlocked.Increment(ref _segmentCounter);
                var segment = new Segment(docId, section.Heading, SegmentType.Heading, idx, 0, section.Heading.Length)
                {
                    SectionTitle = section.Heading,
                    HeadingPath = currentHeadingPath,
                    HeadingLevel = section.Level,
                    // Document title gets extra boost
                    PositionWeight = isDocumentTitle ? 2.0 : 1.1,
                    ChunkIndex = chunkIndex
                };
                segments.Add(segment);
            }
            
            // Add sentences
            foreach (var sentenceInfo in section.Sentences)
            {
                if (sentenceInfo.Text.Length < _extractionConfig.MinSegmentLength)
                    continue;
                
                var idx = Interlocked.Increment(ref _segmentCounter);
                var segment = new Segment(docId, sentenceInfo.Text, SegmentType.Sentence, idx, 0, sentenceInfo.Text.Length)
                {
                    SectionTitle = section.Heading,
                    HeadingPath = currentHeadingPath,
                    HeadingLevel = section.Level,
                    PositionWeight = sentenceInfo.PositionWeight,
                    ChunkIndex = chunkIndex
                };
                segments.Add(segment);
            }
            
            // Add list items
            if (_extractionConfig.IncludeListItems)
            {
                foreach (var item in section.ListItems)
                {
                    if (item.Length < _extractionConfig.MinSegmentLength)
                        continue;
                    
                    var idx = Interlocked.Increment(ref _segmentCounter);
                    var segment = new Segment(docId, item, SegmentType.ListItem, idx, 0, item.Length)
                    {
                        SectionTitle = section.Heading,
                        HeadingPath = currentHeadingPath,
                        HeadingLevel = section.Level,
                        PositionWeight = 1.05,
                        ChunkIndex = chunkIndex
                    };
                    segments.Add(segment);
                }
            }
            
            // Add code blocks
            if (_extractionConfig.IncludeCodeBlocks)
            {
                foreach (var codeBlock in section.CodeBlocks)
                {
                    if (codeBlock.Code.Length < 10)
                        continue;
                    
                    var codeText = $"[{codeBlock.Language}] {codeBlock.Code}";
                    if (codeText.Length > 500)
                        codeText = codeText[..500] + "...";
                    
                    var idx = Interlocked.Increment(ref _segmentCounter);
                    var segment = new Segment(docId, codeText, SegmentType.CodeBlock, idx, 0, codeText.Length)
                    {
                        SectionTitle = section.Heading,
                        HeadingPath = currentHeadingPath,
                        HeadingLevel = section.Level,
                        PositionWeight = 1.15,
                        ChunkIndex = chunkIndex
                    };
                    segments.Add(segment);
                }
            }
            
            // Add quotes
            foreach (var quote in section.Quotes)
            {
                if (quote.Length < _extractionConfig.MinSegmentLength)
                    continue;
                
                var idx = Interlocked.Increment(ref _segmentCounter);
                var segment = new Segment(docId, quote, SegmentType.Quote, idx, 0, quote.Length)
                {
                    SectionTitle = section.Heading,
                    HeadingPath = currentHeadingPath,
                    HeadingLevel = section.Level,
                    PositionWeight = 1.1,
                    ChunkIndex = chunkIndex
                };
                segments.Add(segment);
            }
        }
        
        return segments;
    }

    /// <summary>
    /// Start background embedding task if not already running
    /// </summary>
    private void StartBackgroundEmbeddingIfNeeded()
    {
        if (_embeddingTaskStarted) return;
        
        _embeddingLock.Wait();
        try
        {
            if (_embeddingTaskStarted) return;
            _embeddingTaskStarted = true;
            
            _embeddingCts = new CancellationTokenSource();
            _backgroundEmbeddingTask = Task.Run(() => BackgroundEmbeddingLoop(_embeddingCts.Token));
        }
        finally
        {
            _embeddingLock.Release();
        }
    }

    private float[] GetCurrentCentroid()
    {
        var embedded = _accumulatedSegments.Where(s => s.Embedding != null).ToList();
        if (embedded.Count == 0) return Array.Empty<float>();
        var dim = embedded[0].Embedding!.Length;
        var centroid = new float[dim];
        foreach (var s in embedded)
        {
            for (int i = 0; i < dim; i++) centroid[i] += s.Embedding![i];
        }
        for (int i = 0; i < dim; i++) centroid[i] /= embedded.Count;
        var norm = MathF.Sqrt(centroid.Sum(x => x * x));
        if (norm > 0)
            for (int i = 0; i < dim; i++) centroid[i] /= norm;
        return centroid;
    }

    private double ScoreSegment(Segment segment, float[] centroid)
    {
        double typeBoost = segment.Type switch
        {
            SegmentType.Heading => 1.2,
            SegmentType.CodeBlock => 1.1,
            SegmentType.ListItem => 1.05,
            _ => 1.0
        };
        var lenBoost = Math.Min(segment.Text.Length / 200.0, 1.0);
        var baseScore = segment.PositionWeight * typeBoost + 0.2 * lenBoost;
        if (centroid.Length == 0 || segment.Embedding == null) return baseScore;
        var sim = CosineSimilarity(centroid, segment.Embedding);
        return 0.7 * sim + 0.3 * baseScore;
    }

    private int StableHash(string input)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in input)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }

    /// <summary>
    /// Background loop that embeds segments as they arrive
    /// </summary>
    private async Task BackgroundEmbeddingLoop(CancellationToken ct)
    {
        await _embeddingService.InitializeAsync(ct);
        
        var batchSize = 32;
        var batch = new List<Segment>();
        
        while (!ct.IsCancellationRequested)
        {
            // Collect a batch
            batch.Clear();
            while (batch.Count < batchSize && _pendingEmbedding.TryDequeue(out var segment))
            {
                batch.Add(segment);
            }
            
            if (batch.Count > 0)
            {
                // Respect global budget (extra safety)
                var remaining = _globalEmbedBudget - _embeddedBudgetUsed;
                if (remaining <= 0)
                {
                    // drop pending if no budget
                    _pendingEmbedding.Clear();
                    break;
                }
                if (batch.Count > remaining)
                    batch = batch.Take(remaining).ToList();

                // Embed the batch
                var texts = batch.Select(s => s.Text).ToList();
                var embeddings = await _embeddingService.EmbedBatchAsync(texts, ct);
                
                for (int i = 0; i < batch.Count; i++)
                {
                    batch[i].Embedding = embeddings[i];
                }
                _embeddedBudgetUsed += batch.Count;
                
                if (_verbose && batch.Count > 0)
                    if (!ProgressService.IsInInteractiveContext)
                        AnsiConsole.MarkupLine($"[dim]Background embedded {batch.Count} segments (budget { _embeddedBudgetUsed}/{_globalEmbedBudget})[/]");
            }
            else
            {
                // No pending segments, wait a bit
                await Task.Delay(50, ct);
            }
        }
    }

    /// <summary>
    /// Finalize and generate summary after all chunks have been received.
    /// This waits for background embedding to complete, runs MMR, and synthesizes.
    /// </summary>
    public async Task<DocumentSummary> FinalizeAsync(
        string docId,
        string? focusQuery = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (_verbose)
            if (!ProgressService.IsInInteractiveContext)
                AnsiConsole.MarkupLine($"[bold cyan]Finalizing: {_accumulatedSegments.Count} segments from {_chunksReceived} chunks[/]");
        
        // Wait for pending embeddings to complete
        await WaitForPendingEmbeddingsAsync(ct);
        
        // Stop background embedding task
        _embeddingCts?.Cancel();
        if (_backgroundEmbeddingTask != null)
        {
            try { await _backgroundEmbeddingTask; } catch (OperationCanceledException) { }
        }
        
        // Use only embedded segments
        var segments = _accumulatedSegments
            .Where(s => s.Embedding != null)
            .OrderBy(s => s.Index)
            .ToList();
        
        if (segments.Count == 0)
        {
            return CreateEmptySummary(docId, "No segments embedded from document");
        }
        
        // Calculate centroid
        var centroid = CalculateCentroid(segments);
        
        // Compute salience scores with MMR
        if (_verbose && !ProgressService.IsInInteractiveContext) 
            AnsiConsole.MarkupLine("[dim]Computing salience scores with MMR...[/]");
        ComputeSalienceScores(segments, centroid, _detectedContentType);
        
        // Retrieve top segments
        var retrieved = await RetrieveAsync(segments, focusQuery, ct);
        
        if (_verbose)
            AnsiConsole.MarkupLine($"[dim]Retrieved {retrieved.Count} segments for synthesis[/]");
        
        // Synthesize
        if (_verbose) AnsiConsole.MarkupLine("[bold cyan]Synthesis phase[/]");
        var summary = await SynthesizeAsync(docId, retrieved, focusQuery, ct);
        
        stopwatch.Stop();
        
        // Build trace
        var trace = new SummarizationTrace(
            docId,
            segments.Count,
            retrieved.Count,
            segments.Where(s => s.Type == SegmentType.Heading).Select(s => s.Text).Take(10).ToList(),
            stopwatch.Elapsed,
            CoverageScore: (double)retrieved.Count / segments.Count,
            CitationRate: 1.0);
        
        return new DocumentSummary(
            summary.ExecutiveSummary,
            summary.TopicSummaries,
            summary.OpenQuestions,
            trace,
            summary.Entities);
    }

    /// <summary>
    /// Wait for all pending embeddings to complete
    /// </summary>
    private async Task WaitForPendingEmbeddingsAsync(CancellationToken ct)
    {
        var maxWait = TimeSpan.FromSeconds(30);
        var waited = TimeSpan.Zero;
        var pollInterval = TimeSpan.FromMilliseconds(100);
        
        while (!_pendingEmbedding.IsEmpty && waited < maxWait)
        {
            await Task.Delay(pollInterval, ct);
            waited += pollInterval;
        }
        
        if (!_pendingEmbedding.IsEmpty && _verbose)
            AnsiConsole.MarkupLine($"[yellow]Warning: {_pendingEmbedding.Count} segments still pending after {maxWait.TotalSeconds}s[/]");
    }

    /// <summary>
    /// Embed a list of segments
    /// </summary>
    private async Task EmbedSegmentsAsync(List<Segment> segments, CancellationToken ct)
    {
        await _embeddingService.InitializeAsync(ct);
        
        const int batchSize = 64;
        for (int i = 0; i < segments.Count; i += batchSize)
        {
            var batch = segments.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(s => s.Text).ToList();
            var embeddings = await _embeddingService.EmbedBatchAsync(texts, ct);
            
            for (int j = 0; j < batch.Count; j++)
            {
                batch[j].Embedding = embeddings[j];
            }
        }
    }

    /// <summary>
    /// Calculate document centroid
    /// </summary>
    private static float[] CalculateCentroid(List<Segment> segments)
    {
        var withEmbeddings = segments.Where(s => s.Embedding != null).ToList();
        if (withEmbeddings.Count == 0)
            return Array.Empty<float>();
        
        var dim = withEmbeddings[0].Embedding!.Length;
        var centroid = new float[dim];
        
        foreach (var segment in withEmbeddings)
        {
            for (int i = 0; i < dim; i++)
                centroid[i] += segment.Embedding![i];
        }
        
        for (int i = 0; i < dim; i++)
            centroid[i] /= withEmbeddings.Count;
        
        var norm = MathF.Sqrt(centroid.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < dim; i++)
                centroid[i] /= norm;
        }
        
        return centroid;
    }

    /// <summary>
    /// Compute salience scores using MMR
    /// </summary>
    private void ComputeSalienceScores(List<Segment> segments, float[] centroid, ContentType contentType)
    {
        if (centroid.Length == 0) return;
        
        var lambda = _extractionConfig.MmrLambda;
        var candidates = new HashSet<Segment>(segments.Where(s => s.Embedding != null));
        var ranked = new List<Segment>();
        
        // Pre-compute centroid similarities
        foreach (var segment in candidates)
        {
            var baseSim = CosineSimilarity(segment.Embedding!, centroid);
            segment.SalienceScore = baseSim * segment.PositionWeight;
        }
        
        // Greedy MMR
        while (candidates.Count > 0)
        {
            Segment? best = null;
            double bestScore = double.MinValue;
            
            foreach (var candidate in candidates)
            {
                var relevance = candidate.SalienceScore;
                
                double maxSimToRanked = 0;
                foreach (var rankedSeg in ranked)
                {
                    var sim = CosineSimilarity(candidate.Embedding!, rankedSeg.Embedding!);
                    maxSimToRanked = Math.Max(maxSimToRanked, sim);
                }
                
                var mmrScore = lambda * relevance - (1 - lambda) * maxSimToRanked;
                
                if (mmrScore > bestScore)
                {
                    bestScore = mmrScore;
                    best = candidate;
                }
            }
            
            if (best != null)
            {
                best.SalienceScore = 1.0 - ((double)ranked.Count / segments.Count);
                ranked.Add(best);
                candidates.Remove(best);
            }
            else break;
        }
    }

    /// <summary>
    /// Retrieve segments using hybrid search
    /// </summary>
    private async Task<List<Segment>> RetrieveAsync(
        List<Segment> segments,
        string? focusQuery,
        CancellationToken ct)
    {
        var topBySalience = segments
            .OrderByDescending(s => s.SalienceScore)
            .Take(_retrievalConfig.TopK * 2)
            .ToList();
        
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return topBySalience.Take(_retrievalConfig.TopK).ToList();
        }
        
        // Embed query
        await _queryEmbedder.InitializeAsync(ct);
        var queryEmbedding = await _queryEmbedder.EmbedAsync(focusQuery, ct);
        
        // Score by query similarity
        foreach (var segment in segments)
        {
            if (segment.Embedding == null) continue;
            segment.QuerySimilarity = CosineSimilarity(queryEmbedding, segment.Embedding);
        }
        
        // RRF fusion
        var byQuerySim = segments
            .Where(s => s.Embedding != null)
            .OrderByDescending(s => s.QuerySimilarity)
            .ToList();
        
        var bySalience = segments
            .Where(s => s.Embedding != null)
            .OrderByDescending(s => s.SalienceScore)
            .ToList();
        
        var rrfScores = new Dictionary<Segment, double>();
        var k = _retrievalConfig.RrfK;
        
        for (int i = 0; i < byQuerySim.Count; i++)
        {
            var segment = byQuerySim[i];
            rrfScores[segment] = 1.0 / (k + i + 1);
        }
        
        for (int i = 0; i < bySalience.Count; i++)
        {
            var segment = bySalience[i];
            if (rrfScores.ContainsKey(segment))
                rrfScores[segment] += 1.0 / (k + i + 1);
            else
                rrfScores[segment] = 1.0 / (k + i + 1);
        }
        
        var topByRetrieval = rrfScores
            .OrderByDescending(kv => kv.Value)
            .Take(_retrievalConfig.TopK)
            .Select(kv => kv.Key)
            .ToList();
        
        // Add fallback
        var fallback = topBySalience
            .Where(s => !topByRetrieval.Contains(s))
            .Take(_retrievalConfig.FallbackCount);
        
        return topByRetrieval.Concat(fallback)
            .OrderBy(s => s.Index)
            .ToList();
    }

    /// <summary>
    /// Synthesize summary from retrieved segments
    /// </summary>
    private async Task<DocumentSummary> SynthesizeAsync(
        string docId,
        List<Segment> retrieved,
        string? focusQuery,
        CancellationToken ct)
    {
        var bySection = retrieved
            .GroupBy(s => s.SectionTitle)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToList();
        
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("# Retrieved Segments\n");
        
        foreach (var segment in retrieved)
        {
            contextBuilder.AppendLine($"{segment.Citation} [{segment.Type}] {segment.Text}");
        }
        
        var coverage = SegmentCount == 0 ? 0 : (double)retrieved.Count / SegmentCount;
        var targetWords = Template.TargetWords > 0 ? Template.TargetWords : 300;
        var focusLine = string.IsNullOrEmpty(focusQuery) ? "" : $"FOCUS: {focusQuery}\n";
        
        var prompt = _detectedContentType == ContentType.Narrative
            ? BuildNarrativePrompt(targetWords, focusLine, contextBuilder.ToString(), coverage)
            : BuildExpositoryPrompt(targetWords, focusLine, contextBuilder.ToString(), coverage);
        
        var rawSummary = await _ollama.GenerateAsync(prompt, temperature: 0.3);
        var cleanedSummary = CleanResponse(rawSummary);
        cleanedSummary = ApplyCoverageGuard(cleanedSummary, coverage);
        cleanedSummary = AppendCoverageFooter(cleanedSummary, coverage);
        
        var topicSummaries = bySection.Take(10)
            .Select(section =>
            {
                var sectionSegments = section.OrderBy(s => s.Index).ToList();
                var annotation = BuildSectionAnnotation(section.Key!, sectionSegments);
                var body = string.Join(" ", sectionSegments.Select(s => $"{s.Text} {s.Citation}"));
                var annotated = string.IsNullOrEmpty(annotation) ? body : $"Annotation: {annotation}\n{body}";
                return new TopicSummary(section.Key!, annotated, sectionSegments.Select(s => s.Id).ToList());
            })
            .ToList();
        
        var entities = ExtractEntities(retrieved, _detectedContentType);
        
        return new DocumentSummary(
            cleanedSummary,
            topicSummaries,
            new List<string>(),
            new SummarizationTrace(docId, SegmentCount, retrieved.Count, new List<string>(), TimeSpan.Zero, coverage, 0),
            entities);
    }

    private static string BuildNarrativePrompt(int targetWords, string focusLine, string context, double coverage) => $"""
        You are writing a book report / plot summary. Your job is to describe WHAT HAPPENS, not transcribe dialogue.

        INSTRUCTIONS:
        1. Write at the SCENE level, not the dialogue level
        2. Describe ACTIONS: who did what, where, when
        3. Identify KEY CHARACTERS and their roles (detective, narrator, client, villain)
        4. Describe the SETTING (place, time period, atmosphere)
        5. Identify the INCITING INCIDENT (what starts the plot)
        6. Note any THEMES or central conflicts
        7. Use past tense, third person
        8. Write ~{targetWords} words in flowing prose

        IMPORTANT:
        - Do NOT include citation references like [s1], [s2] in your text - write clean prose
        - Do NOT quote dialogue verbatim (paraphrase instead)
        - Do NOT list every conversation ("He said X, she replied Y")
        - Do NOT treat all moments as equally important

        COVERAGE RULE:
        {(coverage < 0.05 ? "Coverage is very low (<5%). Use cautious language like 'in the sampled sections' and avoid definitive endings." : "Use confident tone consistent with evidence.")}

        {focusLine}
        {context}

        Write a book report that describes what happens:
        """;

    private static string BuildExpositoryPrompt(int targetWords, string focusLine, string context, double coverage) => $"""
        You are generating a factual executive summary from retrieved document segments.
        Your goals are: accuracy, clarity, non-redundancy, readable human prose.
        This is NOT a rewrite of source text. It is a synthesis.

        RULES:
        1. Do NOT mention segment IDs, citations, or list indices in the prose.
           Evidence tracking is handled separately - write clean readable text.
        2. Do NOT repeat the same idea more than once, even if it appears in multiple segments.
           If several segments say the same thing, summarize the idea once.
        3. Prefer the author's intent over literal phrasing.
           If the author corrects themselves, describe the correction clearly and briefly.
        4. Write like a technical blog summary, not an academic paper.
           Natural language. No parenthetical references. No hedging unless uncertainty is explicit.
        5. If the document is reflective or corrective, capture the arc:
           - what was originally claimed
           - why it was wrong or outdated  
           - what the author now believes or intends to do
        6. Avoid absolutist or textbook language unless the source explicitly uses it.
           Good: "the author realised compression doesn't remove encryption"
           Bad: "compression and encryption are exclusive processes"
        7. Assume the reader is technical but not reading the source.
           Be concise, but explanatory where it helps understanding.
        8. Write ~{targetWords} words in 3-6 short paragraphs or tight bullet points.
        9. {(coverage < 0.05 ? "Coverage is very low (<5%). Use cautious language and avoid definitive conclusions." : "Use confident tone consistent with evidence.")}

        ANTI-REDUNDANCY: Before writing, mentally group the retrieved segments by meaning.
        Each group should appear AT MOST ONCE in the summary.

        {focusLine}
        {context}

        Write a clean executive summary (no citations in text):
        """;

    private static string ApplyCoverageGuard(string summary, double coverage)
    {
        if (coverage >= 0.05) return summary;
        var banned = new[] { "ultimately", "as the story unfolds", "it becomes clear", "finally", "in the end" };
        var guarded = summary;
        foreach (var term in banned)
        {
            guarded = System.Text.RegularExpressions.Regex.Replace(
                guarded,
                System.Text.RegularExpressions.Regex.Escape(term),
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        var disclaimer = $"Based on sampled sections only (~{coverage:P1}), the excerpt suggests:";
        return $"{disclaimer}\n{guarded.Trim()}";
    }

    private static string AppendCoverageFooter(string summary, double coverage)
    {
        var scope = coverage < 0.05 ? "sampled scenes" : "document segments covered";
        var confidence = coverage < 0.05 ? "Low" : coverage < 0.15 ? "Medium" : "High";
        var footer = $"\n\nCoverage: {coverage:P1} ({scope})\nConfidence: {confidence}";
        return summary.TrimEnd() + footer;
    }

    private static string BuildSectionAnnotation(string sectionTitle, List<Segment> sectionSegments)
    {
        if (!sectionSegments.Any()) return string.Empty;
        var lead = sectionSegments.First();
        var who = string.IsNullOrEmpty(lead.SectionTitle) ? sectionTitle : lead.SectionTitle;
        var type = lead.Type.ToString().ToLowerInvariant();
        return $"Highlights {who} via {type}; establishes why this section matters.";
    }

    private static ExtractedEntities ExtractEntities(List<Segment> segments, ContentType contentType)
    {
        // Skip entity extraction for expository/technical content - produces noise (code tokens)
        if (contentType == ContentType.Expository)
            return ExtractedEntities.Empty;
        
        // For unknown content type, check if it looks like code-heavy content
        var text = string.Join(" ", segments.Select(s => s.Text));
        if (contentType == ContentType.Unknown && LooksLikeCodeContent(text))
            return ExtractedEntities.Empty;
            
        var tokens = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim(
                ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '—', '–'))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        
        var honorifics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mr.", "Mrs.", "Miss", "Ms.", "Dr.", "Captain", "Inspector", "Professor" };
        var placeStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Street", "St", "Wharf", "Yard", "Road", "Lane", "Court" };
        var dayStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        var monthStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var miscStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Baker", "Street", "Wharf" };
        
        // Code/technical keywords that should not be treated as character names
        var codeStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
        { 
            // Assembly/low-level
            "MOV", "MOVX", "CALL", "RET", "JMP", "JNZ", "JZ", "PUSH", "POP", "ADD", "SUB", "MUL", "DIV",
            // VB/Basic
            "Dim", "Sub", "Function", "End", "If", "Then", "Else", "For", "Next", "Do", "Loop", "While",
            // C-style
            "Int", "Void", "Char", "Float", "Double", "Bool", "String", "Null", "True", "False",
            // Common programming
            "API", "HTTP", "JSON", "XML", "HTML", "CSS", "URL", "URI", "SQL", "REST", "GET", "POST", "PUT", "DELETE",
            "CPU", "GPU", "RAM", "ROM", "BIOS", "FPGA", "ASIC", "VHDL", "HDL", "RTL",
            // General technical acronyms
            "ID", "IO", "UI", "UX", "OS", "VM", "SDK", "IDE", "CLI", "GUI"
        };
        
        var candidates = new List<string>();
        int i = 0;
        while (i < tokens.Count)
        {
            var tok = tokens[i];
            if (IsProper(tok, honorifics, codeStop))
            {
                var span = new List<string> { tok };
                var j = i + 1;
                while (j < tokens.Count && IsContinuation(tokens[j]))
                {
                    span.Add(tokens[j]);
                    j++;
                }
                var spanText = string.Join(" ", span);
                candidates.Add(spanText);
                i = j;
            }
            else
            {
                i++;
            }
        }
        
        var merged = candidates
            .Select(c => c.Trim())
            .Where(c => c.Length > 2)
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();
        
        var final = new List<string>();
        foreach (var name in merged)
        {
            if (dayStop.Contains(name) || monthStop.Contains(name)) continue;
            if (placeStop.Contains(name)) continue;
            if (honorifics.Contains(name)) continue;
            if (miscStop.Contains(name)) continue;
            if (codeStop.Contains(name)) continue;
            if (name.Equals("Street", StringComparison.OrdinalIgnoreCase) || name.Equals("Wharf", StringComparison.OrdinalIgnoreCase))
                continue;
            // Skip all-uppercase words (likely acronyms/code)
            if (name.Length <= 5 && name.All(char.IsUpper))
                continue;
            final.Add(name);
            if (final.Count >= 12) break;
        }
        
        var characters = contentType == ContentType.Narrative ? final : final;
        return new ExtractedEntities(characters, new List<string>(), new List<string>(), new List<string>(), new List<string>());
        
        bool IsProper(string token, HashSet<string> honors, HashSet<string> codeWords)
        {
            // Reject code keywords
            if (codeWords.Contains(token)) return false;
            // Reject all-caps short words (likely acronyms/code)
            if (token.Length <= 5 && token.All(char.IsUpper)) return false;
            // Accept honorifics or proper nouns (capital start, rest lowercase letters)
            return honors.Contains(token) || 
                   (token.Length > 1 && char.IsUpper(token[0]) && token.Skip(1).All(c => char.IsLower(c)));
        }
        
        bool IsContinuation(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            if (placeStop.Contains(token)) return true;
            if (honorifics.Contains(token)) return true;
            // Only continue with proper nouns (capital start, rest lowercase)
            if (token.Length > 1 && char.IsUpper(token[0]) && token.Skip(1).All(c => char.IsLower(c))) return true;
            return false;
        }
    }
    
    /// <summary>
    /// Check if text appears to be code-heavy content (FPGA, assembly, programming)
    /// </summary>
    private static bool LooksLikeCodeContent(string text)
    {
        var lower = text.ToLowerInvariant();
        var codeIndicators = new[] { "```", "function", "class", "void", "int ", "return", "if (", "for (", "while (", 
            "0x", "mov ", "call ", "push ", "pop ", "fpga", "vhdl", "register", "memory address" };
        var codeCount = codeIndicators.Count(ind => lower.Contains(ind));
        return codeCount >= 3;
    }

    private static string CleanResponse(string response)

    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleaned = new List<string>();
        var foundContent = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (!foundContent)
            {
                if (trimmed.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Here's", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Below is", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Based on", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("I'll", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Let me", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(trimmed))
                    continue;
                foundContent = true;
            }
            
            cleaned.Add(line);
        }
        
        return cleaned.Count == 0 ? response.Trim() : string.Join("\n", cleaned).Trim();
    }

    private static DocumentSummary CreateEmptySummary(string docId, string message) =>
        new(message, new List<TopicSummary>(), new List<string>(),
            new SummarizationTrace(docId, 0, 0, new List<string>(), TimeSpan.Zero, 0, 0),
            ExtractedEntities.Empty);

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? dot / denom : 0;
    }

    public void Dispose()
    {
        _embeddingCts?.Cancel();
        _backgroundEmbeddingTask?.Dispose();
        _embeddingLock.Dispose();
        _embeddingService.Dispose();
        _queryEmbedder.Dispose();
    }

    private static string SanitizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Normalize and strip combining marks
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark)
                continue;

            // Replace control/non-printable with space
            if (char.IsControl(c) && !char.IsWhiteSpace(c))
            {
                sb.Append(' ');
                continue;
            }

            // Replace obviously garbled high-code-point symbols (non letter/digit) with space
            if (c > 0xFF && !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c))
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        var cleaned = MultiSpace.Replace(sb.ToString(), " ").Trim();
        return cleaned;
}

}
