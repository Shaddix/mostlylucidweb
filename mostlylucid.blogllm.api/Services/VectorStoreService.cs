using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Mostlylucid.BlogLLM.Api.Services;

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
            Categories = r.Payload["categories"].StringValue.Split(", ", StringSplitOptions.RemoveEmptyEntries),
            Language = r.Payload.ContainsKey("language") ? r.Payload["language"].StringValue : "en"
        }).ToList();
    }

    public async Task<long> GetDocumentCountAsync()
    {
        var collection = await _client.GetCollectionInfoAsync(_collectionName);
        return (long)collection.PointsCount;
    }
}

public class SearchResult
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentSlug { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string SectionHeading { get; set; } = string.Empty;
    public float Score { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string Language { get; set; } = "en";
}
