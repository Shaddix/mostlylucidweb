using LLama;
using LLama.Common;
using LLama.Native;
using TinyLLM.Models;

namespace TinyLLM.Services;

public class ChatService : IDisposable
{
    private LLamaWeights? _model;
    private LLamaContext? _context;
    private InteractiveExecutor? _executor;
    private readonly RagService _ragService;
    private readonly List<ChatMessage> _conversationHistory;
    private AppSettings _settings;
    private bool _isInitialized;

    public event EventHandler<string>? OnTokenGenerated;
    public event EventHandler<string>? OnError;

    public bool IsInitialized => _isInitialized;

    public ChatService(RagService ragService)
    {
        _ragService = ragService;
        _conversationHistory = new List<ChatMessage>();
        _settings = new AppSettings();
    }

    public async Task<bool> InitializeAsync(string modelPath, AppSettings settings)
    {
        try
        {
            _settings = settings;

            // Check if CUDA is available when GPU is requested
            if (settings.UseGpu)
            {
                try
                {
                    // Try to load CUDA backend
                    NativeLibraryConfig.Instance.WithCuda();
                }
                catch
                {
                    OnError?.Invoke(this, "CUDA not available. Falling back to CPU.");
                    settings.UseGpu = false;
                }
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)settings.ContextSize,
                GpuLayerCount = settings.UseGpu ? settings.GpuLayers : 0,
                UseMemorymap = true,
                UseMemoryLock = false,
                Seed = (uint)Random.Shared.Next()
            };

            _model = await Task.Run(() => LLamaWeights.LoadFromFile(parameters));
            _context = await Task.Run(() => _model.CreateContext(parameters));
            _executor = new InteractiveExecutor(_context);

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, $"Failed to initialize model: {ex.Message}");
            return false;
        }
    }

    public async IAsyncEnumerable<string> ChatAsync(string userMessage, bool useRag = true)
    {
        if (!_isInitialized || _executor == null)
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

        // Build prompt with RAG context if enabled
        var prompt = BuildPrompt(userMessage, useRag);

        var inferenceParams = new InferenceParams
        {
            Temperature = _settings.Temperature,
            MaxTokens = _settings.MaxTokens,
            AntiPrompts = new[] { "User:", "user:", "\nUser:", "\nuser:" }
        };

        var fullResponse = "";

        await foreach (var token in _executor.InferAsync(prompt, inferenceParams))
        {
            fullResponse += token;
            OnTokenGenerated?.Invoke(this, token);
            yield return token;
        }

        // Add assistant response to history
        _conversationHistory.Add(new ChatMessage
        {
            Role = "assistant",
            Content = fullResponse.Trim()
        });

        // Store conversation in RAG for future context
        if (useRag)
        {
            _ragService.AddConversationToRag(userMessage, fullResponse.Trim());
        }
    }

    private string BuildPrompt(string userMessage, bool useRag)
    {
        var promptBuilder = new System.Text.StringBuilder();

        // System prompt
        promptBuilder.AppendLine("You are a helpful AI assistant. You have access to context from previous conversations.");
        promptBuilder.AppendLine();

        // Add RAG context if enabled
        if (useRag)
        {
            var ragContext = _ragService.GetContextForQuery(userMessage, _settings.TopRagResults);
            if (!string.IsNullOrEmpty(ragContext))
            {
                promptBuilder.AppendLine(ragContext);
                promptBuilder.AppendLine();
            }
        }

        // Add recent conversation history (last 3 exchanges)
        var recentHistory = _conversationHistory
            .TakeLast(6) // 3 user + 3 assistant messages
            .ToList();

        foreach (var message in recentHistory)
        {
            if (message.Role == "user")
                promptBuilder.AppendLine($"User: {message.Content}");
            else
                promptBuilder.AppendLine($"Assistant: {message.Content}");
        }

        // Add current user message
        promptBuilder.AppendLine($"User: {userMessage}");
        promptBuilder.Append("Assistant:");

        return promptBuilder.ToString();
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
        _executor?.Dispose();
        _context?.Dispose();
        _model?.Dispose();
    }
}
