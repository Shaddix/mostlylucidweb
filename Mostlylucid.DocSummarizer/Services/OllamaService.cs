using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaSharp;
using OllamaSharp.Models;
using Mostlylucid.DocSummarizer.Config;

namespace Mostlylucid.DocSummarizer.Services;

public class OllamaService
{
    private readonly OllamaApiClient _client;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _embedModel;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Default timeout for LLM generation (10 minutes for large documents/slow models)
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public OllamaService(
        string model = "qwen2.5:1.5b",
        string embedModel = "mxbai-embed-large",
        string baseUrl = "http://localhost:11434",
        TimeSpan? timeout = null)
    {
        _timeout = timeout ?? DefaultTimeout;
        _baseUrl = baseUrl.TrimEnd('/');
        
        // Create HttpClient for direct API calls (embeddings)
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = _timeout + TimeSpan.FromMinutes(1)
        };
        
        // Create a custom HttpClient with proper timeout for OllamaSharp
        // OllamaSharp's internal HttpClient defaults to 100 seconds which is too short
        var ollamaHttpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = _timeout + TimeSpan.FromMinutes(1) // Give extra buffer beyond our CancellationToken timeout
        };
        
        // Use the HttpClient constructor for custom timeout + Native AOT support
        // OllamaApiClient(HttpClient, string model, JsonSerializerContext)
        _client = new OllamaApiClient(ollamaHttpClient, model, DocSummarizerJsonContext.Default);
        _model = model;
        _embedModel = embedModel;
    }

    public string Model => _model;
    public string EmbedModel => _embedModel;
    public TimeSpan Timeout => _timeout;

    public async Task<string> GenerateAsync(string prompt, double temperature = 0.3, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);
        
        var request = new GenerateRequest
        {
            Model = _model,
            Prompt = prompt,
            Options = new RequestOptions { Temperature = (float)temperature }
        };

        var sb = new StringBuilder();
        try
        {
            await foreach (var chunk in _client.GenerateAsync(request, cts.Token))
            {
                if (chunk?.Response != null)
                    sb.Append(chunk.Response);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"LLM generation timed out after {_timeout.TotalMinutes:F0} minutes. Consider using a faster model or increasing the timeout.");
        }
        return sb.ToString().Trim();
    }

    public async Task<float[]> EmbedAsync(string text, int maxRetries = 3)
    {
        // Clean and normalize text for embedding
        var cleanText = NormalizeTextForEmbedding(text);
        
        // Use direct HTTP call with explicit JSON to avoid OllamaSharp AOT serialization issues
        var json = $$"""{"model":"{{_embedModel}}","prompt":"{{EscapeJsonString(cleanText)}}"}""";
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        Exception? lastException = null;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/embeddings", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"Ollama embedding request failed with status {response.StatusCode}: {errorBody}. " +
                        $"Request was: {json[..Math.Min(200, json.Length)]}...");
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var embedding = doc.RootElement.GetProperty("embedding");
                
                var result = new float[embedding.GetArrayLength()];
                var i = 0;
                foreach (var val in embedding.EnumerateArray())
                {
                    result[i++] = (float)val.GetDouble();
                }
                return result;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                // Wait before retry, increasing delay each time
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                // Recreate content for retry
                content = new StringContent(json, Encoding.UTF8, "application/json");
            }
        }
        
        throw lastException ?? new InvalidOperationException("Embedding failed after retries");
    }
    
    /// <summary>
    /// Normalize text for embedding - remove problematic characters
    /// </summary>
    private static string NormalizeTextForEmbedding(string text)
    {
        // Normalize line endings to \n
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Remove null characters and other control characters (except newline/tab)
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c == '\n' || c == '\t' || !char.IsControl(c))
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString();
    }
    
    private static string EscapeJsonString(string s)
    {
        var sb = new StringBuilder(s.Length + 10);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
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

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var models = await _client.ListLocalModelsAsync();
            return models.Any();
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
            var model = modelName ?? _model;
            var showResponse = await _client.ShowModelAsync(model);
            
            if (showResponse == null) return null;

            // Parse model info from response
            var modelInfo = new ModelInfo
            {
                Name = model,
                ParameterCount = showResponse.Details?.ParameterSize ?? "unknown",
                QuantizationLevel = showResponse.Details?.QuantizationLevel ?? "unknown",
                Family = showResponse.Details?.Family ?? "unknown",
                Format = showResponse.Details?.Format ?? "unknown"
            };

            // Use known context windows based on model name/family
            modelInfo.ContextWindow = GetContextWindowForModel(model, modelInfo.Family);

            return modelInfo;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Get the context window size for the current model
    /// </summary>
    public async Task<int> GetContextWindowAsync()
    {
        var info = await GetModelInfoAsync();
        return info?.ContextWindow ?? 8192; // Conservative default
    }

    /// <summary>
    /// Get context window for a model based on name or family
    /// </summary>
    private static int GetContextWindowForModel(string model, string family)
    {
        // Exact model name matches
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

        if (contextWindows.TryGetValue(model, out var knownWindow))
        {
            return knownWindow;
        }

        // Family-based defaults
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

        // Model name pattern matching
        var modelLower = model.ToLowerInvariant();
        if (modelLower.Contains("llama3") || modelLower.Contains("ministral"))
            return 128000;
        if (modelLower.Contains("gemma3"))
            return 32000;
        if (modelLower.Contains("qwen"))
            return 32000;

        // Default for unknown models
        return 8192;
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        try
        {
            var models = await _client.ListLocalModelsAsync();
            return models.Select(m => m.Name).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}

public record ModelInfo
{
    public string Name { get; set; } = "";
    public string ParameterCount { get; set; } = "unknown";
    public string QuantizationLevel { get; set; } = "unknown";
    public string Family { get; set; } = "unknown";
    public string Format { get; set; } = "unknown";
    public int ContextWindow { get; set; } = 2048; // Default fallback
}
