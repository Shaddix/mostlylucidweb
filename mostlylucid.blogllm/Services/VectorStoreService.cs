using Qdrant.Client;
using Qdrant.Client.Grpc;
using Mostlylucid.BlogLLM.Models;

namespace Mostlylucid.BlogLLM.Services;

public class VectorStoreService
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;

    public VectorStoreService(string host, int port, string collectionName, string? apiKey = null)
    {
        _client = new QdrantClient(host, port, https: false, apiKey: apiKey);
        _collectionName = collectionName;
    }

    public async Task<bool> CollectionExistsAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            return collections.Any(c => c == _collectionName);
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateCollectionAsync(ulong vectorSize)
    {
        var collections = await _client.ListCollectionsAsync();

        if (collections.Any(c => c == _collectionName))
        {
            Console.WriteLine($"Collection '{_collectionName}' already exists");
            return;
        }

        await _client.CreateCollectionAsync(
            collectionName: _collectionName,
            vectorsConfig: new VectorParams
            {
                Size = vectorSize,
                Distance = Distance.Cosine
            }
        );

        Console.WriteLine($"Created collection '{_collectionName}' with {vectorSize} dimensions");
    }

    public async Task DeleteCollectionAsync()
    {
        await _client.DeleteCollectionAsync(_collectionName);
        Console.WriteLine($"Deleted collection '{_collectionName}'");
    }

    public async Task UpsertChunksAsync(
        List<ContentChunk> chunks,
        IProgress<(int current, int total)>? progress = null)
    {
        const int batchSize = 100;

        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            await UpsertBatchAsync(batch);
            progress?.Report((Math.Min(i + batchSize, chunks.Count), chunks.Count));
        }
    }

    private async Task UpsertBatchAsync(List<ContentChunk> chunks)
    {
        var points = chunks.Select(chunk => new PointStruct
        {
            Id = GeneratePointId(chunk),
            Vectors = chunk.Embedding!,
            Payload =
            {
                ["chunk_id"] = chunk.ChunkId,
                ["document_slug"] = chunk.DocumentSlug,
                ["document_title"] = chunk.DocumentTitle,
                ["chunk_index"] = chunk.ChunkIndex,
                ["text"] = chunk.Text,
                ["section_heading"] = chunk.SectionHeading,
                ["headings"] = string.Join(" > ", chunk.Headings),
                ["categories"] = string.Join(", ", chunk.Categories),
                ["published_date"] = chunk.PublishedDate.ToString("yyyy-MM-dd"),
                ["language"] = chunk.Language,
                ["token_count"] = chunk.TokenCount
            }
        }).ToList();

        await _client.UpsertAsync(_collectionName, points);
    }

    private ulong GeneratePointId(ContentChunk chunk)
    {
        var idString = $"{chunk.DocumentSlug}_{chunk.ChunkIndex}";
        var hash = idString.GetHashCode();
        return (ulong)Math.Abs(hash);
    }

    public async Task DeleteDocumentChunksAsync(string slug)
    {
        await _client.DeleteAsync(
            collectionName: _collectionName,
            filter: new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "document_slug",
                            Match = new Match { Keyword = slug }
                        }
                    }
                }
            }
        );
    }

    public async Task<List<SearchResult>> SearchAsync(
        float[] queryEmbedding,
        int limit = 10,
        float scoreThreshold = 0.7f,
        string? languageFilter = null)
    {
        Filter? filter = null;

        if (!string.IsNullOrEmpty(languageFilter))
        {
            filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "language",
                            Match = new Match { Keyword = languageFilter }
                        }
                    }
                }
            };
        }

        var searchResult = await _client.SearchAsync(
            collectionName: _collectionName,
            vector: queryEmbedding,
            filter: filter,
            limit: (ulong)limit,
            scoreThreshold: scoreThreshold
        );

        return searchResult.Select(r => new SearchResult
        {
            ChunkId = r.Payload["chunk_id"].StringValue,
            DocumentSlug = r.Payload["document_slug"].StringValue,
            DocumentTitle = r.Payload["document_title"].StringValue,
            ChunkIndex = (int)r.Payload["chunk_index"].IntegerValue,
            Text = r.Payload["text"].StringValue,
            SectionHeading = r.Payload["section_heading"].StringValue,
            Score = r.Score,
            Categories = r.Payload["categories"].StringValue.Split(", ", StringSplitOptions.RemoveEmptyEntries)
        }).ToList();
    }

    public async Task<long> GetDocumentCountAsync()
    {
        var collection = await _client.GetCollectionInfoAsync(_collectionName);
        return (long)collection.PointsCount;
    }
}
