using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.DocSummarizer.Config;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Lightweight Ollama HTTP client for AOT compatibility.
///     Replaces OllamaSharp to reduce binary size and avoid reflection.
/// </summary>
public class OllamaService
{
    /// <summary>
    ///     Default timeout for LLM generation (20 minutes for large documents/slow models)
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);

    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;

    public OllamaService(
        string model = "llama3.2:3b",
        string embedModel = "nomic-embed-text",
        string baseUrl = "http://localhost:11434",
        TimeSpan? timeout = null)
    {
        _timeout = timeout ?? DefaultTimeout;
        _baseUrl = baseUrl.TrimEnd('/');
        Model = model;
        EmbedModel = embedModel;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = _timeout + TimeSpan.FromMinutes(1)
        };
    }

    public string Model { get; }

    public string EmbedModel { get; }

    public TimeSpan Timeout => _timeout;

    public async Task<string> GenerateAsync(string prompt, double temperature = 0.3,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        var request = new OllamaGenerateRequest
        {
            Model = Model,
            Prompt = prompt,
            Options = new OllamaOptions { Temperature = temperature }
        };

        var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.OllamaGenerateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var sb = new StringBuilder();
        try
        {
            using var response = await _httpClient.PostAsync("/api/generate", content, cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            // Read streaming NDJSON response
            string? line;
            while ((line = await reader.ReadLineAsync(cts.Token)) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;

                var chunk = JsonSerializer.Deserialize(line, DocSummarizerJsonContext.Default.OllamaGenerateResponse);
                if (chunk?.Response != null) sb.Append(chunk.Response);

                if (chunk?.Done == true) break;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"LLM generation timed out after {_timeout.TotalMinutes:F0} minutes. Consider using a faster model or increasing the timeout.");
        }

        return sb.ToString().Trim();
    }

    public async Task<float[]> EmbedAsync(string text, int maxRetries = 3)
    {
        var cleanText = NormalizeTextForEmbedding(text);
        var request = new OllamaEmbedRequest { Model = EmbedModel, Prompt = cleanText };
        var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.OllamaEmbedRequest);

        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/embeddings", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"Ollama embedding request failed with status {response.StatusCode}: {errorBody}. " +
                        $"Request was: {json[..Math.Min(200, json.Length)]}...");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var embedResponse =
                    JsonSerializer.Deserialize(responseJson, DocSummarizerJsonContext.Default.OllamaEmbedResponse);

                return embedResponse?.Embedding ?? throw new InvalidOperationException("No embedding returned");
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }

        throw lastException ?? new InvalidOperationException("Embedding failed after retries");
    }

    private static string NormalizeTextForEmbedding(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (c == '\n' || c == '\t' || !char.IsControl(c))
                sb.Append(c);

        return sb.ToString();
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var tagsResponse = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.OllamaTagsResponse);
            return tagsResponse?.Models?.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ModelInfo?> GetModelInfoAsync(string? modelName = null)
    {
        try
        {
            var model = modelName ?? Model;
            var request = new OllamaShowRequest { Name = model };
            var json = JsonSerializer.Serialize(request, DocSummarizerJsonContext.Default.OllamaShowRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/show", content);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            var showResponse =
                JsonSerializer.Deserialize(responseJson, DocSummarizerJsonContext.Default.OllamaShowResponse);

            var modelInfo = new ModelInfo
            {
                Name = model,
                ParameterCount = showResponse?.Details?.ParameterSize ?? "unknown",
                QuantizationLevel = showResponse?.Details?.QuantizationLevel ?? "unknown",
                Family = showResponse?.Details?.Family ?? "unknown",
                Format = showResponse?.Details?.Format ?? "unknown"
            };

            modelInfo.ContextWindow = GetContextWindowForModel(model, modelInfo.Family);
            return modelInfo;
        }
        catch
        {
            return null;
        }
    }

    public async Task<int> GetContextWindowAsync()
    {
        var info = await GetModelInfoAsync();
        return info?.ContextWindow ?? 8192;
    }

    private static int GetContextWindowForModel(string model, string family)
    {
        var contextWindows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "ministral-3:3b", 128000 },
            { "ministral-3:latest", 128000 },
            { "llama3.2:3b", 128000 },
            { "llama3.2:latest", 128000 },
            { "llama3.1:8b", 128000 },
            { "llama3.1:latest", 128000 },
            { "gemma3:1b", 32000 },
            { "gemma3:4b", 128000 },
            { "gemma3:12b", 128000 },
            { "gemma2:2b", 8192 },
            { "gemma2:9b", 8192 },
            { "qwen2.5:1.5b", 32000 },
            { "qwen2.5:3b", 32000 },
            { "qwen2.5:7b", 32000 },
            { "phi3:mini", 128000 },
            { "phi3:medium", 128000 },
            { "mistral:7b", 32000 },
            { "mistral:latest", 32000 },
            { "tinyllama:latest", 2048 },
            { "nomic-embed-text", 8192 },
            { "nomic-embed-text:latest", 8192 }
        };

        if (contextWindows.TryGetValue(model, out var knownWindow)) return knownWindow;

        var familyLower = family.ToLowerInvariant();
        if (familyLower.Contains("llama3") || familyLower.Contains("ministral"))
            return 128000;
        if (familyLower.Contains("gemma3"))
            return 32000;
        if (familyLower.Contains("qwen"))
            return 32000;
        if (familyLower.Contains("phi"))
            return 128000;
        if (familyLower.Contains("mistral"))
            return 32000;

        var modelLower = model.ToLowerInvariant();
        if (modelLower.Contains("llama3") || modelLower.Contains("ministral"))
            return 128000;
        if (modelLower.Contains("gemma3"))
            return 32000;
        if (modelLower.Contains("qwen"))
            return 32000;

        return 8192;
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            if (!response.IsSuccessStatusCode) return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            var tagsResponse = JsonSerializer.Deserialize(json, DocSummarizerJsonContext.Default.OllamaTagsResponse);

            return tagsResponse?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

// DTOs for Ollama API - used by source generator
public class OllamaGenerateRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";

    [JsonPropertyName("options")] public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("response")] public string? Response { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }
}

public class OllamaEmbedRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";

    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";
}

public class OllamaEmbedResponse
{
    [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
}

public class OllamaShowRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public class OllamaShowResponse
{
    [JsonPropertyName("details")] public OllamaModelDetails? Details { get; set; }
}

public class OllamaModelDetails
{
    [JsonPropertyName("parameter_size")] public string? ParameterSize { get; set; }

    [JsonPropertyName("quantization_level")]
    public string? QuantizationLevel { get; set; }

    [JsonPropertyName("family")] public string? Family { get; set; }

    [JsonPropertyName("format")] public string? Format { get; set; }
}

public class OllamaTagsResponse
{
    [JsonPropertyName("models")] public List<OllamaModelInfo>? Models { get; set; }
}

public class OllamaModelInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public record ModelInfo
{
    public string Name { get; set; } = "";
    public string ParameterCount { get; set; } = "unknown";
    public string QuantizationLevel { get; set; } = "unknown";
    public string Family { get; set; } = "unknown";
    public string Format { get; set; } = "unknown";
    public int ContextWindow { get; set; } = 2048;
}