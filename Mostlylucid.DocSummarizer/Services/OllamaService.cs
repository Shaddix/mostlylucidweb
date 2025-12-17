using System.Text;
using System.Text.Json;
using OllamaSharp;
using OllamaSharp.Models;
using Mostlylucid.DocSummarizer.Config;

namespace Mostlylucid.DocSummarizer.Services;

public class OllamaService
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly string _embedModel;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Default timeout for LLM generation (10 minutes for large documents/slow models)
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public OllamaService(
        string model = "ministral-3:3b",
        string embedModel = "nomic-embed-text",
        string baseUrl = "http://localhost:11434",
        TimeSpan? timeout = null)
    {
        _timeout = timeout ?? DefaultTimeout;
        
        // Configure OllamaApiClient with custom JsonSerializerContext for AOT support
        var config = new OllamaApiClient.Configuration
        {
            Uri = new Uri(baseUrl),
            Model = model,
            JsonSerializerContext = DocSummarizerJsonContext.Default
        };
        
        _client = new OllamaApiClient(config);
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

    public async Task<float[]> EmbedAsync(string text)
    {
        var request = new EmbedRequest
        {
            Model = _embedModel,
            Input = [text]
        };
        
        var response = await _client.EmbedAsync(request);
        return response.Embeddings.First().Select(d => (float)d).ToArray();
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
