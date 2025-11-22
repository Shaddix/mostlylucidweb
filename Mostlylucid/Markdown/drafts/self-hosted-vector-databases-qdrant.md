# Self-Hosted Vector Databases with Qdrant
<!--category-- ASP.NET, Semantic Search, Vector Databases -->
<datetime class="hidden">2025-01-20T09:00</datetime>

## Introduction

Vector databases have become essential infrastructure for modern AI applications. Whether you're building semantic search, recommendation systems, RAG (Retrieval-Augmented Generation) pipelines, or similarity-based features, you need a way to efficiently store and query high-dimensional vectors. While managed services like Pinecone offer convenience, self-hosted options like Qdrant provide full control, zero vendor lock-in, and cost predictability.

In this article, I'll explore Qdrant—a high-performance, open-source vector database written in Rust—and walk through a real-world implementation used in this blog's semantic search feature. We'll cover what makes Qdrant different, how the C# client works, performance optimizations, and common gotchas you'll encounter when building production systems.

[TOC]

## What is Qdrant?

[Qdrant](https://qdrant.tech/) (pronounced "quadrant") is a vector similarity search engine built from the ground up in Rust. Unlike traditional databases that excel at exact matches and structured queries, vector databases like Qdrant specialize in finding similar items based on high-dimensional vector representations.

### Key Features

**Rust Performance**: Written in Rust, Qdrant delivers exceptional performance with low memory footprint and high throughput. It leverages SIMD (Single Instruction, Multiple Data) hardware acceleration on modern CPUs and uses async I/O with `io_uring` on Linux for maximum disk throughput.

**Advanced Filtering**: One of Qdrant's standout features is its ability to combine vector similarity search with complex metadata filtering. You can attach arbitrary JSON payloads to vectors and filter by keywords, numerical ranges, geo-locations, dates, and more—all before the similarity search runs.

**gRPC and REST APIs**: Qdrant provides both REST and gRPC interfaces. For production workloads, gRPC offers significantly better performance, especially for high-frequency operations.

**Self-Hostable**: Fully open-source (Apache 2.0 license) with no feature gates. You get the same capabilities whether you self-host or use Qdrant Cloud, making it easy to develop locally and deploy anywhere.

**Efficient Storage**: Uses HNSW (Hierarchical Navigable Small World) graphs for approximate nearest neighbor search, providing excellent recall with sub-linear query times.

## Why Choose Qdrant? Vector Database Comparison

Before diving into implementation, let's understand where Qdrant fits in the vector database landscape.

### Qdrant vs. The Competition

**Pinecone** - Managed-first, serverless approach
- **Pros**: Zero ops overhead, multi-region replication, excellent reliability
- **Cons**: Expensive at scale, vendor lock-in, limited self-hosting options
- **Best for**: Teams that want maximum convenience and have budget for managed services

**Weaviate** - Open-source with strong hybrid search
- **Pros**: Built-in vectorization modules, GraphQL API, knowledge graph features
- **Cons**: More complex architecture, higher resource requirements
- **Best for**: Applications needing hybrid semantic + graph queries

**Milvus** - Industrial-scale vector database
- **Pros**: Proven at billion-vector scale, extensive ecosystem, elastic scalability
- **Cons**: Complex cluster management, heavy resource footprint, steep learning curve
- **Best for**: Large enterprises with dedicated data engineering teams

**ChromaDB** - Developer-friendly lightweight option
- **Pros**: Simple API, easy to get started, good for prototyping
- **Cons**: Limited scalability, fewer production features, smaller community
- **Best for**: Quick prototypes and small to medium applications

### Why I Chose Qdrant for This Blog

For the semantic search feature on this blog, I needed:

1. **Self-hosting capability** - Full control over data and costs
2. **Excellent filtering** - Search by language, category, date ranges
3. **Moderate scale** - Hundreds to thousands of blog posts, not billions
4. **Low resource footprint** - Running on modest VPS hardware
5. **C# support** - First-class .NET client library

Qdrant checked all these boxes. The Rust implementation means it runs efficiently on a single server, the filtering capabilities allow sophisticated queries, and the C# client is well-maintained by the Qdrant team.

## How Qdrant Works: Core Concepts

### Collections

In Qdrant, a **collection** is similar to a table in a traditional database. Each collection has:
- A fixed vector dimensionality (e.g., 384, 768, 1536)
- A distance metric (Cosine, Dot Product, or Euclidean)
- An index configuration (HNSW parameters)

```csharp
await client.CreateCollectionAsync(
    collectionName: "blog_posts",
    vectorsConfig: new VectorParams
    {
        Size = 384,              // Vector dimensions
        Distance = Distance.Cosine  // Similarity metric
    }
);
```

### Points

A **point** is a single record in a collection, consisting of:
- **ID**: Unique identifier (UUID or unsigned integer)
- **Vector**: The embedding (float array)
- **Payload**: Arbitrary JSON metadata for filtering and display

```csharp
var point = new PointStruct
{
    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
    Vectors = embedding,  // float[] of size 384
    Payload =
    {
        ["slug"] = "my-blog-post",
        ["title"] = "Vector Databases Explained",
        ["language"] = "en",
        ["categories"] = new[] { "AI", "Databases" },
        ["published_date"] = DateTime.UtcNow,
        ["content_hash"] = "sha256:abc123..."
    }
};

await client.UpsertAsync(collectionName: "blog_posts", points: new[] { point });
```

### Distance Metrics

Qdrant supports three similarity metrics:

**Cosine Distance** (0 to 2, lower is more similar)
- Measures angle between vectors, ignoring magnitude
- Best for: Text embeddings where direction matters more than length
- Formula: `1 - (A · B) / (||A|| × ||B||)`

**Dot Product** (higher is more similar)
- Raw inner product of vectors
- Best for: Pre-normalized vectors or when magnitude is meaningful
- Formula: `A · B`

**Euclidean Distance** (lower is more similar)
- Geometric distance in n-dimensional space
- Best for: Spatial data, color similarity
- Formula: `||A - B||`

For text embeddings from models like `all-MiniLM-L6-v2`, **Cosine** is the standard choice.

### Filtering Before Search

This is where Qdrant shines. Instead of searching all vectors and then filtering results (slow), Qdrant filters first, then searches only the qualifying subset.

```csharp
var searchParams = new SearchParams
{
    HnswEf = 128,  // Search accuracy parameter
    Exact = false   // Use approximate search (faster)
};

var filter = new Filter
{
    Must =
    {
        // Only English posts
        new Condition
        {
            Field = new FieldCondition
            {
                Key = "language",
                Match = new Match { Keyword = "en" }
            }
        },
        // Published in 2024 or later
        new Condition
        {
            Field = new FieldCondition
            {
                Key = "published_date",
                Range = new Range
                {
                    Gte = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()
                }
            }
        }
    }
};

var results = await client.SearchAsync(
    collectionName: "blog_posts",
    vector: queryEmbedding,
    limit: 10,
    filter: filter,
    searchParams: searchParams
);
```

## The C# Client: Qdrant.Client

Qdrant provides an official .NET client that supports both REST and gRPC protocols.

### Installation

```bash
dotnet add package Qdrant.Client
```

Current version: **1.14.0** (as of this writing)

### Basic Setup

```csharp
using Qdrant.Client;
using Qdrant.Client.Grpc;

// gRPC client (recommended for production)
var client = new QdrantClient(
    host: "localhost",
    port: 6334,  // gRPC port (6333 for REST)
    https: false
);

// With API key authentication
var client = new QdrantClient(
    host: "cloud.qdrant.io",
    port: 6334,
    https: true,
    apiKey: "your-api-key"
);
```

### gRPC vs REST

For production systems, **always use gRPC** (port 6334) instead of REST (port 6333):

**gRPC Advantages**:
- 3-5x faster for batch operations
- Binary protocol with smaller payloads
- Built-in streaming for large result sets
- Better error handling with typed responses

**When to Use REST**:
- Quick debugging with curl
- Exploring the Qdrant web UI
- Environments where gRPC is blocked (some corporate proxies)

### Windows gRPC Gotcha

On Windows, you may encounter HTTP/2 connection issues with gRPC. The solution is to enable unencrypted HTTP/2:

```csharp
// At application startup (before creating QdrantClient)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

This is already handled in the semantic search implementation at `/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/Services/QdrantVectorStoreService.cs:51`.

## Case Study: Blog Semantic Search Implementation

Let's walk through the real implementation used on this blog, which provides semantic search across hundreds of multilingual blog posts.

### Architecture Overview

```
┌─────────────────┐
│   Blog Post     │
│   (Markdown)    │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│     MarkdownRenderingService            │
│  (Extract title, content, metadata)     │
└────────┬────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│      BlogPostDocument Model             │
│  (slug, title, content, language, etc)  │
└────────┬────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│     OnnxEmbeddingService                │
│  (Text → 384-dim vector using ONNX)     │
└────────┬────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│     QdrantVectorStoreService            │
│  (Store vectors + metadata in Qdrant)   │
└─────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│         Qdrant Database                 │
│  Collection: "blog_posts" (Cosine, 384) │
└─────────────────────────────────────────┘

Search Flow:
User Query → Embedding → Vector Search → Results
```

### Component Breakdown

#### 1. Embedding Generation: OnnxEmbeddingService

Instead of calling external APIs (OpenAI, Cohere), this implementation uses **CPU-based embeddings** with ONNX Runtime. The model is `all-MiniLM-L6-v2`, which produces 384-dimensional vectors.

**Key Features**:
- Fully offline, no API costs
- Fast on CPU (50-100ms per document)
- WordPiece tokenization with 30K vocabulary
- L2 normalization for cosine similarity

**Implementation** (`/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/Services/OnnxEmbeddingService.cs`):

```csharp
public class OnnxEmbeddingService : IEmbeddingService
{
    private InferenceSession? _session;
    private WordPieceTokenizer? _tokenizer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task InitializeAsync()
    {
        var modelPath = Path.Combine(_config.EmbeddingModelPath);
        _session = new InferenceSession(modelPath);

        var vocabPath = Path.Combine(_config.VocabPath);
        var vocab = await LoadVocabularyAsync(vocabPath);
        _tokenizer = new WordPieceTokenizer(vocab);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Tokenize text → [CLS] tokens [SEP] [PAD]...
            var tokens = _tokenizer.Tokenize(text);

            // Create ONNX input tensors
            var inputIds = CreateInputTensor(tokens);
            var attentionMask = CreateAttentionMask(tokens);
            var tokenTypeIds = CreateTokenTypeIds(tokens);

            // Run inference
            using var results = _session.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
            });

            var embedding = results.First().AsTensor<float>().ToArray();

            // L2 normalize for cosine similarity
            return NormalizeVector(embedding);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private float[] NormalizeVector(float[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / (float)magnitude).ToArray();
    }
}
```

**Model Download** (`/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/download-models.sh`):

```bash
#!/bin/bash
mkdir -p ../Mostlylucid/models

# Download all-MiniLM-L6-v2 ONNX model (~33MB)
wget -O ../Mostlylucid/models/all-MiniLM-L6-v2.onnx \
  https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx

# Download vocabulary (~232KB)
wget -O ../Mostlylucid/models/vocab.txt \
  https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt
```

#### 2. Vector Storage: QdrantVectorStoreService

This service handles all Qdrant operations using the gRPC client.

**Key Methods** (`/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/Services/QdrantVectorStoreService.cs`):

```csharp
public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly SemanticSearchConfig _config;

    public QdrantVectorStoreService(IOptions<SemanticSearchConfig> config)
    {
        _config = config.Value;

        // Enable HTTP/2 for Windows compatibility
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var uri = new Uri(_config.QdrantUrl);
        var port = uri.Port > 0 ? uri.Port : 6334;  // Default gRPC port

        _client = new QdrantClient(
            host: uri.Host,
            port: port,
            https: uri.Scheme == "https",
            apiKey: _config.WriteApiKey
        );
    }

    public async Task InitializeAsync()
    {
        // Check if collection exists
        var collections = await _client.ListCollectionsAsync();
        if (collections.Any(c => c.Name == _config.CollectionName))
        {
            _logger.LogInformation("Collection {CollectionName} already exists", _config.CollectionName);
            return;
        }

        // Create collection with cosine similarity
        await _client.CreateCollectionAsync(
            collectionName: _config.CollectionName,
            vectorsConfig: new VectorParams
            {
                Size = (ulong)_config.VectorSize,
                Distance = Distance.Cosine
            }
        );

        _logger.LogInformation("Created collection {CollectionName}", _config.CollectionName);
    }

    public async Task IndexDocumentAsync(BlogPostDocument document, float[] embedding)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = document.Id.ToString() },
            Vectors = embedding,
            Payload =
            {
                ["slug"] = document.Slug,
                ["title"] = document.Title,
                ["language"] = document.Language,
                ["categories"] = document.Categories.ToArray(),
                ["published_date"] = new DateTimeOffset(document.PublishedDate).ToUnixTimeSeconds(),
                ["content_hash"] = document.ContentHash,
                ["document_id"] = document.Id.ToString()
            }
        };

        await _client.UpsertAsync(
            collectionName: _config.CollectionName,
            points: new[] { point }
        );
    }

    public async Task<List<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int limit,
        string? language = null,
        float minScore = 0.5f)
    {
        var filter = new Filter();

        if (!string.IsNullOrEmpty(language))
        {
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "language",
                    Match = new Match { Keyword = language }
                }
            });
        }

        var results = await _client.SearchAsync(
            collectionName: _config.CollectionName,
            vector: queryEmbedding,
            limit: (ulong)limit,
            filter: filter,
            scoreThreshold: minScore,
            withPayload: true
        );

        return results.Select(r => new SearchResult
        {
            Slug = r.Payload["slug"].StringValue,
            Title = r.Payload["title"].StringValue,
            Language = r.Payload["language"].StringValue,
            Score = r.Score,
            Categories = r.Payload["categories"].ListValue.Values
                .Select(v => v.StringValue).ToList()
        }).ToList();
    }
}
```

#### 3. Orchestration: SemanticSearchService

This service coordinates embedding generation, indexing, and searching.

**Content Hash-Based Incremental Updates**:

```csharp
public async Task IndexPostAsync(BlogPostDocument document)
{
    // Compute content hash
    var contentToHash = $"{document.Title}|{document.Content}";
    document.ContentHash = ComputeSHA256Hash(contentToHash);

    // Check if document already indexed with same content
    var existingPoints = await _vectorStore.GetPointsByFilterAsync(new Filter
    {
        Must =
        {
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "slug",
                    Match = new Match { Keyword = document.Slug }
                }
            },
            new Condition
            {
                Field = new FieldCondition
                {
                    Key = "language",
                    Match = new Match { Keyword = document.Language }
                }
            }
        }
    });

    if (existingPoints.Any())
    {
        var existingHash = existingPoints.First().Payload["content_hash"].StringValue;
        if (existingHash == document.ContentHash)
        {
            _logger.LogInformation("Document {Slug} ({Language}) unchanged, skipping",
                document.Slug, document.Language);
            return;
        }
    }

    // Content changed, generate new embedding
    var text = PrepareTextForEmbedding(document);
    var embedding = await _embeddingService.GenerateEmbeddingAsync(text);

    await _vectorStore.IndexDocumentAsync(document, embedding);
}

private string PrepareTextForEmbedding(BlogPostDocument document)
{
    // Weight title 2x by repeating it
    var text = $"{document.Title}. {document.Title}. {document.Content}";

    // Truncate to model's max token limit (~2000 chars ≈ 512 tokens)
    const int maxLength = 2000;
    return text.Length > maxLength ? text[..maxLength] : text;
}
```

#### 4. Hybrid Search with Reciprocal Rank Fusion

For even better results, the implementation combines semantic search with traditional full-text search using **Reciprocal Rank Fusion (RRF)**.

**How RRF Works** (`/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/Services/HybridSearchService.cs`):

```csharp
public async Task<List<SearchResult>> HybridSearchAsync(
    string query,
    int limit,
    string? language = null)
{
    // Run both searches in parallel
    var semanticTask = _semanticSearch.SearchAsync(query, limit * 2, language);
    var fullTextTask = _fullTextSearch.SearchAsync(query, limit * 2, language);

    await Task.WhenAll(semanticTask, fullTextTask);

    var semanticResults = await semanticTask;
    var fullTextResults = await fullTextTask;

    // Apply Reciprocal Rank Fusion
    var fusedScores = new Dictionary<string, double>();
    const int k = 60;  // RRF constant

    // Score semantic results
    for (int i = 0; i < semanticResults.Count; i++)
    {
        var key = $"{semanticResults[i].Slug}_{semanticResults[i].Language}";
        fusedScores[key] = 1.0 / (k + i + 1);
    }

    // Add full-text results
    for (int i = 0; i < fullTextResults.Count; i++)
    {
        var key = $"{fullTextResults[i].Slug}_{fullTextResults[i].Language}";
        if (fusedScores.ContainsKey(key))
            fusedScores[key] += 1.0 / (k + i + 1);
        else
            fusedScores[key] = 1.0 / (k + i + 1);
    }

    // Sort by fused score and return top results
    return fusedScores
        .OrderByDescending(kvp => kvp.Value)
        .Take(limit)
        .Select(kvp =>
        {
            var parts = kvp.Key.Split('_');
            var result = semanticResults.FirstOrDefault(r =>
                r.Slug == parts[0] && r.Language == parts[1])
                ?? fullTextResults.First(r =>
                    r.Slug == parts[0] && r.Language == parts[1]);
            result.Score = (float)kvp.Value;
            return result;
        })
        .ToList();
}
```

### Configuration

**appsettings.json**:

```json
{
  "SemanticSearch": {
    "Enabled": false,
    "QdrantUrl": "http://localhost:6334",
    "ReadApiKey": "",
    "WriteApiKey": "",
    "CollectionName": "blog_posts",
    "EmbeddingModelPath": "models/all-MiniLM-L6-v2.onnx",
    "VocabPath": "models/vocab.txt",
    "VectorSize": 384,
    "RelatedPostsCount": 5,
    "MinimumSimilarityScore": 0.5,
    "SearchResultsCount": 10
  }
}
```

### Docker Deployment

**semantic-search-docker-compose.yml**:

```yaml
version: '3.8'

services:
  qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant
    ports:
      - "6333:6333"  # REST API
      - "6334:6334"  # gRPC API
    volumes:
      - qdrant_storage:/qdrant/storage
    environment:
      - QDRANT__SERVICE__HTTP_PORT=6333
      - QDRANT__SERVICE__GRPC_PORT=6334
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

volumes:
  qdrant_storage:
    driver: local
```

Start with:

```bash
docker-compose -f semantic-search-docker-compose.yml up -d
```

### API Endpoints

**SearchController** (`/home/user/mostlylucidweb/Mostlylucid/Controllers/SearchController.cs`):

```csharp
[ApiController]
[Route("search")]
public class SearchController : BaseController
{
    [HttpGet("semantic")]
    [OutputCache(Duration = 3600)]
    public async Task<IActionResult> SemanticSearch(
        [FromQuery] string? query,
        [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query is required");

        var results = await _semanticSearch.SearchAsync(query, limit);

        // Support HTMX partial views
        if (Request.IsHtmx())
            return PartialView("_SemanticSearchResults", results);

        return Json(results);
    }

    [HttpGet("related/{slug}/{language}")]
    [OutputCache(Duration = 7200)]
    public async Task<IActionResult> RelatedPosts(
        string slug,
        string language,
        [FromQuery] int limit = 5)
    {
        var results = await _semanticSearch.GetRelatedPostsAsync(slug, language, limit);

        if (Request.IsHtmx())
            return PartialView("_RelatedPosts", results);

        return Json(results);
    }
}
```

**Usage Examples**:

```bash
# Semantic search
curl "http://localhost:5000/search/semantic?query=vector%20databases&limit=5"

# Related posts
curl "http://localhost:5000/search/related/self-hosted-vector-databases-qdrant/en?limit=3"
```

## Performance Optimizations

### 1. Batch Indexing

When indexing many documents, use batch operations:

```csharp
public async Task IndexPostsBatchAsync(List<BlogPostDocument> documents)
{
    var points = new List<PointStruct>();

    foreach (var doc in documents)
    {
        var text = PrepareTextForEmbedding(doc);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(text);

        points.Add(new PointStruct
        {
            Id = new PointId { Uuid = doc.Id.ToString() },
            Vectors = embedding,
            Payload = CreatePayload(doc)
        });
    }

    // Single batch upsert instead of N individual calls
    await _client.UpsertAsync(
        collectionName: _config.CollectionName,
        points: points,
        wait: true  // Wait for indexing to complete
    );
}
```

**Performance gain**: 5-10x faster than individual upserts.

### 2. HNSW Index Tuning

Qdrant uses HNSW (Hierarchical Navigable Small World) graphs. Key parameters:

```csharp
var hnswConfig = new HnswConfigDiff
{
    M = 16,              // Number of edges per node (higher = better recall, more memory)
    EfConstruct = 100,   // Construction time accuracy (higher = better quality, slower build)
    FullScanThreshold = 10000  // Use brute force for small collections
};

await _client.UpdateCollectionAsync(
    collectionName: _config.CollectionName,
    optimizersConfig: new OptimizersConfigDiff
    {
        IndexingThreshold = 10000  // Start indexing after N points
    },
    hnswConfig: hnswConfig
);
```

**Search-time parameter**:

```csharp
var searchParams = new SearchParams
{
    HnswEf = 128,  // Higher = better recall, slower search (range: 32-512)
    Exact = false   // Set true for exact brute-force search
};
```

**Tuning guidance**:
- `M = 16-32`: Sweet spot for most use cases
- `EfConstruct = 100-200`: Balance between build time and quality
- `HnswEf = 64-128`: Good recall with low latency (<10ms)
- For >95% recall at scale, use `HnswEf = 256`

### 3. Payload Index Optimization

For frequently filtered fields, create payload indexes:

```csharp
await _client.CreatePayloadIndexAsync(
    collectionName: _config.CollectionName,
    fieldName: "language",
    schemaType: PayloadSchemaType.Keyword  // Fast exact matching
);

await _client.CreatePayloadIndexAsync(
    collectionName: _config.CollectionName,
    fieldName: "published_date",
    schemaType: PayloadSchemaType.Integer  // Fast range queries
);
```

**Performance impact**: 10-100x faster filtering on large collections.

### 4. Connection Pooling

The C# client uses gRPC channels internally. For high-throughput scenarios, reuse the same `QdrantClient` instance (registered as singleton in DI):

```csharp
// In Program.cs
services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();
```

Avoid creating new clients per request—gRPC channel creation is expensive.

### 5. Quantization for Large Collections

Qdrant supports scalar and product quantization to reduce memory usage:

```csharp
await _client.UpdateCollectionAsync(
    collectionName: _config.CollectionName,
    quantizationConfig: new ScalarQuantization
    {
        Scalar = new ScalarQuantizationConfig
        {
            Type = ScalarType.Int8,  // Reduce from float32 to int8
            Quantile = 0.99f,
            AlwaysRam = true
        }
    }
);
```

**Tradeoff**: 4x less memory, ~2% recall loss, 1.5x faster search.

## Common Gotchas and Solutions

### 1. Port Confusion

**Problem**: Connection refused when trying to connect to Qdrant.

**Solution**: Qdrant exposes two ports:
- **6333**: REST API
- **6334**: gRPC API

The C# client uses gRPC by default. Make sure you're connecting to port **6334**, not 6333.

```csharp
// Correct
var client = new QdrantClient(host: "localhost", port: 6334);

// Wrong (will fail)
var client = new QdrantClient(host: "localhost", port: 6333);
```

### 2. Windows HTTP/2 Issue

**Problem**: `System.Net.Http.HttpRequestException: The SSL connection could not be established`

**Solution**: Enable unencrypted HTTP/2 support:

```csharp
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

Place this **before** creating the `QdrantClient`.

### 3. Vector Dimension Mismatch

**Problem**: `StatusCode="InvalidArgument" Detail="Wrong input: Vector dimension error: expected dim: 384, got 768"`

**Solution**: Ensure your embedding model's output dimension matches the collection's `VectorSize`:

```csharp
// Model produces 384-dim vectors
var embedding = await embeddingService.GenerateEmbeddingAsync(text);  // float[384]

// Collection must match
await client.CreateCollectionAsync(
    collectionName: "blog_posts",
    vectorsConfig: new VectorParams { Size = 384 }  // Must be 384, not 768
);
```

Common embedding dimensions:
- `all-MiniLM-L6-v2`: 384
- `nomic-embed-text`: 768
- `text-embedding-ada-002` (OpenAI): 1536
- `text-embedding-3-small` (OpenAI): 1536
- `text-embedding-3-large` (OpenAI): 3072

### 4. Payload Type Errors

**Problem**: Payload values must match expected types.

**Solution**: Use correct value constructors:

```csharp
// Strings
Payload = { ["title"] = "My Title" }

// Arrays of strings
Payload = { ["categories"] = new[] { "AI", "ML" } }

// Numbers
Payload = { ["year"] = 2024 }

// Booleans
Payload = { ["published"] = true }

// Dates (store as Unix timestamp)
Payload = { ["date"] = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() }
```

### 5. Memory Usage with Large Batches

**Problem**: Out of memory when indexing thousands of documents.

**Solution**: Process in smaller batches:

```csharp
const int batchSize = 100;
for (int i = 0; i < documents.Count; i += batchSize)
{
    var batch = documents.Skip(i).Take(batchSize).ToList();
    await IndexPostsBatchAsync(batch);

    // Optional: force GC between batches
    GC.Collect();
    GC.WaitForPendingFinalizers();
}
```

### 6. Slow Initial Searches

**Problem**: First search after startup takes 2-3 seconds.

**Cause**: HNSW index lazy-loads into memory on first access.

**Solution**: Warm up the index after initialization:

```csharp
public async Task WarmUpAsync()
{
    // Dummy search to load index into memory
    var dummyVector = new float[_config.VectorSize];
    await _client.SearchAsync(
        collectionName: _config.CollectionName,
        vector: dummyVector,
        limit: 1
    );
}
```

### 7. Filtering by Arrays

**Problem**: Need to match documents with any of several categories.

**Solution**: Use `MatchAny` for array fields:

```csharp
var filter = new Filter
{
    Must =
    {
        new Condition
        {
            Field = new FieldCondition
            {
                Key = "categories",
                Match = new Match
                {
                    Any = new RepeatedField<string> { "AI", "Machine Learning" }
                }
            }
        }
    }
};
```

This returns documents where `categories` contains **any** of the specified values.

## Testing

The implementation includes comprehensive tests using xUnit and Moq.

**Example: Testing Embedding Service** (`/home/user/mostlylucidweb/Mostlylucid.SemanticSearch.Test/OnnxEmbeddingServiceTests.cs`):

```csharp
public class OnnxEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbedding_ReturnsNormalizedVector()
    {
        var config = Options.Create(new SemanticSearchConfig
        {
            EmbeddingModelPath = "models/all-MiniLM-L6-v2.onnx",
            VocabPath = "models/vocab.txt",
            VectorSize = 384
        });

        var service = new OnnxEmbeddingService(config, Mock.Of<ILogger<OnnxEmbeddingService>>());
        await service.InitializeAsync();

        var embedding = await service.GenerateEmbeddingAsync("vector database");

        Assert.Equal(384, embedding.Length);

        // Verify L2 normalization (magnitude ≈ 1.0)
        var magnitude = Math.Sqrt(embedding.Sum(v => v * v));
        Assert.InRange(magnitude, 0.99, 1.01);
    }
}
```

**Example: Testing Search Logic**:

```csharp
[Fact]
public async Task SearchAsync_FiltersbyLanguage()
{
    var embeddingServiceMock = new Mock<IEmbeddingService>();
    embeddingServiceMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
        .ReturnsAsync(new float[384]);

    var vectorStoreMock = new Mock<IVectorStoreService>();
    vectorStoreMock.Setup(x => x.SearchAsync(
        It.IsAny<float[]>(),
        It.IsAny<int>(),
        "en",  // Only English
        It.IsAny<float>()
    )).ReturnsAsync(new List<SearchResult>
    {
        new() { Slug = "post-1", Language = "en", Score = 0.95f }
    });

    var service = new SemanticSearchService(
        embeddingServiceMock.Object,
        vectorStoreMock.Object,
        Mock.Of<ILogger<SemanticSearchService>>()
    );

    var results = await service.SearchAsync("test query", limit: 10, language: "en");

    Assert.Single(results);
    Assert.Equal("en", results[0].Language);
}
```

## Production Checklist

Before deploying to production:

- [ ] **Enable authentication**: Set API keys in Qdrant config
- [ ] **Persistent storage**: Mount volume for `/qdrant/storage`
- [ ] **Resource limits**: Set Docker memory limits (2GB+ recommended)
- [ ] **Backup strategy**: Regular snapshots of Qdrant data directory
- [ ] **Monitoring**: Expose Qdrant metrics endpoint for Prometheus
- [ ] **Index optimization**: Create payload indexes for filtered fields
- [ ] **Health checks**: Implement `/health` endpoint checking Qdrant connectivity
- [ ] **Embedding caching**: Cache embeddings by content hash to avoid regeneration
- [ ] **Rate limiting**: Protect search endpoints from abuse
- [ ] **Logging**: Structured logging for all Qdrant operations
- [ ] **Version pinning**: Use specific Qdrant Docker image tags, not `:latest`

## Monitoring and Maintenance

### Qdrant Metrics

Qdrant exposes Prometheus metrics at `http://localhost:6333/metrics`:

```
# Collections
qdrant_collections_total
qdrant_collections_vector_count

# Performance
qdrant_rest_responses_total
qdrant_rest_responses_duration_seconds
qdrant_grpc_responses_total

# Resources
qdrant_memory_usage_bytes
qdrant_disk_usage_bytes
```

### Useful Management Queries

**Check collection info**:

```bash
curl http://localhost:6333/collections/blog_posts
```

**Get collection statistics**:

```bash
curl http://localhost:6333/collections/blog_posts/cluster
```

**Create snapshot (backup)**:

```bash
curl -X POST http://localhost:6333/collections/blog_posts/snapshots
```

**List snapshots**:

```bash
curl http://localhost:6333/collections/blog_posts/snapshots
```

## Cost Analysis: Self-Hosted vs. Managed

Let's compare costs for a medium-scale blog (10,000 posts, 384-dim vectors):

**Qdrant Cloud** (managed):
- 1GB RAM, 10GB storage: ~$25/month
- 4GB RAM, 40GB storage: ~$95/month

**Self-Hosted** (VPS):
- Hetzner CX21 (2 vCPU, 4GB RAM, 40GB SSD): €5.39/month (~$6)
- DigitalOcean Droplet (2 vCPU, 4GB RAM, 80GB SSD): $24/month
- Linode Shared (2 vCPU, 4GB RAM, 80GB SSD): $24/month

**Savings**: 75-80% with self-hosting for moderate scale.

**Break-even point**: If you need >8GB RAM or dedicated support, managed services become more attractive.

## Alternative: Sample with Ollama

The repository includes an alternative sample using Ollama for embeddings instead of ONNX.

**Location**: `/home/user/mostlylucidweb/samples/QdrantMarkdownSearch/`

**Key differences**:
- Uses `nomic-embed-text` model (768 dimensions vs. 384)
- Embeddings generated via HTTP API instead of local ONNX
- Requires Ollama service running (`docker-compose up ollama`)

**When to use Ollama**:
- You want to experiment with different embedding models easily
- You're already running Ollama for LLM inference
- You prefer API-based workflows over local model files

**When to use ONNX (as in main implementation)**:
- Fully offline/air-gapped environments
- Lower latency (no HTTP overhead)
- Smaller resource footprint (no separate service)
- Consistent deterministic results

## Conclusion

Qdrant provides a compelling self-hosted vector database solution with enterprise-grade features, excellent performance, and zero licensing costs. The Rust implementation delivers speed and reliability, while the sophisticated filtering capabilities set it apart from simpler alternatives like ChromaDB.

For this blog's semantic search, Qdrant handles hundreds of multilingual posts with sub-10ms query times on modest hardware. The CPU-based ONNX embeddings eliminate API costs, and the content-hash-based incremental indexing ensures efficient updates.

Key takeaways:

1. **Choose Qdrant if**: You want self-hosting, need advanced filtering, or have cost constraints
2. **Use gRPC**: Always prefer gRPC over REST for production (3-5x faster)
3. **Tune HNSW**: Start with `M=16`, `EfConstruct=100`, `HnswEf=128`
4. **Index payloads**: Create indexes for frequently filtered fields
5. **Batch operations**: Use batch upserts for bulk indexing
6. **Monitor resources**: Qdrant is efficient, but plan for 2-4GB RAM for moderate collections
7. **Test thoroughly**: Use the patterns in the test suite to validate your implementation

The complete source code for this implementation is available in this repository. Whether you're building RAG applications, recommendation engines, or semantic search features, Qdrant provides the foundation for high-quality vector similarity search without the complexity or cost of managed alternatives.

## Resources

- **Qdrant Documentation**: https://qdrant.tech/documentation/
- **C# Client GitHub**: https://github.com/qdrant/qdrant-dotnet
- **HNSW Paper**: https://arxiv.org/abs/1603.09320
- **all-MiniLM-L6-v2 Model**: https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2
- **This Blog's Implementation**: `/home/user/mostlylucidweb/Mostlylucid.SemanticSearch/`

Happy vector searching!
