using Qdrant.Client;
using Qdrant.Client.Grpc;
using QdrantMarkdownSearch.Models;

namespace QdrantMarkdownSearch.Services;

/// <summary>
/// Implementation of vector search using Qdrant
/// This handles all interactions with the Qdrant database
/// </summary>
public class QdrantVectorSearchService : IVectorSearchService
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<QdrantVectorSearchService> _logger;
    private readonly string _collectionName;
    private readonly int _vectorSize;

    public QdrantVectorSearchService(
        QdrantClient client,
        IEmbeddingService embeddingService,
        IConfiguration configuration,
        ILogger<QdrantVectorSearchService> logger)
    {
        _client = client;
        _embeddingService = embeddingService;
        _logger = logger;
        _collectionName = configuration["Qdrant:CollectionName"] ?? "markdown_docs";
        _vectorSize = int.Parse(configuration["Qdrant:VectorSize"] ?? "768");
    }

    public async Task InitializeCollectionAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if collection exists
            var collections = await _client.ListCollectionsAsync(ct);

            if (collections.Any(c => c.Name == _collectionName))
            {
                _logger.LogInformation("Collection '{CollectionName}' already exists", _collectionName);
                return;
            }

            _logger.LogInformation("Creating collection '{CollectionName}' with vector size {VectorSize}",
                _collectionName, _vectorSize);

            // Create collection with cosine distance for normalized vectors
            await _client.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_vectorSize,
                    Distance = Distance.Cosine // Cosine similarity works well for semantic search
                },
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully created collection '{CollectionName}'", _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize collection '{CollectionName}'", _collectionName);
            throw;
        }
    }

    public async Task<bool> IndexDocumentAsync(
        string id,
        string fileName,
        string title,
        string content,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Indexing document: {FileName}", fileName);

            // Generate embedding for the content
            var embedding = await _embeddingService.GenerateEmbeddingAsync(content, ct);

            // Create the point with metadata
            var point = new PointStruct
            {
                Id = new PointId { Uuid = id },
                Vectors = embedding,
                Payload =
                {
                    ["fileName"] = fileName,
                    ["title"] = title,
                    ["content"] = content,
                    ["indexed_at"] = DateTime.UtcNow.ToString("O")
                }
            };

            // Upsert to Qdrant (insert or update)
            await _client.UpsertAsync(
                collectionName: _collectionName,
                points: new[] { point },
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully indexed document: {FileName}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document: {FileName}", fileName);
            return false;
        }
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Searching for: {Query}", query);

            // Generate embedding for the search query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, ct);

            // Search for similar vectors
            var searchResult = await _client.SearchAsync(
                collectionName: _collectionName,
                vector: queryEmbedding,
                limit: (ulong)limit,
                cancellationToken: ct
            );

            // Map results to our SearchResult model
            var results = searchResult.Select(hit => new SearchResult
            {
                Id = hit.Id.ToString(),
                Score = hit.Score,
                FileName = hit.Payload["fileName"].StringValue,
                Title = hit.Payload["title"].StringValue,
                Content = hit.Payload["content"].StringValue
            }).ToList();

            _logger.LogInformation("Found {Count} results for query: {Query}", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            return new List<SearchResult>();
        }
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default)
    {
        try
        {
            var info = await _client.GetCollectionInfoAsync(_collectionName, ct);
            return (int)info.PointsCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document count");
            return 0;
        }
    }
}
