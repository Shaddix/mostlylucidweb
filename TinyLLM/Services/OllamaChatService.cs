using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TinyLLM.Models;

namespace TinyLLM.Services;

public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 512;
}

public class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaChatService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly RagService _ragService;
    private readonly List<ChatMessage> _conversationHistory;
    private AppSettings _settings;
    private string _currentModel = "";
    private readonly string _baseUrl;

    public event EventHandler<string>? OnTokenGenerated;
    public event EventHandler<string>? OnError;

    public bool IsInitialized { get; private set; }

    public OllamaChatService(RagService ragService, string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _ragService = ragService;
        _conversationHistory = new List<ChatMessage>();
        _settings = new AppSettings();
    }

    public async Task<bool> InitializeAsync(string modelName, AppSettings settings)
    {
        try
        {
            _settings = settings;
            _currentModel = modelName;

            // Test if model is available
            var testRequest = new OllamaChatRequest
            {
                Model = modelName,
                Messages = new List<OllamaChatMessage>
                {
                    new() { Role = "user", Content = "Hi" }
                },
                Stream = false
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/chat", content);
            if (!response.IsSuccessStatusCode)
            {
                OnError?.Invoke(this, $"Model '{modelName}' not available in Ollama");
                return false;
            }

            IsInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, $"Failed to initialize Ollama model: {ex.Message}");
            return false;
        }
    }

    public async IAsyncEnumerable<string> ChatAsync(string userMessage, bool useRag = true, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            yield return "Error: Model not initialized";
            yield break;
        }

        // Add user message to history
        _conversationHistory.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        // Build messages with RAG context if enabled
        var messages = BuildMessages(userMessage, useRag);

        var request = new OllamaChatRequest
        {
            Model = _currentModel,
            Messages = messages,
            Stream = true,
            Options = new OllamaOptions
            {
                Temperature = _settings.Temperature,
                NumPredict = _settings.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var fullResponse = "";

        try
        {
            using var response = await _httpClient.PostAsync("/api/chat", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(line);
                    if (chatResponse?.Message?.Content != null)
                    {
                        var token = chatResponse.Message.Content;
                        fullResponse += token;
                        OnTokenGenerated?.Invoke(this, token);
                        yield return token;
                    }

                    if (chatResponse?.Done == true)
                        break;
                }
                catch
                {
                    // Ignore JSON parsing errors for individual lines
                }
            }

            // Add assistant response to history
            _conversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = fullResponse.Trim()
            });

            // Store conversation in RAG for future context
            if (useRag && !string.IsNullOrWhiteSpace(fullResponse))
            {
                _ragService.AddConversationToRag(userMessage, fullResponse.Trim());
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, $"Chat error: {ex.Message}");
            yield return $"\n\n[Error: {ex.Message}]";
        }
    }

    public string? LastRagContext { get; private set; }

    private List<OllamaChatMessage> BuildMessages(string userMessage, bool useRag)
    {
        var messages = new List<OllamaChatMessage>();

        // System message with RAG context
        var systemContent = @"You are a helpful and knowledgeable AI assistant. You provide clear, accurate, and concise responses.
When relevant context from previous conversations is provided, use it to give more informed and personalized answers.
Be natural and conversational while remaining helpful and professional.";

        LastRagContext = null;

        if (useRag)
        {
            var ragContext = _ragService.GetContextForQuery(userMessage, _settings.TopRagResults);
            if (!string.IsNullOrEmpty(ragContext))
            {
                systemContent += "\n\n" + ragContext;
                LastRagContext = ragContext;
            }
        }

        messages.Add(new OllamaChatMessage
        {
            Role = "system",
            Content = systemContent
        });

        // Add recent conversation history (last 6 messages = 3 exchanges)
        var recentHistory = _conversationHistory
            .TakeLast(6)
            .ToList();

        foreach (var message in recentHistory)
        {
            messages.Add(new OllamaChatMessage
            {
                Role = message.Role,
                Content = message.Content
            });
        }

        // Add current user message
        messages.Add(new OllamaChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        return messages;
    }

    public List<ChatMessage> GetConversationHistory()
    {
        return new List<ChatMessage>(_conversationHistory);
    }

    public void ClearConversationHistory()
    {
        _conversationHistory.Clear();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
