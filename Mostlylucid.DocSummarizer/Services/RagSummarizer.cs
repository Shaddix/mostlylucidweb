using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

public class RagSummarizer
{
    private const string CollectionPrefix = "docsummarizer_";

    /// <summary>
    ///     Default max parallelism for LLM calls. Ollama processes one request at a time per model,
    ///     so high values just queue requests. 8 is a good balance for throughput vs memory.
    /// </summary>
    public const int DefaultMaxParallelism = 8;
    
    /// <summary>
    /// Document type classification for adaptive prompts
    /// </summary>
    public enum DocumentType
    {
        Unknown,
        Fiction,        // Novels, short stories, creative writing
        Technical,      // Code docs, API refs, manuals, specs
        Academic,       // Research papers, theses, scholarly articles
        Business,       // Reports, proposals, memos, contracts
        Legal,          // Contracts, laws, regulations, legal briefs
        News,           // Articles, journalism, press releases
        Reference       // Encyclopedias, wikis, how-tos, tutorials
    }
    
    /// <summary>
    /// Document classification result with confidence
    /// </summary>
    private record DocumentClassification(
        DocumentType Type,
        double Confidence,
        string[] Indicators,
        string Method = "heuristic") // "heuristic" or "llm"
    {
        public bool IsHighConfidence => Confidence >= 0.7;
        public bool IsLowConfidence => Confidence < 0.5;
    }
    
    /// <summary>
    /// Classify document type using heuristics on early chunks
    /// No LLM call - purely statistical/pattern-based for speed
    /// </summary>
    private static DocumentClassification ClassifyDocument(List<DocumentChunk> chunks)
    {
        // Sample first ~5 chunks for classification (enough signal, fast)
        var sampleText = string.Join(" ", chunks.Take(5).Select(c => c.Content));
        var sampleLower = sampleText.ToLowerInvariant();
        var headings = chunks.Take(10).Select(c => c.Heading?.ToLowerInvariant() ?? "").ToList();
        var headingsText = string.Join(" ", headings);
        
        var scores = new Dictionary<DocumentType, (double score, List<string> indicators)>
        {
            [DocumentType.Fiction] = (0, []),
            [DocumentType.Technical] = (0, []),
            [DocumentType.Academic] = (0, []),
            [DocumentType.Business] = (0, []),
            [DocumentType.Legal] = (0, []),
            [DocumentType.News] = (0, []),
            [DocumentType.Reference] = (0, [])
        };
        
        // Fiction indicators
        var fictionPatterns = new (string pattern, double weight)[]
        {
            (@"\b(said|replied|whispered|shouted|asked)\b", 0.3),
            (@"\b(he|she|they)\s+(walked|ran|looked|felt|thought)\b", 0.25),
            (@"\b(chapter|prologue|epilogue)\s*\d*\b", 0.4),
            (@"[""'][^""']{20,}[""']", 0.2), // Dialogue
            (@"\b(mr\.|mrs\.|miss|lady|lord|sir)\s+[a-z]+\b", 0.25),
            (@"\b(smiled|frowned|sighed|laughed|cried)\b", 0.2),
            (@"\b(heart|soul|love|passion|desire)\b", 0.15),
        };
        
        // Technical indicators
        var technicalPatterns = new (string pattern, double weight)[]
        {
            (@"\b(function|class|method|api|interface|module)\b", 0.35),
            (@"\b(parameter|argument|return|async|await)\b", 0.3),
            (@"```|\bcode\b|`[^`]+`", 0.4),
            (@"\b(install|configure|setup|deploy|build)\b", 0.25),
            (@"\b(error|exception|debug|log|trace)\b", 0.2),
            (@"\b(version|v\d+\.\d+|release)\b", 0.2),
            (@"\b(http|https|url|endpoint|request|response)\b", 0.25),
            (@"\b(json|xml|yaml|config|schema)\b", 0.25),
        };
        
        // Academic indicators
        var academicPatterns = new (string pattern, double weight)[]
        {
            (@"\b(abstract|introduction|methodology|conclusion|references)\b", 0.4),
            (@"\b(hypothesis|findings|results|analysis|discussion)\b", 0.3),
            (@"\b(study|research|paper|journal|publication)\b", 0.25),
            (@"\([a-z]+,?\s*\d{4}\)", 0.35), // Citations like (Smith, 2020)
            (@"\b(et al\.?|ibid|op\.?\s*cit)\b", 0.4),
            (@"\b(figure|table|appendix)\s*\d+", 0.25),
            (@"\b(significant|correlation|variable|sample|data)\b", 0.2),
        };
        
        // Business indicators
        var businessPatterns = new (string pattern, double weight)[]
        {
            (@"\b(executive summary|overview|objectives|deliverables)\b", 0.35),
            (@"\b(revenue|profit|margin|budget|cost|roi)\b", 0.3),
            (@"\b(stakeholder|client|vendor|partner|customer)\b", 0.25),
            (@"\b(q[1-4]|fy\d{2,4}|fiscal|quarter)\b", 0.3),
            (@"\b(strategy|initiative|roadmap|milestone)\b", 0.25),
            (@"\b(kpi|metric|target|goal|benchmark)\b", 0.25),
        };
        
        // Legal indicators  
        var legalPatterns = new (string pattern, double weight)[]
        {
            (@"\b(whereas|hereby|herein|thereof|pursuant)\b", 0.4),
            (@"\b(party|parties|agreement|contract|clause)\b", 0.3),
            (@"\b(shall|must|may not|is prohibited)\b", 0.25),
            (@"\b(liability|indemnify|warranty|damages)\b", 0.3),
            (@"\b(section|article|paragraph)\s*\d+", 0.25),
            (@"\b(plaintiff|defendant|court|jurisdiction)\b", 0.35),
        };
        
        // News indicators
        var newsPatterns = new (string pattern, double weight)[]
        {
            (@"\b(reported|announced|according to|sources say)\b", 0.35),
            (@"\b(yesterday|today|monday|tuesday|wednesday|thursday|friday)\b", 0.2),
            (@"\b(officials?|spokesperson|minister|president|ceo)\b", 0.25),
            (@"\b(breaking|update|developing|exclusive)\b", 0.3),
            (@"\b(interview|statement|press|conference)\b", 0.2),
        };
        
        // Reference/Tutorial indicators
        var referencePatterns = new (string pattern, double weight)[]
        {
            (@"\b(step\s*\d+|first,?|next,?|then,?|finally)\b", 0.25),
            (@"\b(how to|guide|tutorial|learn|example)\b", 0.35),
            (@"\b(tip|note|warning|important|see also)\b", 0.25),
            (@"\b(definition|overview|summary|quick\s*start)\b", 0.25),
            (@"\b(faq|frequently asked|common questions)\b", 0.3),
        };
        
        // Score each type
        void ScorePatterns((string pattern, double weight)[] patterns, DocumentType type)
        {
            var (score, indicators) = scores[type];
            foreach (var (pattern, weight) in patterns)
            {
                var matches = Regex.Matches(sampleLower, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    score += weight * Math.Min(matches.Count, 3); // Cap at 3 matches per pattern
                    if (indicators.Count < 3)
                        indicators.Add($"{pattern.TrimStart('\\', 'b', '(').Split('|')[0]}×{matches.Count}");
                }
            }
            // Also check headings
            foreach (var (pattern, weight) in patterns)
            {
                if (Regex.IsMatch(headingsText, pattern, RegexOptions.IgnoreCase))
                    score += weight * 0.5;
            }
            scores[type] = (score, indicators);
        }
        
        ScorePatterns(fictionPatterns, DocumentType.Fiction);
        ScorePatterns(technicalPatterns, DocumentType.Technical);
        ScorePatterns(academicPatterns, DocumentType.Academic);
        ScorePatterns(businessPatterns, DocumentType.Business);
        ScorePatterns(legalPatterns, DocumentType.Legal);
        ScorePatterns(newsPatterns, DocumentType.News);
        ScorePatterns(referencePatterns, DocumentType.Reference);
        
        // Find winner
        var winner = scores.OrderByDescending(kv => kv.Value.score).First();
        var totalScore = scores.Values.Sum(v => v.score);
        var confidence = totalScore > 0 ? winner.Value.score / totalScore : 0;
        
        // If no clear winner or very low scores, return Unknown
        if (winner.Value.score < 0.5 || confidence < 0.3)
            return new DocumentClassification(DocumentType.Unknown, confidence, [], "heuristic");
        
        return new DocumentClassification(
            winner.Key, 
            Math.Min(confidence, 0.95), // Cap at 95% confidence
            winner.Value.indicators.ToArray(),
            "heuristic");
    }
    
    /// <summary>
    /// Fast LLM-based classification for when heuristics are uncertain
    /// Uses a minimal prompt to get quick classification from any model
    /// </summary>
    private async Task<DocumentClassification> ClassifyDocumentWithLlmAsync(List<DocumentChunk> chunks)
    {
        // Take a small sample - just first 500 chars from first 2 chunks
        var sample = string.Join("\n", chunks.Take(2).Select(c => 
            c.Content.Length > 250 ? c.Content[..250] : c.Content));
        
        var prompt = $"""
            Classify this text into ONE category. Reply with ONLY the category name.
            
            Categories: FICTION, TECHNICAL, ACADEMIC, BUSINESS, LEGAL, NEWS, REFERENCE
            
            Text sample:
            {sample}
            
            Category:
            """;
        
        try
        {
            var response = await _ollama.GenerateAsync(prompt);
            var cleaned = response.Trim().ToUpperInvariant().TrimEnd('.', ':', ' ');
            
            // Parse response
            var docType = cleaned switch
            {
                var s when s.Contains("FICTION") => DocumentType.Fiction,
                var s when s.Contains("TECHNICAL") => DocumentType.Technical,
                var s when s.Contains("ACADEMIC") => DocumentType.Academic,
                var s when s.Contains("BUSINESS") => DocumentType.Business,
                var s when s.Contains("LEGAL") => DocumentType.Legal,
                var s when s.Contains("NEWS") => DocumentType.News,
                var s when s.Contains("REFERENCE") || s.Contains("TUTORIAL") => DocumentType.Reference,
                _ => DocumentType.Unknown
            };
            
            return new DocumentClassification(
                docType,
                docType == DocumentType.Unknown ? 0.3 : 0.8,
                [cleaned],
                "llm");
        }
        catch
        {
            // If LLM fails, return unknown
            return new DocumentClassification(DocumentType.Unknown, 0, [], "llm-error");
        }
    }
    
    /// <summary>
    /// Hybrid classification: fast heuristics first, LLM fallback if uncertain
    /// </summary>
    private async Task<DocumentClassification> ClassifyDocumentHybridAsync(List<DocumentChunk> chunks, bool useLlmFallback = true)
    {
        // First try fast heuristics
        var heuristicResult = ClassifyDocument(chunks);
        
        if (_verbose)
        {
            Console.WriteLine($"[Classify] Heuristic: {heuristicResult.Type} ({heuristicResult.Confidence:P0}) " +
                $"[{string.Join(", ", heuristicResult.Indicators.Take(3))}]");
        }
        
        // If high confidence, use heuristic result
        if (heuristicResult.IsHighConfidence)
            return heuristicResult;
        
        // If low confidence and LLM fallback enabled, try LLM
        if (useLlmFallback && heuristicResult.IsLowConfidence)
        {
            if (_verbose) Console.WriteLine("[Classify] Low confidence, trying LLM...");
            
            var llmResult = await ClassifyDocumentWithLlmAsync(chunks);
            
            if (_verbose)
            {
                Console.WriteLine($"[Classify] LLM: {llmResult.Type} ({llmResult.Confidence:P0})");
            }
            
            // If LLM gives a clear answer, use it
            if (llmResult.Type != DocumentType.Unknown)
                return llmResult;
        }
        
        // Fall back to heuristic result (even if low confidence)
        return heuristicResult;
    }
    
    /// <summary>
    /// Adaptive parameters based on document size
    /// </summary>
    private record DocumentSizeProfile(
        int TopicCount,           // How many topics to extract
        int ChunksPerTopic,       // How many chunks to retrieve per topic
        int MaxCharacters,        // Entity cap for characters
        int MaxLocations,         // Entity cap for locations
        int MaxOther,             // Entity cap for orgs/events/dates
        int BulletCount,          // Executive summary bullets
        int WordsPerBullet,       // Max words per bullet
        int TopClaimsCount)       // Top claims to include in synthesis
    {
        /// <summary>
        /// Get appropriate profile based on chunk count
        /// </summary>
        public static DocumentSizeProfile ForChunks(int chunkCount) => chunkCount switch
        {
            // Tiny docs (< 5 pages / ~10 chunks): minimal processing
            < 10 => new(
                TopicCount: 2,
                ChunksPerTopic: 2,
                MaxCharacters: 4,
                MaxLocations: 3,
                MaxOther: 2,
                BulletCount: 2,
                WordsPerBullet: 25,
                TopClaimsCount: 5),
            
            // Small docs (5-20 pages / 10-40 chunks): light processing
            < 40 => new(
                TopicCount: 3,
                ChunksPerTopic: 3,
                MaxCharacters: 5,
                MaxLocations: 4,
                MaxOther: 3,
                BulletCount: 3,
                WordsPerBullet: 20,
                TopClaimsCount: 8),
            
            // Medium docs (20-100 pages / 40-200 chunks): standard processing
            < 200 => new(
                TopicCount: 4,
                ChunksPerTopic: 3,
                MaxCharacters: 6,
                MaxLocations: 4,
                MaxOther: 3,
                BulletCount: 3,
                WordsPerBullet: 20,
                TopClaimsCount: 10),
            
            // Large docs (100-500 pages / 200-1000 chunks): expanded processing
            < 1000 => new(
                TopicCount: 5,
                ChunksPerTopic: 4,
                MaxCharacters: 8,
                MaxLocations: 5,
                MaxOther: 4,
                BulletCount: 4,
                WordsPerBullet: 20,
                TopClaimsCount: 12),
            
            // Very large docs (500+ pages / 1000+ chunks): comprehensive but efficient
            _ => new(
                TopicCount: 6,
                ChunksPerTopic: 5,
                MaxCharacters: 10,
                MaxLocations: 6,
                MaxOther: 5,
                BulletCount: 5,
                WordsPerBullet: 18,
                TopClaimsCount: 15)
        };
        
        public string SizeCategory => TopicCount switch
        {
            <= 2 => "tiny",
            <= 3 => "small", 
            <= 4 => "medium",
            <= 5 => "large",
            _ => "very large"
        };
    }

    private readonly bool _deleteCollectionAfterSummarization;
    private readonly int _maxParallelism;
    private readonly OllamaService _ollama;
    private readonly IEmbeddingService _embedder;
    private readonly bool _useOnnxEmbedding;
    private readonly int _vectorSize;
    private readonly QdrantHttpClient _qdrant;
    private readonly bool _verbose;
    private readonly TextAnalysisService _textAnalysis;

    public RagSummarizer(
        OllamaService ollama,
        IEmbeddingService embedder,
        string qdrantHost = "localhost",
        bool verbose = false,
        int maxParallelism = DefaultMaxParallelism,
        QdrantConfig? qdrantConfig = null,
        SummaryTemplate? template = null,
        TextAnalysisService? textAnalysis = null)
    {
        _ollama = ollama;
        _embedder = embedder;
        _useOnnxEmbedding = embedder is not OllamaEmbeddingService;
        _textAnalysis = textAnalysis ?? new TextAnalysisService();

        // Use HTTP client instead of gRPC - gRPC has AOT compatibility issues with System.Single marshalling
        var port = qdrantConfig?.Port ?? 6333; // REST port
        var apiKey = qdrantConfig?.ApiKey;
        _qdrant = new QdrantHttpClient(qdrantHost, port, apiKey);

        _verbose = verbose;
        _maxParallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
        _deleteCollectionAfterSummarization = qdrantConfig?.DeleteCollectionAfterSummarization ?? true;
        _vectorSize = embedder.EmbeddingDimension;
        Template = template ?? SummaryTemplate.Presets.Default;
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
    }

    /// <summary>
    ///     Generate a unique collection name for a document to prevent collisions
    /// </summary>
    private static string GetCollectionName(string docId)
    {
        // Create a short hash of the docId to ensure unique collection per document
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(docId));
        var hash = Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        return $"{CollectionPrefix}{hash}";
    }

    /// <summary>
    ///     Delete the collection for a document
    /// </summary>
    private async Task DeleteCollectionAsync(string docId)
    {
        var collectionName = GetCollectionName(docId);
        try
        {
            await _qdrant.DeleteCollectionAsync(collectionName);
            if (_verbose) Console.WriteLine($"[Cleanup] Deleted collection {collectionName}");
        }
        catch (Exception ex)
        {
            if (_verbose) Console.WriteLine($"[Cleanup] Failed to delete collection {collectionName}: {ex.Message}");
        }
    }

    public async Task IndexDocumentAsync(string docId, List<DocumentChunk> chunks)
    {
        var collectionName = GetCollectionName(docId);
        await EnsureCollectionAsync(collectionName);

        // Adaptive batch size based on document size:
        // - Larger batches for big docs (amortize API overhead)
        // - Smaller batches for small docs (faster feedback)
        var batchSize = chunks.Count switch
        {
            < 20 => 5,
            < 100 => 10,
            < 500 => 20,
            _ => 50
        };
        var batch = new List<QdrantPoint>(batchSize);

        // Initialize embedder (downloads ONNX models on first use)
        await _embedder.InitializeAsync();
        
        var backendName = _useOnnxEmbedding ? "ONNX" : "Ollama";
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]Embedding {chunks.Count} chunks ({backendName})[/]", maxValue: chunks.Count);

                for (var i = 0; i < chunks.Count; i++)
                {
                    // Only add delays for Ollama - ONNX is local and fast
                    if (!_useOnnxEmbedding && i > 0)
                    {
                        var baseDelay = 500; // 500ms minimum between chunks
                        var jitter = Random.Shared.Next(0, 500); // 0-500ms jitter
                        await Task.Delay(baseDelay + jitter);
                    }

                    var chunk = chunks[i];
                    var embedding = await _embedder.EmbedAsync(chunk.Content);

                    // Validate embedding dimensions
                    if (embedding.Length != _vectorSize)
                        throw new InvalidOperationException(
                            $"Embedding dimension mismatch: expected {_vectorSize}, got {embedding.Length}. " +
                            $"Check your embedding model configuration.");

                    // Include order in ID to handle duplicate content (same hash)
                    var pointId = GenerateStableId(docId, chunk.Hash, chunk.Order);

                    // Store truncated content in payload to reduce memory pressure
                    var truncatedContent = chunk.Content.Length > 2000
                        ? chunk.Content[..2000]
                        : chunk.Content;

                    batch.Add(new QdrantPoint
                    {
                        Id = pointId.ToString(),
                        Vector = embedding,
                        Payload = new Dictionary<string, object>
                        {
                            ["docId"] = docId,
                            ["chunkId"] = chunk.Id,
                            ["heading"] = chunk.Heading ?? "",
                            ["headingLevel"] = chunk.HeadingLevel,
                            ["order"] = chunk.Order,
                            ["content"] = truncatedContent,
                            ["hash"] = chunk.Hash
                        }
                    });

                    // Upsert batch when full to free memory
                    if (batch.Count >= batchSize)
                    {
                        await _qdrant.UpsertAsync(collectionName, batch);
                        batch.Clear();
                    }

                    task.Increment(1);
                    
                    if (_verbose)
                        AnsiConsole.MarkupLine($"  [grey]Embedded {Markup.Escape($"[{chunk.Id}]")} {Markup.Escape(chunk.Heading ?? "")}[/]");
                }

                // Upsert remaining batch
                if (batch.Count > 0)
                {
                    await _qdrant.UpsertAsync(collectionName, batch);
                    batch.Clear();
                }
            });
    }

    public async Task<DocumentSummary> SummarizeAsync(
        string docId,
        List<DocumentChunk> chunks,
        string? focusQuery = null)
    {
        var sw = Stopwatch.StartNew();
        var collectionName = GetCollectionName(docId);
        
        // Get adaptive parameters based on document size
        var profile = DocumentSizeProfile.ForChunks(chunks.Count);
        if (_verbose) Console.WriteLine($"[Profile] {profile.SizeCategory} document ({chunks.Count} chunks) → {profile.TopicCount} topics, {profile.ChunksPerTopic} chunks/topic");
        
        // Classify document type for adaptive prompts (fast heuristic, LLM fallback if uncertain)
        var docType = await ClassifyDocumentHybridAsync(chunks, useLlmFallback: !_useOnnxEmbedding);
        if (_verbose) Console.WriteLine($"[DocType] {docType.Type} ({docType.Confidence:P0}, {docType.Method})");

        try
        {
            // Build TF-IDF index from all chunks to identify distinctive vs common terms
            // This helps us classify claims as fact/inference/colour later
            if (_verbose) Console.WriteLine("[TF-IDF] Building term frequency index...");
            _textAnalysis.BuildTfIdfIndex(chunks.Select(c => c.Content));
            
            // Index first
            await IndexDocumentAsync(docId, chunks);

            // Extract topics - constrain to actual headings where possible
            var headings = chunks.Select(c => c.Heading).Where(h => !string.IsNullOrEmpty(h)).ToList();
            var topics = await ExtractTopicsAsync(headings, profile.TopicCount);

            if (_verbose) Console.WriteLine($"[Topics] Extracted {topics.Count} topics");

            // Retrieve and summarize per topic - run in parallel for speed
            var allRetrievedChunks = new HashSet<string>();
            var allEntities = new List<ExtractedEntities>();
            var claimLedger = new ClaimLedger();

            // First, retrieve chunks for all topics in parallel (embeddings are fast)
            var retrievalTasks = topics.Select(async topic =>
            {
                var query = focusQuery != null ? $"{topic} {focusQuery}" : topic;
                var retrieved = await RetrieveChunksAsync(collectionName, query, profile.ChunksPerTopic);
                return (topic, retrieved);
            }).ToList();

            var retrievalResults = await Task.WhenAll(retrievalTasks);

            // Now synthesize topics in parallel (LLM calls - this is the slow part)
            var synthesizeTasks = retrievalResults.Select(async r =>
            {
                var (topic, retrieved) = r;
                var (summary, entities, claims) = await SynthesizeTopicWithClaimsAsync(topic, retrieved, focusQuery, docType.Type, chunks.Count);

                if (_verbose) Console.WriteLine($"  [{topic}] Retrieved {retrieved.Count} chunks, {claims.Count} claims");

                return (topic, summary, entities, claims, chunkIds: retrieved.Select(c => c.chunkId).ToList());
            }).ToList();

            var synthesisResults = await Task.WhenAll(synthesizeTasks);

            // Build results maintaining topic order, with post-processing to clean summaries
            var topicSummaries = synthesisResults
                .Select(r => new TopicSummary(
                    CleanTopicName(r.topic), 
                    CleanTopicSummary(r.summary), 
                    r.chunkIds))
                .Where(t => !string.IsNullOrWhiteSpace(t.Summary) && t.Summary.Length > 10)
                .ToList();

            foreach (var result in synthesisResults)
            {
                if (result.entities != null)
                    allEntities.Add(result.entities);
                
                // Add claims to ledger for weighted synthesis
                claimLedger.AddRange(result.claims);
            }

            foreach (var result in retrievalResults)
            foreach (var c in result.retrieved)
                allRetrievedChunks.Add(c.chunkId);

            // Clear retrieval results to free memory - we've extracted what we need
            retrievalResults = null!;
            synthesisResults = null!;
            
            // Deduplicate claims using semantic similarity
            var deduplicatedClaims = _textAnalysis.DeduplicateClaims(claimLedger.Claims.ToList());
            if (_verbose) Console.WriteLine($"[Claims] {claimLedger.Claims.Count} claims → {deduplicatedClaims.Count} after dedup");
            
            // Merge all extracted entities with fuzzy deduplication BEFORE executive summary
            // so we can use them for grounding
            var mergedEntities = allEntities.Count > 0 
                ? NormalizeAndMergeEntities(allEntities) 
                : null;
            
            if (_verbose && mergedEntities != null)
            {
                Console.WriteLine($"[Entities] Extracted: {mergedEntities.Characters.Count} characters, " +
                    $"{mergedEntities.Locations.Count} locations, {mergedEntities.Events.Count} events");
            }
            
            // Create executive summary using weighted claims AND entities for grounding
            var executive = await CreateGroundedExecutiveSummaryAsync(
                topicSummaries, deduplicatedClaims, mergedEntities, focusQuery, profile);

            // Clear intermediate data to free memory before building result
            deduplicatedClaims.Clear();
            allEntities.Clear();
            
            sw.Stop();

            // Coverage = % of top-level headings that appear in at least one retrieved chunk
            var topLevelHeadings = chunks
                .Where(c => c.HeadingLevel <= 2 && !string.IsNullOrEmpty(c.Heading))
                .Select(c => c.Heading)
                .ToList();
            var retrievedHeadings = chunks
                .Where(c => allRetrievedChunks.Contains(c.Id))
                .Select(c => c.Heading)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var coverage = topLevelHeadings.Count > 0
                ? (double)topLevelHeadings.Count(h => retrievedHeadings.Contains(h)) / topLevelHeadings.Count
                : 1.0;
            var citationRate = CalculateCitationRate(executive);
            
            // Build chunk index for output - cap for very large docs to save memory
            var chunkIndex = chunks.Count > 500 
                ? chunks.Take(100).Concat(chunks.Skip(chunks.Count - 100)).Select(ChunkIndexEntry.FromChunk).ToList()
                : chunks.Select(ChunkIndexEntry.FromChunk).ToList();

            // Apply entity caps from profile to final output
            var cappedEntities = mergedEntities != null 
                ? CapEntities(mergedEntities, profile)
                : null;

            return new DocumentSummary(
                executive,
                topicSummaries,
                [],
                new SummarizationTrace(
                    docId, chunks.Count, allRetrievedChunks.Count,
                    topics, sw.Elapsed, coverage, citationRate, chunkIndex),
                cappedEntities);
        }
        finally
        {
            // Clean up collection after summarization (unless configured to keep it)
            if (_deleteCollectionAfterSummarization) await DeleteCollectionAsync(docId);
        }
    }
    
    /// <summary>
    /// Apply entity caps from profile to prevent bloated output
    /// </summary>
    private static ExtractedEntities CapEntities(ExtractedEntities entities, DocumentSizeProfile profile)
    {
        return new ExtractedEntities(
            entities.Characters.Take(profile.MaxCharacters).ToList(),
            entities.Locations.Take(profile.MaxLocations).ToList(),
            entities.Dates.Take(profile.MaxOther).ToList(),
            entities.Events.Take(profile.MaxOther).ToList(),
            entities.Organizations.Take(profile.MaxOther).ToList());
    }

    private async Task<List<string>> ExtractTopicsAsync(List<string> headings, int maxTopics = 5)
    {
        // For small models, limit headings to avoid overwhelming context
        var limitedHeadings = headings.Take(15).ToList();

        var prompt = $"""
                      Extract {maxTopics} main THEMES (not chapter titles) from these headings:
                      {string.Join(", ", limitedHeadings)}

                      Rules:
                      - Output ONE theme per line
                      - Each theme max 6 words
                      - No bullets, numbers, or explanations
                      - Focus on abstract themes, not plot events
                      
                      Example good themes: "Social class and marriage", "Family duty vs personal desire"
                      Example bad themes: "Chapter 1 Introduction", "The events at Longbourn"
                      """;

        var response = await _ollama.GenerateAsync(prompt);
        return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '))
            .Where(t => t.Length > 2 && t.Length < 60 && !t.StartsWith("Example", StringComparison.OrdinalIgnoreCase))
            .Take(maxTopics)
            .ToList();
    }

    private async Task<List<(string chunkId, string heading, string content, int order)>> RetrieveChunksAsync(
        string collectionName, string query, int topK)
    {
        var queryEmbedding = await _embedder.EmbedAsync(query);

        // No filter needed since each document has its own collection
        var results = await _qdrant.SearchAsync(collectionName, queryEmbedding, topK);

        return results.Select(r =>
        {
            var payload = r.GetPayloadStrings();
            var orderStr = payload.GetValueOrDefault("order", "0");
            int.TryParse(orderStr, out var order);
            return (
                payload.GetValueOrDefault("chunkId", ""),
                payload.GetValueOrDefault("heading", ""),
                payload.GetValueOrDefault("content", ""),
                order
            );
        }).ToList();
    }
    
    /// <summary>
    /// Convert chunk order to estimated page reference for citations
    /// Uses ~2 chunks per page as rough estimate for typical documents
    /// </summary>
    private static string GetPageCitation(int chunkOrder, int totalChunks)
    {
        // Estimate: ~2 chunks per page for typical documents
        // Adjust based on total chunks to get reasonable page numbers
        var estimatedPage = (chunkOrder / 2) + 1;
        return $"[p.{estimatedPage}]";
    }
    
    /// <summary>
    /// Build context with page-based citations instead of chunk IDs
    /// </summary>
    private static string BuildContextWithPageCitations(
        List<(string chunkId, string heading, string content, int order)> chunks,
        int maxContentPerChunk,
        int totalChunks)
    {
        return string.Join("\n", chunks.Select(c =>
        {
            var truncated = c.content.Length > maxContentPerChunk
                ? c.content[..maxContentPerChunk] + "..."
                : c.content;
            var pageCite = GetPageCitation(c.order, totalChunks);
            return $"{pageCite}: {truncated}";
        }));
    }

    private async Task<string> SynthesizeTopicAsync(
        string topic,
        List<(string chunkId, string heading, string content, int order)> chunks,
        string? focus,
        DocumentType docType = DocumentType.Unknown,
        int totalChunks = 100)
    {
        var (summary, _) = await SynthesizeTopicWithEntitiesAsync(topic, chunks, focus, docType, totalChunks);
        return summary;
    }
    
    private async Task<(string summary, ExtractedEntities? entities)> SynthesizeTopicWithEntitiesAsync(
        string topic,
        List<(string chunkId, string heading, string content, int order)> chunks,
        string? focus,
        DocumentType docType = DocumentType.Unknown,
        int totalChunks = 100)
    {
        // Adaptive content truncation based on chunk count
        var maxContentPerChunk = chunks.Count switch
        {
            <= 2 => 800,
            <= 4 => 500,
            _ => 350
        };
        
        // Build context with page-based citations instead of chunk IDs
        var context = BuildContextWithPageCitations(chunks, maxContentPerChunk, totalChunks);

        // Get document-type-specific prompt
        var prompt = GetTopicSynthesisPrompt(topic, context, focus, docType);
        var response = await _ollama.GenerateAsync(prompt);
        var (summary, entities) = ParseSummaryAndEntities(response);
        
        return (summary, entities);
    }
    
    private async Task<(string summary, ExtractedEntities? entities)> SynthesizeTopicWithEntitiesAsync(
        string topic,
        List<(string chunkId, string heading, string content)> chunks,
        string? focus,
        DocumentType docType = DocumentType.Unknown)
    {
        // Adaptive content truncation based on chunk count
        var maxContentPerChunk = chunks.Count switch
        {
            <= 2 => 800,
            <= 4 => 500,
            _ => 350
        };
        
        var context = string.Join("\n", chunks.Select(c =>
        {
            var truncated = c.content.Length > maxContentPerChunk
                ? c.content[..maxContentPerChunk] + "..."
                : c.content;
            return $"[{c.chunkId}]: {truncated}";
        }));

        // Get document-type-specific prompt
        var prompt = GetTopicSynthesisPrompt(topic, context, focus, docType);
        var response = await _ollama.GenerateAsync(prompt);
        var (summary, entities) = ParseSummaryAndEntities(response);
        
        return (summary, entities);
    }
    
    /// <summary>
    /// Generate tight, document-type-specific prompts for topic synthesis
    /// Uses page citations [p.N] instead of chunk citations
    /// </summary>
    private static string GetTopicSynthesisPrompt(string topic, string context, string? focus, DocumentType docType)
    {
        var focusLine = focus != null ? $"Focus: {focus}\n" : "";
        
        // Base entity extraction (same for all types)
        const string entityBlock = """
            ENTITIES:
            Characters: [names or "none"]
            Locations: [places or "none"]  
            Dates: [dates or "none"]
            Events: [events or "none"]
            Organizations: [orgs or "none"]
            """;
        
        return docType switch
        {
            DocumentType.Fiction => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [One sentence insight about theme/meaning, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Insight not plot. No "the story shows". No author name. Use exact [p.N] page cite from context.
                """,
            
            DocumentType.Technical => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [What this does/solves, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Function over description. No "this section explains". Use exact [p.N] page cite.
                """,
            
            DocumentType.Academic => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [Key finding or argument, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Claim not method. No "the study shows". Use exact [p.N] page cite.
                """,
            
            DocumentType.Business => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [Business implication or action, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Impact not description. No "the report indicates". Use exact [p.N] page cite.
                """,
            
            DocumentType.Legal => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [Legal effect or requirement, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Obligation not description. No "the document states". Use exact [p.N] page cite.
                """,
            
            DocumentType.News => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [What happened and why it matters, max 18 words] [p.N]
                {entityBlock}
                
                Rules: News value not narrative. No "according to". Use exact [p.N] page cite.
                """,
            
            DocumentType.Reference => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [Key concept or how-to, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Actionable info. No "this guide explains". Use exact [p.N] page cite.
                """,
            
            // Unknown/default - generic but tight
            _ => $"""
                [{topic}]
                {context}
                {focusLine}
                SUMMARY: [One insight, max 18 words] [p.N]
                {entityBlock}
                
                Rules: Insight not restatement. No filler phrases. Use exact [p.N] page cite from context.
                """
        };
    }
    
    private static (string summary, ExtractedEntities? entities) ParseSummaryAndEntities(string response)
    {
        var summary = response;
        ExtractedEntities? entities = null;
        
        // Try to extract structured entities from response
        var summaryMatch = Regex.Match(response, @"SUMMARY:\s*(.+?)(?=ENTITIES:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var entitiesMatch = Regex.Match(response, @"ENTITIES:\s*(.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        if (summaryMatch.Success)
        {
            summary = summaryMatch.Groups[1].Value.Trim();
        }
        
        if (entitiesMatch.Success)
        {
            var entitiesText = entitiesMatch.Groups[1].Value;
            entities = ParseEntitiesBlock(entitiesText);
        }
        
        return (summary, entities);
    }
    
    private static ExtractedEntities ParseEntitiesBlock(string entitiesText)
    {
        var characters = ExtractEntityList(entitiesText, "Characters");
        var locations = ExtractEntityList(entitiesText, "Locations");
        var dates = ExtractEntityList(entitiesText, "Dates");
        var events = ExtractEntityList(entitiesText, "Events");
        var organizations = ExtractEntityList(entitiesText, "Organizations");
        
        return new ExtractedEntities(characters, locations, dates, events, organizations);
    }
    
    private static List<string> ExtractEntityList(string text, string entityType)
    {
        // Try multiple patterns - LLMs format these inconsistently
        var patterns = new[]
        {
            $@"{entityType}:\s*\**\s*(.+?)(?=\n\**[A-Z]|\n---|\nENTITIES|$)",
            $@"\*\*{entityType}:\*\*\s*(.+?)(?=\n\*\*|\n---|\nENTITIES|$)",
            $@"- \*\*{entityType}\*\*:\s*(.+?)(?=\n- \*\*|\n---|\nENTITIES|$)"
        };
        
        string? value = null;
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                value = match.Groups[1].Value.Trim();
                break;
            }
        }
        
        if (string.IsNullOrWhiteSpace(value)) return [];
        
        // Handle "none" or empty
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("n/a", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
        
        // Clean up markdown artifacts and split
        value = Regex.Replace(value, @"\*\*|\[\d+\]|\[chunk-\d+\]", ""); // Remove ** and citations
        value = Regex.Replace(value, @"\n\s*-\s*", ", "); // Convert bullet lists to comma-separated
        
        // Split by comma, semicolon, or newline and clean up
        var rawEntities = value.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('[', ']', '"', '\'', '*', '-', ' '))
            .ToList();
        
        // Apply strict filtering to remove parser failures
        return rawEntities
            .Where(IsValidEntityEntry)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    
    /// <summary>
    /// Strict validation for entity entries - rejects pronouns, relational phrases, parser artifacts
    /// </summary>
    private static bool IsValidEntityEntry(string entity)
    {
        if (string.IsNullOrWhiteSpace(entity) || entity.Length < 2 || entity.Length > 100)
            return false;
        
        // Reject common parser failures and non-entity text
        var invalidPatterns = new[]
        {
            @"^(his|her|their|its|my|your|our)\s",    // Possessive pronouns  
            @"^(he|she|they|it|we|you)\s",            // Subject pronouns
            @"^(a|an|the|some|any|this|that)\s",      // Articles at start
            @"\b(brother|sister|mother|father|son|daughter|wife|husband|uncle|aunt|cousin)\b(?!\s+\w)", // Relationships without full name
            @"^(young|old|younger|older|eldest)\s",   // Adjective-only entries
            @"^\d+$",                                  // Just numbers
            @"^\[.*\]$",                              // Bracketed parser artifacts
            @"^(none|n/a|unknown|unnamed|not specified|not mentioned)$",
            @"(mentioned|described|referred|appears|seems)", // Meta-language
            @"^(the|a|an)\s.*family$",                // "the X family" - too vague
            @"^\w+\s+family\s*\([^)]*$",              // Incomplete parenthetical like "Lucas family (Sir William"
        };
        
        return !invalidPatterns.Any(p => 
            Regex.IsMatch(entity, p, RegexOptions.IgnoreCase));
    }

    private async Task<string> CreateExecutiveSummaryAsync(
        List<TopicSummary> topicSummaries, string? focus)
    {
        // Truncate each topic summary for small models
        const int maxSummaryLength = 300;
        var summariesText = string.Join("\n", topicSummaries.Select(t =>
        {
            var truncated = t.Summary.Length > maxSummaryLength
                ? t.Summary[..maxSummaryLength] + "..."
                : t.Summary;
            return $"- {t.Topic}: {truncated}";
        }));

        var prompt = Template.GetExecutivePrompt(summariesText, focus);

        return await _ollama.GenerateAsync(prompt);
    }
    
    /// <summary>
    /// Create executive summary using weighted claims AND extracted entities for grounding
    /// This addresses the "creosote problem" where models over-emphasize incidental details
    /// </summary>
    private async Task<string> CreateGroundedExecutiveSummaryAsync(
        List<TopicSummary> topicSummaries,
        List<Claim> weightedClaims,
        ExtractedEntities? entities,
        string? focus,
        DocumentSizeProfile? profile = null)
    {
        profile ??= DocumentSizeProfile.ForChunks(50); // Default to medium
        
        // Get top claims by weight (facts first, then inferences, colour last)
        var topClaims = weightedClaims
            .OrderByDescending(c => c.Weight)
            .Take(profile.TopClaimsCount)
            .ToList();
        
        // Build context from both topic summaries and weighted claims
        var topicContext = string.Join("\n", topicSummaries.Select(t =>
        {
            var truncated = t.Summary.Length > 200 ? t.Summary[..200] + "..." : t.Summary;
            return $"- {t.Topic}: {truncated}";
        }));
        
        // Add highest-weighted claims as prioritized facts
        var claimsByType = topClaims.GroupBy(c => c.Type).OrderByDescending(g => (int)g.Key);
        var claimContext = new StringBuilder();
        
        foreach (var group in claimsByType)
        {
            var typeLabel = group.Key switch
            {
                ClaimType.Fact => "Key Facts",
                ClaimType.Inference => "Key Inferences", 
                _ => "Supporting Details"
            };
            
            claimContext.AppendLine($"\n{typeLabel}:");
            foreach (var claim in group.Take(3))
            {
                claimContext.AppendLine($"  - {claim.Render()}");
            }
        }
        
        // Build entity grounding context - CRITICAL for keeping summary factual
        // Use profile caps to scale with document size
        var entityContext = new StringBuilder();
        if (entities != null && entities.HasAny)
        {
            entityContext.AppendLine("\nVERIFIED ENTITIES (only use these names):");
            if (entities.Characters.Count > 0)
                entityContext.AppendLine($"  Characters: {string.Join(", ", entities.Characters.Take(profile.MaxCharacters))}");
            if (entities.Locations.Count > 0)
                entityContext.AppendLine($"  Locations: {string.Join(", ", entities.Locations.Take(profile.MaxLocations))}");
            if (entities.Organizations.Count > 0)
                entityContext.AppendLine($"  Organizations: {string.Join(", ", entities.Organizations.Take(profile.MaxOther))}");
            if (entities.Events.Count > 0)
                entityContext.AppendLine($"  Key Events: {string.Join(", ", entities.Events.Take(profile.MaxOther))}");
            if (entities.Dates.Count > 0)
                entityContext.AppendLine($"  Dates: {string.Join(", ", entities.Dates.Take(profile.MaxOther))}");
        }
        
        var prompt = $"""
            Write {profile.BulletCount} bullet points synthesizing these topics and claims.
            
            TOPICS:
            {topicContext}
            
            KEY CLAIMS:
            {claimContext}
            {entityContext}
            
            {(focus != null ? $"Focus on: {focus}\n" : "")}
            
            FORMAT: Start directly with • (no preamble)
            • [insight, max {profile.WordsPerBullet} words] [chunk-N]
            • [different insight] [chunk-N]
            • [different insight] [chunk-N]
            
            RULES:
            1. Each bullet = ONE unique insight (what it MEANS, not what happens)
            2. Use DIFFERENT [chunk-N] citations for each bullet - no repeats
            3. NO filler: "as seen in", "as evidenced by", "the text shows"
            4. NO meta: "Here is", "This summary", "the novel explores"
            5. NO author references: "Jane Austen", "Austen's portrayal"
            6. Use ONLY names from VERIFIED ENTITIES list
            
            BAD EXAMPLE: "Social class is explored through relationships, as seen in the text [chunk-4]"
            GOOD EXAMPLE: "Social class functions as invisible currency in the marriage market [chunk-4]"
            """;

        var rawResponse = await _ollama.GenerateAsync(prompt);
        
        // Post-process to remove meta-commentary and fix common issues
        return CleanExecutiveSummary(rawResponse, profile.BulletCount);
    }
    
    /// <summary>
    /// Post-process executive summary to remove meta-commentary and fix citation issues
    /// </summary>
    private static string CleanExecutiveSummary(string summary, int maxBullets = 3)
    {
        var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleanedLines = new List<string>();
        var seenContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip meta-commentary lines
            if (IsMetaCommentary(trimmed))
                continue;
            
            // Skip empty bullet markers
            if (trimmed is "•" or "-" or "*")
                continue;
            
            // Fix invented citations like [1], [2] -> remove them (better no citation than fake)
            var cleaned = Regex.Replace(trimmed, @"\[(\d{1,2})\](?!\d)", "");
            
            // Remove hedging phrases inline
            cleaned = RemoveHedgingInline(cleaned);
            
            // Normalize the bullet point format
            cleaned = NormalizeBullet(cleaned);
            
            // Skip if too short or duplicate content
            if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 10)
                continue;
            
            // Dedupe similar content
            var normalized = Regex.Replace(cleaned.ToLowerInvariant(), @"[^\w\s]", "");
            if (seenContent.Any(s => ComputeSimpleSimilarity(s, normalized) > 0.7))
                continue;
            
            seenContent.Add(normalized);
            cleanedLines.Add(cleaned);
        }
        
        // Enforce bullet max from profile
        return string.Join("\n", cleanedLines.Take(maxBullets));
    }
    
    /// <summary>
    /// Remove hedging phrases and filler language inline while preserving the core claim
    /// </summary>
    private static string RemoveHedgingInline(string text)
    {
        var patternsToRemove = new[]
        {
            // Hedging
            @"\b(appears to|seems to|possibly|likely|probably)\s+",
            @"\b(may be|might be|could be)\s+",
            @"\b(it is possible that|assuming|apparently)\s+",
            @"\b(presumably|potentially|suggests that)\s+",
            
            // Filler phrases that add no information
            @",?\s*as (seen|evidenced|revealed|shown|demonstrated) (in|by)\b",
            @",?\s*as (explored|examined|discussed) (in|by)\b",
            @"\b(the text|the novel|the book|the story|the author)\s+(shows|reveals|demonstrates|explores|examines)\s+(that\s+)?",
            @"\bJane Austen'?s?\s+(exploration|examination|portrayal|depiction)\s+of\s+",
            @"\bin\s+[""']?Pride and Prejudice[""']?\s*",
            @"\bthe author'?s?\s+(exploration|examination)\s+of\s+",
            
            // Redundant citations to the work itself
            @",?\s*as\s+revealed\s+in\s+[""'][^""']+[""']\s*",
        };
        
        var result = text;
        foreach (var pattern in patternsToRemove)
        {
            result = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase);
        }
        
        // Clean up resulting double spaces and trailing commas
        result = Regex.Replace(result, @"\s{2,}", " ");
        result = Regex.Replace(result, @",\s*\.", ".");
        result = Regex.Replace(result, @",\s*$", "");
        
        return result.Trim();
    }
    
    /// <summary>
    /// Normalize bullet point format to consistent • prefix
    /// </summary>
    private static string NormalizeBullet(string line)
    {
        // Remove existing bullet markers and normalize
        var content = line.TrimStart('•', '-', '*', ' ', '\t');
        
        // If it looks like a bullet point, ensure it starts with •
        if (content.Length > 0 && (line.StartsWith("•") || line.StartsWith("-") || line.StartsWith("*")))
        {
            return "• " + content;
        }
        
        // If no bullet but has content, add one
        if (content.Length > 20)
        {
            return "• " + content;
        }
        
        return content;
    }
    
    /// <summary>
    /// Simple word overlap similarity for deduplication
    /// </summary>
    private static double ComputeSimpleSimilarity(string a, string b)
    {
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        if (wordsA.Count == 0 || wordsB.Count == 0) return 0;
        
        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();
        
        return union > 0 ? (double)intersection / union : 0;
    }
    
    /// <summary>
    /// Clean topic name to remove meta-commentary prefixes
    /// </summary>
    private static string CleanTopicName(string topic)
    {
        // Remove common prefixes the LLM adds
        var cleaned = Regex.Replace(topic, @"^(Here are the|The following|Theme \d+:?|Topic \d+:?)\s*", "", RegexOptions.IgnoreCase);
        return cleaned.Trim().TrimEnd(':');
    }
    
    /// <summary>
    /// Clean topic summary to remove meta-commentary and filler
    /// </summary>
    private static string CleanTopicSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return "";
        
        var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleanedLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip meta-commentary
            if (IsMetaCommentary(trimmed))
                continue;
            
            // Remove filler phrases
            var cleaned = RemoveHedgingInline(trimmed);
            
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 10)
                cleanedLines.Add(cleaned);
        }
        
        return string.Join(" ", cleanedLines).Trim();
    }
    
    /// <summary>
    /// Detect meta-commentary that should be stripped
    /// </summary>
    private static bool IsMetaCommentary(string line)
    {
        var metaPatterns = new[]
        {
            @"^(Here is|Here are|Below is|The following)",
            @"^(This summary|This document|Based on the)",
            @"^(I have|I've|Let me|I will|I'll)",
            @"^(Note:|Note that|Please note)",
            @"(executive summary|bullet points?|extracted themes?)[:.]?\s*$",
            @"^(In summary|To summarize|In conclusion)",
            @"^(According to|From the text|The text shows|The text reveals)",
            @"^\*\*Executive Summary\*\*",
            @"^Executive Summary:?\s*$",
            @"^(The novel|The book|The story|The author)\s+(shows|reveals|demonstrates|explores)",
            @"^Here are the"
        };
        
        return metaPatterns.Any(p => 
            Regex.IsMatch(line, p, RegexOptions.IgnoreCase));
    }
    
    /// <summary>
    /// Legacy method - redirects to grounded version
    /// </summary>
    private async Task<string> CreateWeightedExecutiveSummaryAsync(
        List<TopicSummary> topicSummaries,
        List<Claim> weightedClaims,
        string? focus)
    {
        return await CreateGroundedExecutiveSummaryAsync(topicSummaries, weightedClaims, null, focus);
    }
    
    /// <summary>
    /// Synthesize topic with claim extraction and classification
    /// Returns summary, entities, AND typed claims for weighted synthesis
    /// </summary>
    private async Task<(string summary, ExtractedEntities? entities, List<Claim> claims)> SynthesizeTopicWithClaimsAsync(
        string topic,
        List<(string chunkId, string heading, string content, int order)> chunks,
        string? focus,
        DocumentType docType = DocumentType.Unknown,
        int totalChunks = 100)
    {
        // Get base summary and entities with document-type-aware prompts
        var (summary, entities) = await SynthesizeTopicWithEntitiesAsync(topic, chunks, focus, docType, totalChunks);
        
        // Extract and classify claims from the summary
        var claims = ExtractAndClassifyClaims(summary, topic, chunks);
        
        return (summary, entities, claims);
    }
    
    /// <summary>
    /// Extract claims from summary text and classify using TF-IDF
    /// </summary>
    private List<Claim> ExtractAndClassifyClaims(
        string summary, 
        string topic,
        List<(string chunkId, string heading, string content, int order)> sourceChunks)
    {
        var claims = new List<Claim>();
        
        // Split summary into bullet points / sentences
        var lines = summary.Split('\n')
            .Select(l => l.Trim().TrimStart('-', '*', '•').Trim())
            .Where(l => l.Length > 10)
            .ToList();
        
        var sourceText = string.Join(" ", sourceChunks.Select(c => c.content));
        
        foreach (var line in lines)
        {
            // Extract citations from the line
            var citations = ExtractCitations(line);
            
            // Classify claim type using TF-IDF analysis
            var claimType = ClassifyClaimType(line, sourceText, sourceChunks);
            
            // Assess confidence based on evidence
            var confidence = AssessClaimConfidence(line, citations, sourceChunks);
            
            claims.Add(new Claim(
                Text: RemoveCitations(line),
                Type: claimType,
                Confidence: confidence,
                Evidence: citations,
                Topic: topic));
        }
        
        return claims;
    }
    
    /// <summary>
    /// Classify claim type using TF-IDF to detect "colour" vs "fact"
    /// High TF-IDF terms that appear rarely = likely colour (incidental details)
    /// Terms that appear across many chunks = likely plot-critical facts
    /// </summary>
    private ClaimType ClassifyClaimType(
        string claimText,
        string fullSourceText,
        List<(string chunkId, string heading, string content, int order)> sourceChunks)
    {
        var terms = _textAnalysis.Tokenize(claimText);
        if (terms.Count == 0) return ClaimType.Colour;
        
        // Get distinctive terms in this claim
        var distinctiveTerms = terms
            .Select(t => (term: t, type: _textAnalysis.ClassifyTermImportance(t)))
            .ToList();
        
        // Count term types
        var factTerms = distinctiveTerms.Count(t => t.type == ClaimType.Fact);
        var colourTerms = distinctiveTerms.Count(t => t.type == ClaimType.Colour);
        
        // If claim is dominated by rare/distinctive terms, it's likely colour
        if (colourTerms > factTerms * 2)
            return ClaimType.Colour;
        
        // If claim has mostly common terms, it's likely a fact
        if (factTerms > colourTerms)
            return ClaimType.Fact;
        
        // Check if claim text appears verbatim or near-verbatim in source
        var similarity = _textAnalysis.ComputeCombinedSimilarity(
            _textAnalysis.NormalizeForComparison(claimText),
            _textAnalysis.NormalizeForComparison(fullSourceText));
        
        if (similarity > 0.7)
            return ClaimType.Fact;
        
        return ClaimType.Inference;
    }
    
    /// <summary>
    /// Assess confidence level based on evidence quality
    /// </summary>
    private ConfidenceLevel AssessClaimConfidence(
        string claimText,
        List<Citation> citations,
        List<(string chunkId, string heading, string content, int order)> sourceChunks)
    {
        // No citations = low confidence
        if (citations.Count == 0)
            return ConfidenceLevel.Low;
        
        // Multiple citations = high confidence
        if (citations.Count >= 2)
            return ConfidenceLevel.High;
        
        // Single citation - verify it actually supports the claim
        var citedChunk = sourceChunks.FirstOrDefault(c => c.chunkId == citations[0].ChunkId);
        if (citedChunk.content == null)
            return ConfidenceLevel.Uncertain;
        
        // Check if key terms from claim appear in cited chunk
        var claimTerms = _textAnalysis.Tokenize(claimText);
        var chunkTerms = _textAnalysis.Tokenize(citedChunk.content);
        var overlap = claimTerms.Intersect(chunkTerms, StringComparer.OrdinalIgnoreCase).Count();
        var overlapRatio = claimTerms.Count > 0 ? (double)overlap / claimTerms.Count : 0;
        
        return overlapRatio switch
        {
            > 0.5 => ConfidenceLevel.High,
            > 0.3 => ConfidenceLevel.Medium,
            _ => ConfidenceLevel.Low
        };
    }
    
    /// <summary>
    /// Extract citation references from text
    /// </summary>
    private static List<Citation> ExtractCitations(string text)
    {
        var citations = new List<Citation>();
        var matches = Regex.Matches(text, @"\[(chunk-\d+)\]");
        
        foreach (Match match in matches)
        {
            citations.Add(new Citation(match.Groups[1].Value));
        }
        
        return citations;
    }
    
    /// <summary>
    /// Remove citation markers from text for clean display
    /// </summary>
    private static string RemoveCitations(string text)
    {
        return Regex.Replace(text, @"\s*\[chunk-\d+\]", "").Trim();
    }
    
    /// <summary>
    /// Normalize and merge entities using fuzzy deduplication
    /// Addresses issues like "Thaddeus Sholto" vs "Mr. Thaddeus Sholto"
    /// </summary>
    private ExtractedEntities NormalizeAndMergeEntities(List<ExtractedEntities> allEntities)
    {
        // First do basic merge
        var merged = ExtractedEntities.Merge(allEntities);
        
        // Then apply advanced normalization with fuzzy matching
        var normalizedCharacters = _textAnalysis.NormalizeEntities(merged.Characters, "character")
            .Select(e => e.CanonicalName)
            .ToList();
        
        var normalizedLocations = _textAnalysis.NormalizeEntities(merged.Locations, "location")
            .Select(e => e.CanonicalName)
            .ToList();
        
        // Dates and events don't need fuzzy matching typically
        return new ExtractedEntities(
            normalizedCharacters,
            normalizedLocations,
            merged.Dates,
            merged.Events,
            merged.Organizations);
    }

    private async Task EnsureCollectionAsync(string collectionName)
    {
        var collections = await _qdrant.ListCollectionsAsync();
        if (!collections.Any(c => c == collectionName))
        {
            await _qdrant.CreateCollectionAsync(collectionName, _vectorSize);
            if (_verbose) Console.WriteLine($"[Index] Created collection {collectionName}");
        }
    }

    /// <summary>
    /// Generate a stable, unique ID for a chunk point.
    /// Includes order to handle duplicate content (same hash) in documents with repeated sections.
    /// </summary>
    private static Guid GenerateStableId(string docId, string chunkHash, int order)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{docId}:{chunkHash}:{order}"));
        return new Guid(bytes.Take(16).ToArray());
    }

    private static double CalculateCitationRate(string summary)
    {
        var bullets = summary.Split('\n').Count(l => l.TrimStart().StartsWith('-'));
        if (bullets == 0) return 0;
        var citations = Regex.Matches(summary, @"\[chunk-\d+\]").Count;
        return (double)citations / bullets;
    }
}