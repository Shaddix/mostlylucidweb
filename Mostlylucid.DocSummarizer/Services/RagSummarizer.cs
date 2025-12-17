using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class RagSummarizer
{
    private const string CollectionPrefix = "docsummarizer_";
    private const int VectorSize = 768; // nomic-embed-text

    /// <summary>
    ///     Default max parallelism for LLM calls. Ollama processes one request at a time per model,
    ///     so high values just queue requests. 8 is a good balance for throughput vs memory.
    /// </summary>
    public const int DefaultMaxParallelism = 8;

    private readonly bool _deleteCollectionAfterSummarization;
    private readonly int _maxParallelism;
    private readonly OllamaService _ollama;
    private readonly QdrantHttpClient _qdrant;
    private readonly bool _verbose;
    private readonly TextAnalysisService _textAnalysis;

    public RagSummarizer(
        OllamaService ollama,
        string qdrantHost = "localhost",
        bool verbose = false,
        int maxParallelism = DefaultMaxParallelism,
        QdrantConfig? qdrantConfig = null,
        SummaryTemplate? template = null,
        TextAnalysisService? textAnalysis = null)
    {
        _ollama = ollama;
        _textAnalysis = textAnalysis ?? new TextAnalysisService();

        // Use HTTP client instead of gRPC - gRPC has AOT compatibility issues with System.Single marshalling
        var port = qdrantConfig?.Port ?? 6333; // REST port
        var apiKey = qdrantConfig?.ApiKey;
        _qdrant = new QdrantHttpClient(qdrantHost, port, apiKey);

        _verbose = verbose;
        _maxParallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
        _deleteCollectionAfterSummarization = qdrantConfig?.DeleteCollectionAfterSummarization ?? true;
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

        // Embeddings must be sequential - Ollama can only process one at a time
        // Parallel requests cause connection failures
        Console.WriteLine($"[Index] Indexing {chunks.Count} chunks (sequential - Ollama limitation)...");
        Console.Out.Flush();

        // Batch upsert to avoid OOM - each embedding is 4KB (1024 floats)
        const int batchSize = 10;
        var batch = new List<QdrantPoint>(batchSize);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var embedding = await _ollama.EmbedAsync(chunk.Content);

            // Validate embedding dimensions
            if (embedding.Length != VectorSize)
                throw new InvalidOperationException(
                    $"Embedding dimension mismatch: expected {VectorSize}, got {embedding.Length}. " +
                    $"Ensure you have the correct embedding model (nomic-embed-text) pulled in Ollama.");

            // Include order in ID to handle duplicate content (same hash)
            var pointId = GenerateStableId(docId, chunk.Hash, chunk.Order);

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
                    ["content"] = chunk.Content,
                    ["hash"] = chunk.Hash
                }
            });

            // Upsert batch when full to free memory
            if (batch.Count >= batchSize)
            {
                await _qdrant.UpsertAsync(collectionName, batch);
                batch.Clear();
            }

            if (_verbose)
            {
                Console.WriteLine($"  Embedded [{chunk.Id}] {chunk.Heading} ({embedding.Length} dims)");
            }
            else
            {
                Console.Write($"\r  Progress: {i + 1}/{chunks.Count} chunks embedded");
                Console.Out.Flush();
            }
        }

        // Upsert remaining batch
        if (batch.Count > 0)
        {
            await _qdrant.UpsertAsync(collectionName, batch);
            batch.Clear();
        }

        if (!_verbose) Console.WriteLine(); // New line after progress
    }

    public async Task<DocumentSummary> SummarizeAsync(
        string docId,
        List<DocumentChunk> chunks,
        string? focusQuery = null)
    {
        var sw = Stopwatch.StartNew();
        var collectionName = GetCollectionName(docId);

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
            var topics = await ExtractTopicsAsync(headings);

            if (_verbose) Console.WriteLine($"[Topics] Extracted {topics.Count} topics");

            // Retrieve and summarize per topic - run in parallel for speed
            var allRetrievedChunks = new HashSet<string>();
            var allEntities = new List<ExtractedEntities>();
            var claimLedger = new ClaimLedger();

            // First, retrieve chunks for all topics in parallel (embeddings are fast)
            var retrievalTasks = topics.Select(async topic =>
            {
                var query = focusQuery != null ? $"{topic} {focusQuery}" : topic;
                var retrieved = await RetrieveChunksAsync(collectionName, query, 3);
                return (topic, retrieved);
            }).ToList();

            var retrievalResults = await Task.WhenAll(retrievalTasks);

            // Now synthesize topics in parallel (LLM calls - this is the slow part)
            var synthesizeTasks = retrievalResults.Select(async r =>
            {
                var (topic, retrieved) = r;
                var (summary, entities, claims) = await SynthesizeTopicWithClaimsAsync(topic, retrieved, focusQuery);

                if (_verbose) Console.WriteLine($"  [{topic}] Retrieved {retrieved.Count} chunks, {claims.Count} claims");

                return (topic, summary, entities, claims, chunkIds: retrieved.Select(c => c.chunkId).ToList());
            }).ToList();

            var synthesisResults = await Task.WhenAll(synthesizeTasks);

            // Build results maintaining topic order
            var topicSummaries = synthesisResults
                .Select(r => new TopicSummary(r.topic, r.summary, r.chunkIds))
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
                topicSummaries, deduplicatedClaims, mergedEntities, focusQuery);

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

            return new DocumentSummary(
                executive,
                topicSummaries,
                [],
                new SummarizationTrace(
                    docId, chunks.Count, allRetrievedChunks.Count,
                    topics, sw.Elapsed, coverage, citationRate),
                mergedEntities);
        }
        finally
        {
            // Clean up collection after summarization (unless configured to keep it)
            if (_deleteCollectionAfterSummarization) await DeleteCollectionAsync(docId);
        }
    }

    private async Task<List<string>> ExtractTopicsAsync(List<string> headings)
    {
        // For small models, limit headings to avoid overwhelming context
        var limitedHeadings = headings.Take(15).ToList();

        var prompt = $"""
                      List 3-5 main topics from these headings:
                      {string.Join(", ", limitedHeadings)}

                      Output format: one topic per line, no bullets or numbers.
                      """;

        var response = await _ollama.GenerateAsync(prompt);
        return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '))
            .Where(t => t.Length > 2 && t.Length < 100) // Filter out overly long "topics"
            .Take(5) // Reduce from 8 to 5 for faster processing
            .ToList();
    }

    private async Task<List<(string chunkId, string heading, string content)>> RetrieveChunksAsync(
        string collectionName, string query, int topK)
    {
        var queryEmbedding = await _ollama.EmbedAsync(query);

        // No filter needed since each document has its own collection
        var results = await _qdrant.SearchAsync(collectionName, queryEmbedding, topK);

        return results.Select(r =>
        {
            var payload = r.GetPayloadStrings();
            return (
                payload.GetValueOrDefault("chunkId", ""),
                payload.GetValueOrDefault("heading", ""),
                payload.GetValueOrDefault("content", "")
            );
        }).ToList();
    }

    private async Task<string> SynthesizeTopicAsync(
        string topic,
        List<(string chunkId, string heading, string content)> chunks,
        string? focus,
        bool retry = false)
    {
        var (summary, _) = await SynthesizeTopicWithEntitiesAsync(topic, chunks, focus, retry);
        return summary;
    }
    
    private async Task<(string summary, ExtractedEntities? entities)> SynthesizeTopicWithEntitiesAsync(
        string topic,
        List<(string chunkId, string heading, string content)> chunks,
        string? focus,
        bool retry = false)
    {
        // Truncate content to ~500 chars per chunk for small models
        const int maxContentPerChunk = 500;
        var context = string.Join("\n", chunks.Select(c =>
        {
            var truncated = c.content.Length > maxContentPerChunk
                ? c.content[..maxContentPerChunk] + "..."
                : c.content;
            return $"[{c.chunkId}]: {truncated}";
        }));

        // Combined prompt for summary + entity extraction
        var prompt = $"""
            Analyze this content about "{topic}":
            
            {context}
            
            {(focus != null ? $"Focus on: {focus}\n" : "")}
            
            Provide your response in this EXACT format:
            
            SUMMARY:
            Write 2-3 bullet points summarizing the key information about {topic}. Include [chunk-N] citations.
            
            ENTITIES:
            Characters: [comma-separated list of person names mentioned, or "none"]
            Locations: [comma-separated list of places mentioned, or "none"]
            Dates: [comma-separated list of dates/time periods mentioned, or "none"]
            Events: [comma-separated list of key events mentioned, or "none"]
            Organizations: [comma-separated list of organizations/groups mentioned, or "none"]
            """;

        var response = await _ollama.GenerateAsync(prompt);

        // Parse the response
        var (summary, entities) = ParseSummaryAndEntities(response);
        
        return (summary, entities);
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
        return value.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Trim('[', ']', '"', '\'', '*', '-', ' '))
            .Where(s => !string.IsNullOrWhiteSpace(s) && 
                        s.Length > 1 &&
                        s.Length < 100 && // Filter out overly long entries
                        !s.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                        !s.StartsWith("no ", StringComparison.OrdinalIgnoreCase) &&
                        !s.Contains("not mentioned", StringComparison.OrdinalIgnoreCase) &&
                        !s.Contains("explicitly mentioned", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        string? focus)
    {
        // Get top claims by weight (facts first, then inferences, colour last)
        var topClaims = weightedClaims
            .OrderByDescending(c => c.Weight)
            .Take(10)
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
        var entityContext = new StringBuilder();
        if (entities != null && entities.HasAny)
        {
            entityContext.AppendLine("\nVERIFIED ENTITIES (only use these names):");
            if (entities.Characters.Count > 0)
                entityContext.AppendLine($"  Characters: {string.Join(", ", entities.Characters.Take(10))}");
            if (entities.Locations.Count > 0)
                entityContext.AppendLine($"  Locations: {string.Join(", ", entities.Locations.Take(8))}");
            if (entities.Organizations.Count > 0)
                entityContext.AppendLine($"  Organizations: {string.Join(", ", entities.Organizations.Take(5))}");
            if (entities.Events.Count > 0)
                entityContext.AppendLine($"  Key Events: {string.Join(", ", entities.Events.Take(5))}");
            if (entities.Dates.Count > 0)
                entityContext.AppendLine($"  Dates: {string.Join(", ", entities.Dates.Take(5))}");
        }
        
        var prompt = $"""
            Create an executive summary from these topic summaries, claims, and verified entities.
            
            TOPIC SUMMARIES:
            {topicContext}
            
            PRIORITIZED CLAIMS (ordered by importance):
            {claimContext}
            {entityContext}
            
            {(focus != null ? $"Focus on: {focus}\n" : "")}
            
            CRITICAL INSTRUCTIONS:
            - ONLY use character/location/organization names from the VERIFIED ENTITIES list
            - Do NOT invent or hallucinate any names not in the verified list
            - Lead with the most important facts (high-confidence, well-evidenced claims)
            - Include relevant inferences but label them appropriately  
            - Minimize incidental details unless they're plot-critical
            - Preserve [chunk-N] citations from the claims
            - Write 4-6 bullet points maximum
            - Be concise and factual
            """;

        return await _ollama.GenerateAsync(prompt);
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
        List<(string chunkId, string heading, string content)> chunks,
        string? focus,
        bool retry = false)
    {
        // Get base summary and entities
        var (summary, entities) = await SynthesizeTopicWithEntitiesAsync(topic, chunks, focus, retry);
        
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
        List<(string chunkId, string heading, string content)> sourceChunks)
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
        List<(string chunkId, string heading, string content)> sourceChunks)
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
        List<(string chunkId, string heading, string content)> sourceChunks)
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
            await _qdrant.CreateCollectionAsync(collectionName, VectorSize);
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