using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.DocSummarizer.Config;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Simple HTTP-based Qdrant client for AOT compatibility.
///     The official Qdrant.Client uses gRPC which has AOT issues with System.Single marshalling.
/// </summary>
public class QdrantHttpClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    public QdrantHttpClient(string host = "localhost", int port = 6333, string? apiKey = null)
    {
        // Use 127.0.0.1 if localhost to avoid DNS resolution issues
        var resolvedHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
        _baseUrl = $"http://{resolvedHost}:{port}";

        // Force IPv4 and configure socket handler explicitly
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Add API key header if provided
        if (!string.IsNullOrEmpty(apiKey))
        {
            _http.DefaultRequestHeaders.Add("api-key", apiKey);
            Console.WriteLine("[DEBUG] Qdrant API key configured");
        }
    }

    public async Task<IEnumerable<string>> ListCollectionsAsync()
    {
        try
        {
            Console.WriteLine($"[DEBUG] Requesting: {_baseUrl}/collections");
            var response = await _http.GetAsync($"{_baseUrl}/collections");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.QdrantCollectionsResponse);

            return result?.Result?.Collections?.Select(c => c.Name ?? "") ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] ListCollections failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    public async Task CreateCollectionAsync(string name, int vectorSize)
    {
        var request = new QdrantCreateCollectionRequest
        {
            Vectors = new QdrantVectorConfig
            {
                Size = vectorSize,
                Distance = "Cosine"
            }
        };

        var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.QdrantCreateCollectionRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{_baseUrl}/collections/{name}", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string name)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/collections/{name}");
        // Don't throw if collection doesn't exist
        if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(string collectionName, List<QdrantPoint> points)
    {
        var request = new QdrantUpsertRequest { Points = points };
        var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.QdrantUpsertRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{_baseUrl}/collections/{collectionName}/points", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Qdrant upsert failed: {response.StatusCode} - {error}");
        }
    }

    public async Task<List<QdrantSearchResult>> SearchAsync(string collectionName, float[] vector, int limit = 5)
    {
        var request = new QdrantSearchRequest
        {
            Vector = vector,
            Limit = limit,
            WithPayload = true
        };

        var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.QdrantSearchRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/search", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var searchResponse =
            JsonSerializer.Deserialize(responseJson, DocSummarizerJsonContext.Default.QdrantSearchResponse);

        return searchResponse?.Result ?? new List<QdrantSearchResult>();
    }
}

// Qdrant DTOs for source-generated JSON
public class QdrantPoint
{
    [JsonPropertyName("id")] public required string Id { get; set; }

    [JsonPropertyName("vector")] public required float[] Vector { get; set; }

    [JsonPropertyName("payload")] public Dictionary<string, object> Payload { get; set; } = new();
}

public class QdrantSearchResult
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("score")] public float Score { get; set; }

    [JsonPropertyName("payload")] public Dictionary<string, JsonElement>? Payload { get; set; }

    /// <summary>
    ///     Get payload as string dictionary for convenience
    /// </summary>
    public Dictionary<string, string> GetPayloadStrings()
    {
        var result = new Dictionary<string, string>();
        if (Payload == null) return result;

        foreach (var kv in Payload) result[kv.Key] = kv.Value.ToString();
        return result;
    }
}

public class QdrantUpsertRequest
{
    [JsonPropertyName("points")] public List<QdrantPoint> Points { get; set; } = new();
}

public class QdrantSearchRequest
{
    [JsonPropertyName("vector")] public float[] Vector { get; set; } = Array.Empty<float>();

    [JsonPropertyName("limit")] public int Limit { get; set; } = 5;

    [JsonPropertyName("with_payload")] public bool WithPayload { get; set; } = true;
}

public class QdrantSearchResponse
{
    [JsonPropertyName("result")] public List<QdrantSearchResult>? Result { get; set; }
}

public class QdrantCollectionsResponse
{
    [JsonPropertyName("result")] public QdrantCollectionsResult? Result { get; set; }
}

public class QdrantCollectionsResult
{
    [JsonPropertyName("collections")] public List<QdrantCollectionInfo>? Collections { get; set; }
}

public class QdrantCollectionInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public class QdrantCreateCollectionRequest
{
    [JsonPropertyName("vectors")] public QdrantVectorConfig? Vectors { get; set; }
}

public class QdrantVectorConfig
{
    [JsonPropertyName("size")] public int Size { get; set; }

    [JsonPropertyName("distance")] public string Distance { get; set; } = "Cosine";
}