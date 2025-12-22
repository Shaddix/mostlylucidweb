using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Fast LLM service using Ollama for creative text generation.
/// Optimized for llama3.2:3b for quick responses.
/// </summary>
public class LlmService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private bool _disposed;

    public LlmService(HttpClient httpClient, LlmConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }

    /// <summary>
    /// Check if Ollama is available with the configured model.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", ct);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync(ct);
            var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
            return tags?.Models?.Any(m => m.Name.StartsWith(_config.Model.Split(':')[0])) ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generate text completion with structured JSON output.
    /// </summary>
    public async Task<T?> GenerateAsync<T>(string prompt, CancellationToken ct = default) where T : class
    {
        var response = await GenerateRawAsync(prompt, ct);
        if (string.IsNullOrWhiteSpace(response)) return null;

        try
        {
            // Extract JSON from response (handle markdown code blocks)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<T>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]JSON parse error: {ex.Message}[/]");
        }

        return null;
    }

    /// <summary>
    /// Generate raw text completion.
    /// </summary>
    public async Task<string> GenerateRawAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _config.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = _config.Temperature,
                    NumPredict = _config.MaxTokens,
                    TopP = 0.9
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/generate", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);

            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]LLM error: {ex.Message}[/]");
            return string.Empty;
        }
    }

    /// <summary>
    /// Generate multiple completions in batch (sequential but faster with small model).
    /// </summary>
    public async Task<List<T>> GenerateBatchAsync<T>(
        IEnumerable<string> prompts,
        IProgress<int>? progress = null,
        CancellationToken ct = default) where T : class
    {
        var results = new List<T>();
        var promptList = prompts.ToList();
        var completed = 0;

        foreach (var prompt in promptList)
        {
            var result = await GenerateAsync<T>(prompt, ct);
            if (result != null)
            {
                results.Add(result);
            }
            completed++;
            progress?.Report(completed);
        }

        return results;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #region Request/Response Models

    private class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.8;

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; } = 512;

        [JsonPropertyName("top_p")]
        public double TopP { get; set; } = 0.9;
    }

    private class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;
    }

    #endregion
}

/// <summary>
/// LLM configuration from appsettings.
/// </summary>
public class LlmConfig
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2:3b";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxTokens { get; set; } = 512;
    public double Temperature { get; set; } = 0.8;
}
