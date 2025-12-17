using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class RagSummarizer
{
    private readonly OllamaService _ollama;
    private readonly QdrantHttpClient _qdrant;
    private readonly bool _verbose;
    private readonly int _maxParallelism;
    private readonly bool _deleteCollectionAfterSummarization;
    private const string CollectionPrefix = "docsummarizer_";
    private const int VectorSize = 1024; // mxbai-embed-large

    /// <summary>
    /// Default max parallelism for LLM calls. Ollama processes one request at a time per model,
    /// so high values just queue requests. 8 is a good balance for throughput vs memory.
    /// </summary>
    public const int DefaultMaxParallelism = 8;

    public RagSummarizer(
        OllamaService ollama, 
        string qdrantHost = "localhost", 
        bool verbose = false, 
        int maxParallelism = DefaultMaxParallelism,
        QdrantConfig? qdrantConfig = null)
    {
        _ollama = ollama;
        
        // Use HTTP client instead of gRPC - gRPC has AOT compatibility issues with System.Single marshalling
        var port = qdrantConfig?.Port ?? 6333; // REST port
        var apiKey = qdrantConfig?.ApiKey;
        _qdrant = new QdrantHttpClient(qdrantHost, port, apiKey);
        
        _verbose = verbose;
        _maxParallelism = maxParallelism > 0 ? maxParallelism : DefaultMaxParallelism;
        _deleteCollectionAfterSummarization = qdrantConfig?.DeleteCollectionAfterSummarization ?? true;
    }

    /// <summary>
    /// Generate a unique collection name for a document to prevent collisions
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
    /// Delete the collection for a document
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
            {
                throw new InvalidOperationException(
                    $"Embedding dimension mismatch: expected {VectorSize}, got {embedding.Length}. " +
                    $"Ensure you have the correct embedding model (mxbai-embed-large) pulled in Ollama.");
            }
            
            var pointId = GenerateStableId(docId, chunk.Hash);

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
            // Index first
            await IndexDocumentAsync(docId, chunks);

            // Extract topics
            var headings = chunks.Select(c => c.Heading).Where(h => !string.IsNullOrEmpty(h)).ToList();
            var topics = await ExtractTopicsAsync(headings);
            
            if (_verbose) Console.WriteLine($"[Topics] Extracted {topics.Count} topics");

            // Retrieve and summarize per topic - run in parallel for speed
            var allRetrievedChunks = new HashSet<string>();
            
            // First, retrieve chunks for all topics in parallel (embeddings are fast)
            var retrievalTasks = topics.Select(async topic =>
            {
                var query = focusQuery != null ? $"{topic} {focusQuery}" : topic;
                var retrieved = await RetrieveChunksAsync(collectionName, query, topK: 3);
                return (topic, retrieved);
            }).ToList();
            
            var retrievalResults = await Task.WhenAll(retrievalTasks);
            
            // Now synthesize topics in parallel (LLM calls - this is the slow part)
            var synthesizeTasks = retrievalResults.Select(async r =>
            {
                var (topic, retrieved) = r;
                var summary = await SynthesizeTopicAsync(topic, retrieved, focusQuery);
                
                if (_verbose) Console.WriteLine($"  [{topic}] Retrieved {retrieved.Count} chunks");
                
                return (topic, summary, chunkIds: retrieved.Select(c => c.chunkId).ToList());
            }).ToList();
            
            var synthesisResults = await Task.WhenAll(synthesizeTasks);
            
            // Build results maintaining topic order
            var topicSummaries = synthesisResults
                .Select(r => new TopicSummary(r.topic, r.summary, r.chunkIds))
                .ToList();
            
            foreach (var result in retrievalResults)
            {
                foreach (var c in result.retrieved) 
                    allRetrievedChunks.Add(c.chunkId);
            }

            // Final synthesis
            var executive = await CreateExecutiveSummaryAsync(topicSummaries, focusQuery);
            
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
                    topics, sw.Elapsed, coverage, citationRate));
        }
        finally
        {
            // Clean up collection after summarization (unless configured to keep it)
            if (_deleteCollectionAfterSummarization)
            {
                await DeleteCollectionAsync(docId);
            }
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

        return results.Select(r => (
            r.Payload.GetValueOrDefault("chunkId", ""),
            r.Payload.GetValueOrDefault("heading", ""),
            r.Payload.GetValueOrDefault("content", "")
        )).ToList();
    }

    private async Task<string> SynthesizeTopicAsync(
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

        var prompt = $"""
            Topic: {topic}
            {(focus != null ? $"Focus: {focus}\n" : "")}
            Sources:
            {context}

            Write 2-3 bullet points summarizing this topic. End each with [{chunks.FirstOrDefault().chunkId}].
            """;

        var response = await _ollama.GenerateAsync(prompt);
        
        // Skip citation validation for small models - they struggle with it
        return response;
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

        var prompt = $"""
            {(focus != null ? $"Focus: {focus}\n" : "")}Topics covered:
            {summariesText}

            Write a 3-5 sentence executive summary of the key points.
            """;

        return await _ollama.GenerateAsync(prompt);
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

    private static Guid GenerateStableId(string docId, string chunkHash)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{docId}:{chunkHash}"));
        return new Guid(bytes.Take(16).ToArray());
    }

    private static double CalculateCitationRate(string summary)
    {
        var bullets = summary.Split('\n').Count(l => l.TrimStart().StartsWith('-'));
        if (bullets == 0) return 0;
        var citations = System.Text.RegularExpressions.Regex.Matches(summary, @"\[chunk-\d+\]").Count;
        return (double)citations / bullets;
    }
}
