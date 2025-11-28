using Microsoft.Extensions.Logging;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Mostlylucid.SemanticSearch.Services;

/// <summary>
/// Qdrant-based vector store service for semantic search
/// </summary>
public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly ILogger<QdrantVectorStoreService> _logger;
    private readonly SemanticSearchConfig _config;
    private readonly QdrantClient? _client;
    private bool _collectionInitialized;

    public QdrantVectorStoreService(
        ILogger<QdrantVectorStoreService> logger,
        SemanticSearchConfig config)
    {
        _logger = logger;
        _config = config;

        if (!_config.Enabled)
        {
            _logger.LogInformation("Semantic search is disabled");
            return;
        }

        try
        {
            // Enable HTTP/2 unencrypted support for gRPC
            // This is required for Qdrant gRPC on Windows without TLS
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Parse the Qdrant URL
            var uri = new Uri(_config.QdrantUrl);
            var host = uri.Host;

            // Use the port from URL if specified, otherwise default to 6334 (gRPC)
            // If port is 6333 (HTTP), we need to use gRPC port 6334
            var port = uri.Port > 0 && uri.Port != 6333 ? uri.Port : 6334;

            // Create Qdrant client with API key
            // Use WriteApiKey for full access (read + write operations)
            // If only ReadApiKey is set, use that (read-only mode)
            var apiKey = !string.IsNullOrEmpty(_config.WriteApiKey)
                ? _config.WriteApiKey
                : _config.ReadApiKey;

            _client = new QdrantClient(host, port, https: uri.Scheme == "https", apiKey: apiKey);

            _logger.LogInformation("Connected to Qdrant at {Host}:{Port} (gRPC), API key: {HasKey}",
                host, port, !string.IsNullOrEmpty(apiKey) ? "configured" : "not set");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Qdrant at {Url}", _config.QdrantUrl);
        }
    }

    public async Task InitializeCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled || _collectionInitialized)
            return;

        try
        {
            // Check if collection exists
            var collections = await _client.ListCollectionsAsync(cancellationToken);
            var collectionExists = collections.Contains(_config.CollectionName);

            if (!collectionExists)
            {
                _logger.LogInformation("Creating collection {CollectionName}", _config.CollectionName);

                // Create collection with cosine similarity
                await _client.CreateCollectionAsync(
                    collectionName: _config.CollectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)_config.VectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Collection {CollectionName} created successfully", _config.CollectionName);
            }
            else
            {
                _logger.LogInformation("Collection {CollectionName} already exists", _config.CollectionName);
            }

            _collectionInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize collection {CollectionName}", _config.CollectionName);
            throw;
        }
    }

    public async Task IndexDocumentAsync(BlogPostDocument document, float[] embedding, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return;

        await InitializeCollectionAsync(cancellationToken);

        try
        {
            // Use deterministic ulong based on document ID to ensure upsert works correctly
            var pointId = GenerateDeterministicId(document.Id);

            var payload = new Dictionary<string, Value>
            {
                ["slug"] = document.Slug,
                ["title"] = document.Title,
                ["language"] = document.Language,
                ["categories"] = new Value
                {
                    ListValue = new ListValue
                    {
                        Values = { document.Categories.Select(c => new Value { StringValue = c }) }
                    }
                },
                ["published_date"] = document.PublishedDate.ToString("O"),
                ["content_hash"] = document.ContentHash ?? "",
                ["id"] = document.Id
            };

            var point = new PointStruct
            {
                Id = pointId,
                Vectors = embedding,
                Payload = { payload }
            };

            await _client.UpsertAsync(
                collectionName: _config.CollectionName,
                points: new[] { point },
                cancellationToken: cancellationToken
            );

            _logger.LogDebug("Indexed document {Id} ({Title})", document.Id, document.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {Id}", document.Id);
            throw;
        }
    }

    public async Task IndexDocumentsAsync(IEnumerable<(BlogPostDocument Document, float[] Embedding)> documents, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return;

        await InitializeCollectionAsync(cancellationToken);

        var documentsList = documents.ToList();
        if (documentsList.Count == 0)
            return;

        try
        {
            var points = documentsList.Select(doc =>
            {
                var payload = new Dictionary<string, Value>
                {
                    ["slug"] = doc.Document.Slug,
                    ["title"] = doc.Document.Title,
                    ["language"] = doc.Document.Language,
                    ["categories"] = new Value
                    {
                        ListValue = new ListValue
                        {
                            Values = { doc.Document.Categories.Select(c => new Value { StringValue = c }) }
                        }
                    },
                    ["published_date"] = doc.Document.PublishedDate.ToString("O"),
                    ["content_hash"] = doc.Document.ContentHash ?? "",
                    ["id"] = doc.Document.Id
                };

                return new PointStruct
                {
                    // Use deterministic ulong based on document ID to ensure upsert works correctly
                    Id = GenerateDeterministicId(doc.Document.Id),
                    Vectors = doc.Embedding,
                    Payload = { payload }
                };
            }).ToList();

            await _client.UpsertAsync(
                collectionName: _config.CollectionName,
                points: points,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Indexed {Count} documents", documentsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index {Count} documents", documentsList.Count);
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchAsync(float[] queryEmbedding, int limit = 10, float scoreThreshold = 0.5f, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return new List<SearchResult>();

        try
        {
            var searchResults = await _client.SearchAsync(
                collectionName: _config.CollectionName,
                vector: queryEmbedding,
                limit: (ulong)limit,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken
            );

            return searchResults.Select(result => new SearchResult
            {
                Slug = result.Payload["slug"].StringValue,
                Title = result.Payload["title"].StringValue,
                Language = result.Payload["language"].StringValue,
                Categories = result.Payload.TryGetValue("categories", out var cats)
                    ? cats.ListValue.Values.Select(v => v.StringValue).ToList()
                    : new List<string>(),
                Score = result.Score,
                PublishedDate = DateTime.Parse(result.Payload["published_date"].StringValue)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            return new List<SearchResult>();
        }
    }

    public async Task<List<SearchResult>> FindRelatedPostsAsync(string slug, string language, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return new List<SearchResult>();

        try
        {
            // Find the document by slug and language
            var scrollResults = await _client.ScrollAsync(
                collectionName: _config.CollectionName,
                filter: new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "slug",
                                Match = new Match { Keyword = slug }
                            }
                        },
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "language",
                                Match = new Match { Keyword = language }
                            }
                        }
                    }
                },
                limit: 1,
                cancellationToken: cancellationToken
            );

            var point = scrollResults.Result.FirstOrDefault();
            if (point == null)
            {
                _logger.LogWarning("Post {Slug} ({Language}) not found in vector store", slug, language);
                return new List<SearchResult>();
            }

            // Use the document's vector to find similar posts
            var searchResults = await _client.SearchAsync(
                collectionName: _config.CollectionName,
                vector: point.Vectors.Vector.Data.ToArray(),
                limit: (ulong)(limit + 1), // +1 because the first result will be the post itself
                scoreThreshold: _config.MinimumSimilarityScore,
                cancellationToken: cancellationToken
            );

            // Filter out the original post and return top N similar posts
            return searchResults
                .Where(r => r.Payload["slug"].StringValue != slug || r.Payload["language"].StringValue != language)
                .Take(limit)
                .Select(result => new SearchResult
                {
                    Slug = result.Payload["slug"].StringValue,
                    Title = result.Payload["title"].StringValue,
                    Language = result.Payload["language"].StringValue,
                    Categories = result.Payload.TryGetValue("categories", out var cats)
                        ? cats.ListValue.Values.Select(v => v.StringValue).ToList()
                        : new List<string>(),
                    Score = result.Score,
                    PublishedDate = DateTime.Parse(result.Payload["published_date"].StringValue)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find related posts for {Slug} ({Language})", slug, language);
            return new List<SearchResult>();
        }
    }

    public async Task DeleteDocumentAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return;

        try
        {
            await _client.DeleteAsync(
                collectionName: _config.CollectionName,
                filter: new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "id",
                                Match = new Match { Keyword = id }
                            }
                        }
                    }
                },
                cancellationToken: cancellationToken
            );

            _logger.LogDebug("Deleted document {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {Id}", id);
        }
    }

    public async Task<string?> GetDocumentHashAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_client == null || !_config.Enabled)
            return null;

        try
        {
            var scrollResults = await _client.ScrollAsync(
                collectionName: _config.CollectionName,
                filter: new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "id",
                                Match = new Match { Keyword = id }
                            }
                        }
                    }
                },
                limit: 1,
                cancellationToken: cancellationToken
            );

            var point = scrollResults.Result.FirstOrDefault();
            return point?.Payload.TryGetValue("content_hash", out var hash) == true
                ? hash.StringValue
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document hash for {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// Generate a deterministic ulong from a string ID using xxHash64.
    /// This ensures the same document ID always gets the same point ID for proper upsert behavior.
    /// </summary>
    private static ulong GenerateDeterministicId(string id)
    {
        return System.IO.Hashing.XxHash64.HashToUInt64(System.Text.Encoding.UTF8.GetBytes(id));
    }
}
