using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using Polly;
using Polly.Retry;

namespace Mostlylucid.Summarizer.Shared.Services;

/// <summary>
/// Shared Ollama client with resilience and retry logic.
/// Used by both DocSummarizer and DataSummarizer.
/// </summary>
public class OllamaClientService : IDisposable
{
    private readonly OllamaApiClient _client;
    private readonly string _defaultModel;
    private readonly bool _verbose;
    private readonly AsyncRetryPolicy _retryPolicy;

    public OllamaClientService(
        string model = "llama3.2:3b",
        string baseUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _client = new OllamaApiClient(new Uri(baseUrl));
        _defaultModel = model;
        _verbose = verbose;
        
        // Retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"[Ollama] Retry {retryCount} after {timeSpan.TotalSeconds}s: {exception.Message}");
                    }
                });
    }

    /// <summary>
    /// Generate a completion with the default model
    /// </summary>
    public async Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var request = new GenerateRequest
            {
                Model = model ?? _defaultModel,
                Prompt = prompt
            };

            var response = await _client.GenerateAsync(request, ct).StreamToEndAsync();
            return response?.Response ?? "";
        });
    }

    /// <summary>
    /// Generate with streaming callback
    /// </summary>
    public async Task<string> GenerateStreamingAsync(
        string prompt,
        Action<string>? onToken = null,
        string? model = null,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        await _retryPolicy.ExecuteAsync(async () =>
        {
            var request = new GenerateRequest
            {
                Model = model ?? _defaultModel,
                Prompt = prompt
            };

            await foreach (var response in _client.GenerateAsync(request, ct))
            {
                if (response?.Response != null)
                {
                    sb.Append(response.Response);
                    onToken?.Invoke(response.Response);
                }
            }
        });

        return sb.ToString();
    }

    /// <summary>
    /// Check if Ollama is available
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(ct);
            return models?.Any() ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// List available models
    /// </summary>
    public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(ct);
            return models?.Select(m => m.Name).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Check if a specific model is available
    /// </summary>
    public async Task<bool> HasModelAsync(string model, CancellationToken ct = default)
    {
        var models = await ListModelsAsync(ct);
        return models.Any(m => m.StartsWith(model, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        // OllamaApiClient doesn't need disposal
    }
}
