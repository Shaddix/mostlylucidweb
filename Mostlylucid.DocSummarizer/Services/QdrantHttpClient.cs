using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Simple HTTP-based Qdrant client for AOT compatibility.
/// The official Qdrant.Client uses gRPC which has AOT issues with System.Single marshalling.
/// </summary>
public class QdrantHttpClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

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
        using var doc = JsonDocument.Parse(json);
        
        var collections = new List<string>();
        if (doc.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("collections", out var collArray))
        {
            foreach (var coll in collArray.EnumerateArray())
            {
                if (coll.TryGetProperty("name", out var name))
                {
                    collections.Add(name.GetString() ?? "");
                }
            }
        }
        return collections;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] ListCollections failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    public async Task CreateCollectionAsync(string name, int vectorSize)
    {
        var body = $$"""
        {
            "vectors": {
                "size": {{vectorSize}},
                "distance": "Cosine"
            }
        }
        """;
        
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{_baseUrl}/collections/{name}", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string name)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}/collections/{name}");
        // Don't throw if collection doesn't exist
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task UpsertAsync(string collectionName, List<QdrantPoint> points)
    {
        var pointsJson = new StringBuilder();
        pointsJson.Append('[');
        
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0) pointsJson.Append(',');
            var p = points[i];
            
            // Build vector array
            var vectorJson = string.Join(",", p.Vector.Select(v => v.ToString("G")));
            
            // Build payload
            var payloadJson = new StringBuilder();
            payloadJson.Append('{');
            var first = true;
            foreach (var kv in p.Payload)
            {
                if (!first) payloadJson.Append(',');
                first = false;
                
                var valueJson = kv.Value switch
                {
                    string s => $"\"{EscapeJson(s)}\"",
                    int n => n.ToString(),
                    long n => n.ToString(),
                    double d => d.ToString("G"),
                    float f => f.ToString("G"),
                    bool b => b ? "true" : "false",
                    _ => $"\"{EscapeJson(kv.Value?.ToString() ?? "")}\""
                };
                payloadJson.Append($"\"{kv.Key}\":{valueJson}");
            }
            payloadJson.Append('}');
            
            pointsJson.Append($$"""{"id":"{{p.Id}}","vector":[{{vectorJson}}],"payload":{{payloadJson}}}""");
        }
        pointsJson.Append(']');
        
        var body = $$"""{"points":{{pointsJson}}}""";
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{_baseUrl}/collections/{collectionName}/points", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Qdrant upsert failed: {response.StatusCode} - {error}");
        }
    }

    public async Task<List<QdrantSearchResult>> SearchAsync(string collectionName, float[] vector, int limit = 5)
    {
        var vectorJson = string.Join(",", vector.Select(v => v.ToString("G")));
        var body = $$"""
        {
            "vector": [{{vectorJson}}],
            "limit": {{limit}},
            "with_payload": true
        }
        """;
        
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/search", content);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        var results = new List<QdrantSearchResult>();
        if (doc.RootElement.TryGetProperty("result", out var resultArray))
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                var payload = new Dictionary<string, string>();
                if (item.TryGetProperty("payload", out var payloadObj))
                {
                    foreach (var prop in payloadObj.EnumerateObject())
                    {
                        payload[prop.Name] = prop.Value.ToString();
                    }
                }
                
                results.Add(new QdrantSearchResult
                {
                    Id = item.TryGetProperty("id", out var id) ? id.ToString() : "",
                    Score = item.TryGetProperty("score", out var score) ? score.GetSingle() : 0,
                    Payload = payload
                });
            }
        }
        
        return results;
    }

    private static string EscapeJson(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32)
                        sb.Append($"\\u{(int)c:x4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}

public class QdrantPoint
{
    public required string Id { get; set; }
    public required float[] Vector { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}

public class QdrantSearchResult
{
    public string Id { get; set; } = "";
    public float Score { get; set; }
    public Dictionary<string, string> Payload { get; set; } = new();
}
