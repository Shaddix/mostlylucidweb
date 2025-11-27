using System.Text.Json;
using System.Text.Json.Serialization;

namespace QdrantMarkdownSearch.Services;

/// <summary>
/// Embedding service using local Ollama instance
/// This is completely free and runs entirely on your machine!
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaEmbeddingService> _logger;
    private readonly string _endpoint;
    private readonly string _model;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "nomic-embed-text";
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _model,
                Prompt = text
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_endpoint}/api/embeddings",
                request,
                ct
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(ct);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Ollama returned empty embedding");
            }

            _logger.LogDebug("Generated embedding with {Dimensions} dimensions", result.Embedding.Length);

            return result.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text (length: {Length})", text.Length);
            throw;
        }
    }

    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
