# GraphRAG Part 2: Minimum Viable Implementation with DuckDB and Hybrid Indexing

<datetime class="hidden">2025-12-26T14:00</datetime>
<!-- category -- ASP.NET, GraphRAG, DuckDB, Vector Search, Machine Learning, Knowledge Graphs -->

In [Part 1](/blog/graphrag-knowledge-graphs-for-rag), we explored why GraphRAG matters: it enables corpus-level reasoning that pure vector search can't handle. Now let's build a **minimum viable GraphRAG** that focuses on practical implementation over theoretical completeness.

This is not Microsoft's full GraphRAG. This is a pragmatic, cost-conscious implementation that combines:
- **DuckDB** for unified storage (vectors + graph + metadata in a single file)
- **Hybrid entity extraction** (regex + embeddings + optional LLM)
- **BM25 + BERT** for search (sparse + dense retrieval)
- **Ollama** for local LLM tasks (zero API costs)

**Series Navigation:**
- [Part 1: GraphRAG Fundamentals](/blog/graphrag-knowledge-graphs-for-rag) - Why knowledge graphs matter for RAG
- **Part 2: Minimum Viable GraphRAG** (this article) - Building it with DuckDB and hybrid indexing

[TOC]

# Why DuckDB?

Microsoft's GraphRAG reference implementation uses separate storage for vectors (LanceDB), entities (Parquet files), and relationships (more Parquet). This works, but it's complex.

**DuckDB simplifies everything:**
- Single `.duckdb` file contains vectors, graph, and metadata
- Built-in VSS extension for HNSW vector search
- Native support for arrays (for embeddings and entity lists)
- SQL queries for both vector search and graph traversal
- Zero deployment complexity (just a file)

Think of it as SQLite for analytics, with first-class vector support.

## Storage Schema

Our schema captures five core concepts in a single database:

```sql
-- Documents (source markdown files)
CREATE TABLE documents (
    id VARCHAR PRIMARY KEY,
    path VARCHAR NOT NULL,
    title VARCHAR,
    content TEXT NOT NULL,
    indexed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Chunks with embeddings for vector search
CREATE TABLE chunks (
    id VARCHAR PRIMARY KEY,
    document_id VARCHAR REFERENCES documents(id),
    chunk_index INTEGER,
    text TEXT NOT NULL,
    embedding FLOAT[384],  -- all-MiniLM-L6-v2 dimension
    token_count INTEGER
);

-- HNSW index for fast vector similarity search
CREATE INDEX chunks_embedding_idx 
ON chunks USING HNSW (embedding) 
WITH (metric = 'cosine');

-- Entities extracted from chunks
CREATE TABLE entities (
    id VARCHAR PRIMARY KEY,
    name VARCHAR NOT NULL,
    normalized_name VARCHAR NOT NULL,  -- for deduplication
    type VARCHAR NOT NULL,
    description TEXT,
    chunk_ids VARCHAR[],  -- which chunks mention this entity
    mention_count INTEGER DEFAULT 1,
    UNIQUE(normalized_name)
);

-- Relationships between entities
CREATE TABLE relationships (
    id VARCHAR PRIMARY KEY,
    source_entity_id VARCHAR REFERENCES entities(id),
    target_entity_id VARCHAR REFERENCES entities(id),
    relationship_type VARCHAR NOT NULL,
    description TEXT,
    weight FLOAT DEFAULT 1.0,
    chunk_ids VARCHAR[],
    UNIQUE(source_entity_id, target_entity_id, relationship_type)
);

-- Communities from Leiden clustering
CREATE TABLE communities (
    id VARCHAR PRIMARY KEY,
    level INTEGER NOT NULL,  -- hierarchical community levels
    entity_ids VARCHAR[] NOT NULL,
    summary TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

The key insight: **array types** (`VARCHAR[]`, `FLOAT[]`) let us store complex data without normalization. `chunk_ids` tracks entity provenance, `embedding` enables vector search, and the graph emerges from `entities` + `relationships`.

# The Implementation

The code is in `Mostlylucid.GraphRag/` with this structure:

```
Mostlylucid.GraphRag/
├── Storage/
│   └── GraphRagDb.cs              # DuckDB storage layer
├── Extraction/
│   └── HybridEntityExtractor.cs   # Entity/relationship extraction
├── Search/
│   ├── BM25Scorer.cs              # Sparse keyword search
│   └── HybridSearchService.cs     # BM25 + BERT fusion
├── Graph/
│   ├── LeidenCommunityDetector.cs # Community clustering
│   └── CommunitySummarizer.cs     # LLM-based summaries
├── Query/
│   └── QueryEngine.cs             # Local/Global/DRIFT modes
└── GraphRagPipeline.cs            # Orchestration
```

Let's walk through each component.

## Storage Layer: GraphRagDb

`GraphRagDb.cs` wraps DuckDB with a clean API. The interesting parts:

### Initialization

```csharp
public async Task InitializeAsync()
{
    var connectionString = $"Data Source={_dbPath}";
    _connection = new DuckDBConnection(connectionString);
    await _connection.OpenAsync();

    // Load VSS extension for vector similarity search
    await ExecuteAsync("INSTALL vss; LOAD vss;");
    
    // Enable experimental HNSW persistence
    await ExecuteAsync("SET hnsw_enable_experimental_persistence = true;");

    await CreateTablesAsync();
}
```

The VSS extension provides `array_cosine_similarity()` and HNSW indexing. Enabling experimental persistence ensures the HNSW index survives restarts.

### Vector Search

Vector similarity search uses DuckDB's native cosine similarity:

```csharp
public async Task<List<ChunkResult>> SearchChunksAsync(float[] queryEmbedding, int topK = 10)
{
    var results = new List<ChunkResult>();
    
    using var cmd = _connection!.CreateCommand();
    cmd.CommandText = $"""
        SELECT c.id, c.document_id, c.text, c.chunk_index,
               array_cosine_similarity(c.embedding, $1::FLOAT[{_embeddingDimension}]) as similarity
        FROM chunks c
        WHERE c.embedding IS NOT NULL
        ORDER BY similarity DESC
        LIMIT $2
        """;
    cmd.Parameters.Add(new DuckDBParameter { Value = queryEmbedding });
    cmd.Parameters.Add(new DuckDBParameter { Value = topK });

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        results.Add(new ChunkResult
        {
            Id = reader.GetString(0),
            DocumentId = reader.GetString(1),
            Text = reader.GetString(2),
            ChunkIndex = reader.GetInt32(3),
            Similarity = reader.GetFloat(4)
        });
    }

    return results;
}
```

The `array_cosine_similarity()` function computes similarity inline. The HNSW index makes this fast even for large corpora.

### Entity Deduplication

Entity normalization prevents "ASP.NET Core", "ASP.NET", and "aspnetcore" from becoming separate entities:

```csharp
public async Task UpsertEntityAsync(string id, string name, string type, 
    string? description, string[] chunkIds)
{
    var normalizedName = NormalizeName(name);
    
    await ExecuteAsync("""
        INSERT INTO entities (id, name, normalized_name, type, description, chunk_ids, mention_count)
        VALUES ($1, $2, $3, $4, $5, $6, 1)
        ON CONFLICT (normalized_name) DO UPDATE SET
            description = COALESCE(EXCLUDED.description, entities.description),
            chunk_ids = array_distinct(array_concat(entities.chunk_ids, EXCLUDED.chunk_ids)),
            mention_count = entities.mention_count + 1
        """, id, name, normalizedName, type, description, chunkIds);
}

private static string NormalizeName(string name) =>
    name.ToLowerInvariant()
        .Replace(".", "")
        .Replace("-", "")
        .Replace("_", "")
        .Trim();
```

The `ON CONFLICT (normalized_name)` clause merges entities, incrementing `mention_count` and combining `chunk_ids`.

## Hybrid Entity Extraction

This is where we diverge from Microsoft's GraphRAG. Instead of **2 LLM calls per chunk** (one for entities, one for relationships), we use a **hybrid approach**:

1. **Regex/heuristics** extract candidate entities (fast, deterministic)
2. **BERT embeddings** deduplicate entities via similarity
3. **BM25 co-occurrence** detects relationships
4. **LLM (optional)** classifies entity types and generates descriptions

This reduces LLM calls from **O(chunks)** to **O(entities)**, which is typically 10-50x fewer.

### Phase 1: Candidate Extraction (Heuristic)

We use regex patterns tuned for technical content:

```csharp
// PascalCase terms (e.g., EntityFramework, SignalR)
private static readonly Regex TechPatternPascal = new(
    @"\b([A-Z][a-z]+(?:[A-Z][a-z]+)+)\b", RegexOptions.Compiled);

// Dotted terms (e.g., ASP.NET, System.Text.Json)
private static readonly Regex TechPatternDotted = new(
    @"\b([A-Z][a-zA-Z]*(?:\.[A-Z][a-zA-Z]*)+)\b", RegexOptions.Compiled);

// Acronyms (e.g., API, REST, SQL)
private static readonly Regex TechPatternAcronym = new(
    @"\b([A-Z]{2,}(?:\.[A-Z]+)*)\b", RegexOptions.Compiled);

// Inline code references
private static readonly Regex CodePattern = new(
    @"`([^`]+)`", RegexOptions.Compiled);
```

We also boost known technology terms:

```csharp
private static readonly HashSet<string> KnownTechTerms = new(StringComparer.OrdinalIgnoreCase)
{
    "ASP.NET", "Entity Framework", "Docker", "Kubernetes", "PostgreSQL", "Redis",
    "HTMX", "Alpine.js", "Tailwind", "Blazor", "SignalR", "gRPC", "REST", "GraphQL",
    "ONNX", "BERT", "LLM", "RAG", "Qdrant", "Ollama", "OpenAI", "Anthropic",
    "C#", "JavaScript", "TypeScript", "Python", "SQL", "JSON", "YAML", "Markdown",
    "GitHub", "Azure", "AWS", "Linux", "Windows", "Nginx", "Caddy", "YARP"
};
```

This gives us a **confidence score** for each candidate. Known terms get 1.0, PascalCase gets 0.7, acronyms get 0.6.

### Phase 2: Deduplication via Embeddings

Regex produces many near-duplicates. We use BERT embeddings to merge them:

```csharp
private async Task<List<EntityCandidate>> DeduplicateEntitiesAsync(
    List<EntityCandidate> candidates, CancellationToken ct)
{
    // Filter low-confidence candidates
    var significant = candidates
        .Where(c => c.MentionCount >= 2 || c.Confidence >= 0.9)
        .OrderByDescending(c => c.MentionCount * c.Confidence)
        .ToList();

    if (significant.Count <= 1)
        return significant;

    // Generate embeddings for entity names
    var names = significant.Select(c => c.Name).ToList();
    var embeddings = await _embedder.EmbedBatchAsync(names, ct);

    // Find similar entities and merge
    var merged = new List<EntityCandidate>();
    var processed = new HashSet<int>();

    for (int i = 0; i < significant.Count; i++)
    {
        if (processed.Contains(i)) continue;

        var canonical = significant[i];
        processed.Add(i);

        // Find similar entities
        for (int j = i + 1; j < significant.Count; j++)
        {
            if (processed.Contains(j)) continue;

            var similarity = CosineSimilarity(embeddings[i], embeddings[j]);
            var stringSim = StringSimilarity(canonical.Name, significant[j].Name);

            if (similarity > 0.85 || stringSim > 0.8)
            {
                // Merge into canonical
                var other = significant[j];
                canonical.MentionCount += other.MentionCount;
                canonical.ChunkIds.UnionWith(other.ChunkIds);
                canonical.Contexts.UnionWith(other.Contexts);
                processed.Add(j);
            }
        }

        merged.Add(canonical);
    }

    return merged;
}
```

This merges "Docker Compose", "docker-compose", and "DockerCompose" into a single entity.

### Phase 3: Relationship Extraction via Co-occurrence

Instead of asking the LLM "what relationships exist in this chunk?", we use **co-occurrence statistics**:

```csharp
// Track which entities appear together
var coOccurrences = new Dictionary<(string, string), int>();

for (int i = 0; i < chunks.Count; i++)
{
    var chunk = chunks[i];
    var candidates = ExtractCandidatesFromText(chunk.Text);

    // Track co-occurrences for relationship detection
    var names = candidates.Select(c => c.Name).Distinct().ToList();
    for (int j = 0; j < names.Count; j++)
    {
        for (int k = j + 1; k < names.Count; k++)
        {
            var pair = (names[j], names[k]);
            if (string.Compare(pair.Item1, pair.Item2, StringComparison.OrdinalIgnoreCase) > 0)
                pair = (pair.Item2, pair.Item1);
            
            coOccurrences[pair] = coOccurrences.GetValueOrDefault(pair) + 1;
        }
    }
}
```

Then we filter to significant pairs:

```csharp
private async Task<int> ExtractRelationshipsAsync(
    List<EntityCandidate> entities,
    Dictionary<(string, string), int> coOccurrences,
    CancellationToken ct)
{
    var entityLookup = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    var count = 0;

    // Filter to significant co-occurrences
    var significantPairs = coOccurrences
        .Where(kv => kv.Value >= 2) // At least 2 co-occurrences
        .Where(kv => entityLookup.ContainsKey(kv.Key.Item1) && entityLookup.ContainsKey(kv.Key.Item2))
        .OrderByDescending(kv => kv.Value)
        .Take(500) // Limit relationships
        .ToList();

    foreach (var (pair, occurrences) in significantPairs)
    {
        var source = entityLookup[pair.Item1];
        var target = entityLookup[pair.Item2];

        var relType = InferRelationshipType(source, target);
        
        await _store.UpsertRelationshipAsync(
            relId,
            sourceId,
            targetId,
            relType,
            null,
            commonChunks);

        count++;
    }

    return count;
}
```

Relationship types are inferred heuristically:

```csharp
private static string InferRelationshipType(EntityCandidate source, EntityCandidate target)
{
    if (source.Type == "framework" && target.Type == "library")
        return "uses";
    if (source.Type == "library" && target.Type == "framework")
        return "part_of";
    if (source.Type == target.Type)
        return "related_to";
    
    return "associated_with";
}
```

This is simplistic but works surprisingly well for technology content.

### Phase 4: LLM Classification (Optional)

If an LLM is available, we batch-classify entities:

```csharp
private async Task ClassifyEntitiesAsync(List<EntityCandidate> entities, CancellationToken ct)
{
    if (_llm == null || !await _llm.IsAvailableAsync(ct))
    {
        // Fallback to heuristic classification
        ClassifyEntitiesHeuristic(entities);
        return;
    }

    // Batch classify with LLM (single call for all entities)
    var entityList = string.Join("\n", entities.Take(100).Select(e => 
        $"- {e.Name}: mentioned {e.MentionCount} times"));

    var prompt = $"""
        Classify these entities from a technical programming blog.
        
        Entities:
        {entityList}
        
        For each entity, provide the type and a brief description.
        Types: technology, concept, pattern, library, framework, language, tool, service
        
        Return as a simple list:
        EntityName|type|brief description
        
        Example:
        Docker|tool|Container runtime platform
        HTMX|library|HTML-over-the-wire library
        """;

    try
    {
        var response = await _llm.GenerateAsync(prompt, ct);
        ParseClassificationResponse(entities, response);
    }
    catch
    {
        // Fallback on error
        ClassifyEntitiesHeuristic(entities);
    }
}
```

This is a **single LLM call** for up to 100 entities, versus Microsoft's approach of 2 calls per chunk.

## Hybrid Search: BM25 + BERT

GraphRAG's Local Search works best when you combine semantic (BERT) and lexical (BM25) retrieval. We use **Reciprocal Rank Fusion (RRF)** to merge results.

### BM25 Scorer

BM25 is a classic IR algorithm that scores documents based on term frequency and inverse document frequency:

```csharp
public class BM25Scorer
{
    private readonly double _k1;  // Term frequency saturation (default 1.5)
    private readonly double _b;   // Length normalization (default 0.75)
    
    private Dictionary<string, double> _idf = new();
    private double _avgDocLength;
    private int _corpusSize;

    public void Initialize(IEnumerable<BM25Document> documents)
    {
        var docList = documents.ToList();
        _corpusSize = docList.Count;
        
        // Document frequencies (how many docs contain each term)
        var docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalLength = 0L;
        
        foreach (var doc in docList)
        {
            var tokens = Tokenize(doc.Text);
            var uniqueTokens = tokens.Distinct(StringComparer.OrdinalIgnoreCase);
            
            totalLength += tokens.Count;
            
            foreach (var token in uniqueTokens)
            {
                docFreq[token] = docFreq.GetValueOrDefault(token) + 1;
            }
        }
        
        _avgDocLength = _corpusSize > 0 ? (double)totalLength / _corpusSize : 1;
        
        // Compute IDF for each term
        _idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (term, df) in docFreq)
        {
            _idf[term] = Math.Log((_corpusSize - df + 0.5) / (df + 0.5) + 1);
        }
    }

    public double Score(BM25Document document, string query)
    {
        var queryTokens = Tokenize(query);
        var docTokens = Tokenize(document.Text);
        var docLength = docTokens.Count;
        
        // Count term frequencies in document
        var termFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in docTokens)
        {
            termFreq[token] = termFreq.GetValueOrDefault(token) + 1;
        }
        
        // BM25 score
        double score = 0;
        foreach (var term in queryTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!termFreq.TryGetValue(term, out var tf)) continue;
            if (!_idf.TryGetValue(term, out var idf)) continue;
            
            // BM25 formula
            var numerator = tf * (_k1 + 1);
            var denominator = tf + _k1 * (1 - _b + _b * docLength / _avgDocLength);
            score += idf * numerator / denominator;
        }
        
        return score;
    }
}
```

BM25 is excellent at finding exact keyword matches that embeddings might miss.

### RRF Fusion

Reciprocal Rank Fusion combines rankings from multiple sources:

```csharp
public async Task<List<SearchResult>> SearchAsync(string query, int topK = 10,
    CancellationToken ct = default)
{
    // 1. Dense search: ONNX BERT embeddings
    var queryEmbedding = await _embedder.EmbedAsync(query, ct);
    var denseResults = await _db.SearchChunksAsync(queryEmbedding, topK * 2);

    // 2. Sparse search: BM25
    var allChunks = await _db.GetAllChunksAsync();
    var bm25Docs = allChunks.Select(c => new BM25Document(c.Id, c.Text)).ToList();
    var bm25Results = _bm25.ScoreAll(bm25Docs, query).Take(topK * 2).ToList();

    // 3. RRF fusion
    var rrfScores = new Dictionary<string, (double Score, ChunkResult Chunk)>();

    // Add dense scores
    for (int i = 0; i < denseResults.Count; i++)
    {
        var chunk = denseResults[i];
        var rrfScore = 1.0 / (_rrfK + i + 1);  // _rrfK typically 60
        rrfScores[chunk.Id] = (rrfScore, chunk);
    }

    // Add BM25 scores
    for (int i = 0; i < bm25Results.Count; i++)
    {
        var docId = bm25Results[i].document.Id;
        var rrfScore = 1.0 / (_rrfK + i + 1);
        
        if (rrfScores.TryGetValue(docId, out var existing))
        {
            rrfScores[docId] = (existing.Score + rrfScore, existing.Chunk);
        }
        else
        {
            var chunk = allChunks.First(c => c.Id == docId);
            rrfScores[docId] = (rrfScore, chunk);
        }
    }

    // Sort by RRF score and return top K
    return rrfScores
        .OrderByDescending(kv => kv.Value.Score)
        .Take(topK)
        .Select(kv => new SearchResult
        {
            ChunkId = kv.Value.Chunk.Id,
            DocumentId = kv.Value.Chunk.DocumentId,
            Text = kv.Value.Chunk.Text,
            Score = kv.Value.Score,
            DenseSimilarity = denseResults.FirstOrDefault(d => d.Id == kv.Key)?.Similarity ?? 0
        })
        .ToList();
}
```

RRF is simple but effective: chunks that appear in both rankings get boosted.

## Community Detection: Leiden Algorithm

Communities group densely connected entities. We use the Leiden algorithm (an improvement over Louvain) for this.

The implementation in `LeidenCommunityDetector.cs` is simplified but follows the core algorithm:

1. Build adjacency graph from relationships
2. Iteratively move nodes to communities that maximize modularity
3. Create hierarchy by treating communities as super-nodes

For production use, consider libraries like `igraph` or `NetworkX` (via Python interop).

## Pipeline Orchestration

`GraphRagPipeline.cs` ties everything together:

```csharp
public async Task IndexAsync(string markdownPath, IProgress<PipelineProgress>? progress = null,
    CancellationToken ct = default)
{
    // Phase 1: Index documents and create chunks with embeddings
    progress?.Report(new PipelineProgress(PipelinePhase.Indexing, 0, "Starting document indexing..."));
    
    var indexer = new MarkdownIndexer(_db, _embedder);
    await indexer.IndexDirectoryAsync(markdownPath, 
        new Progress<IndexProgress>(p => progress?.Report(
            new PipelineProgress(PipelinePhase.Indexing, p.Percentage, p.Message))), ct);

    // Phase 2: Extract entities using hybrid approach
    progress?.Report(new PipelineProgress(PipelinePhase.EntityExtraction, 0, "Extracting entities..."));
    
    var extractor = new HybridEntityExtractor(_db, _embedder, _llm);
    var stats = await extractor.ExtractEntitiesAsync(
        new Progress<ExtractionProgress>(p => progress?.Report(
            new PipelineProgress(PipelinePhase.EntityExtraction, p.Percentage, p.Message))), ct);

    // Phase 3: Detect communities using Leiden algorithm
    progress?.Report(new PipelineProgress(PipelinePhase.CommunityDetection, 0, "Detecting communities..."));
    
    var detector = new LeidenCommunityDetector(_db);
    var communities = await detector.DetectCommunitiesAsync(
        new Progress<string>(msg => progress?.Report(
            new PipelineProgress(PipelinePhase.CommunityDetection, 50, msg))));

    await detector.StoreCommunitiesAsync(communities);

    // Phase 4: Generate community summaries
    progress?.Report(new PipelineProgress(PipelinePhase.Summarization, 0, "Generating community summaries..."));
    
    var summarizer = new CommunitySummarizer(_db);
    await summarizer.SummarizeAllAsync(communities,
        new Progress<SummarizationProgress>(p => progress?.Report(
            new PipelineProgress(PipelinePhase.Summarization, p.Percentage, p.Message))), ct);

    // Initialize BM25 for search
    await _search.InitializeBM25Async();

    progress?.Report(new PipelineProgress(PipelinePhase.Complete, 100, "Indexing complete!"));
}
```

# Using the CLI

The project includes a Spectre.Console CLI:

## Indexing

```bash
# Index a directory of markdown files
dotnet run --project Mostlylucid.GraphRag -- index ./Markdown

# With custom model
dotnet run --project Mostlylucid.GraphRag -- index ./Markdown \
  --model llama3.2:3b \
  --database my-corpus.duckdb
```

Output:

```
GraphRAG Indexer
  Source: ./Markdown
  Database: graphrag.duckdb
  Model: llama3.2:3b

◴ Indexing documents          ████████████████████ 100%
◴ Extracting entities         ████████████████████ 100%
◴ Detecting communities       ████████████████████ 100%
◴ Generating summaries        ████████████████████ 100%

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Indexing Complete
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
┌────────────────┬────────┐
│ Metric         │ Count  │
├────────────────┼────────┤
│ Documents      │ 125    │
│ Chunks         │ 847    │
│ Entities       │ 342    │
│ Relationships  │ 1,284  │
│ Communities    │ 18     │
└────────────────┴────────┘
```

## Querying

### Local Search

```bash
dotnet run --project Mostlylucid.GraphRag -- query "How do I use HTMX with Alpine.js?"
```

Returns chunks + related entities.

### Global Search

```bash
dotnet run --project Mostlylucid.GraphRag -- query \
  "What are the main technology themes in this corpus?" \
  --mode global
```

Uses community summaries for corpus-level insight.

### DRIFT Search

```bash
dotnet run --project Mostlylucid.GraphRag -- query \
  "How do the frontend and backend technologies connect?" \
  --mode drift
```

Combines local results with community context.

# Cost Analysis

Let's compare this implementation to Microsoft's full GraphRAG for a corpus of 100 blog posts (500 chunks).

| Operation | Full GraphRAG | This Implementation |
|-----------|---------------|---------------------|
| **Entity extraction** | 2 LLM calls × 500 chunks = 1,000 calls | 0-1 calls (optional batch) |
| **Relationship extraction** | Included in above | BM25 co-occurrence (free) |
| **Community summaries** | 1 call × 18 communities = 18 calls | Same |
| **Total LLM calls (indexing)** | ~1,018 | ~18 |
| **Indexing cost (gpt-4o-mini)** | ~$5-10 | ~$0.05 |
| **Indexing cost (Ollama local)** | N/A | $0 |

The hybrid approach reduces indexing costs by **50-100x** while maintaining quality for technical content.

# Limitations and Tradeoffs

This isn't a replacement for full GraphRAG. Here's what we sacrifice:

1. **Entity quality**: Regex misses nuanced entities that LLMs would catch
2. **Relationship semantics**: Co-occurrence doesn't capture causality or directionality well
3. **Community stability**: Simplified Leiden may produce less stable clusters
4. **No provenance tracking**: We don't track which LLM calls produced which entities

**When to use this vs. full GraphRAG:**

| Use This | Use Full GraphRAG |
|----------|-------------------|
| Technical documentation | General knowledge bases |
| Cost-constrained environments | Research/prototyping |
| Local LLM deployment | Cloud API access |
| Primarily "how-to" queries | Heavy sensemaking workloads |

# Next Steps

This implementation handles the core GraphRAG pipeline. To make it production-ready:

1. **Incremental indexing**: Update graph when documents change
2. **Entity alias management**: Maintain canonical name mappings
3. **Relationship schema**: Constrain allowed relationship types
4. **Confidence scoring**: Track extraction quality metrics
5. **Query classification**: Automatically route to Local/Global/DRIFT

For most use cases, **BERT + BM25 hybrid search** (without the graph) is sufficient. Add GraphRAG when you have evidence that users ask corpus-level questions.

# Conclusion

We've built a minimum viable GraphRAG that:
- Uses DuckDB for unified storage (vectors + graph + metadata)
- Reduces LLM costs by 50-100x via hybrid extraction
- Provides Local/Global/DRIFT query modes
- Works entirely offline with Ollama

The key insight: **you don't need perfect entity extraction to get value from GraphRAG**. Heuristics + embeddings + BM25 handle 80% of cases, with optional LLM refinement for the remaining 20%.

**Code:** [`Mostlylucid.GraphRag/`](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.GraphRag)

**Next in series:** Part 3 will cover integrating this with the existing blog search and building a query classification system.

## Resources

- [DuckDB VSS Extension](https://duckdb.org/docs/extensions/vss.html) - Vector similarity search in DuckDB
- [Leiden Algorithm Paper](https://arxiv.org/pdf/1810.08473.pdf) - Community detection algorithm
- [BM25 Explained](https://www.elastic.co/blog/practical-bm25-part-2-the-bm25-algorithm-and-its-variables) - Classic IR scoring
- [Part 1: GraphRAG Fundamentals](/blog/graphrag-knowledge-graphs-for-rag) - Why knowledge graphs matter
