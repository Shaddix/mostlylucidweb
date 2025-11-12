using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System.Text;

namespace Mostlylucid.BlogLLM.Api.Services;

public class LlmInferenceService : IDisposable
{
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    private readonly InferenceParams _defaultParams;
    private readonly ILogger<LlmInferenceService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LlmInferenceService(
        string modelPath,
        int contextSize = 4096,
        int gpuLayerCount = 20,
        ILogger<LlmInferenceService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LlmInferenceService>.Instance;

        _logger.LogInformation("Loading LLM model from {ModelPath}", modelPath);
        _logger.LogInformation("Context size: {ContextSize}, GPU layers: {GpuLayers}", contextSize, gpuLayerCount);

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = (uint)contextSize,
            GpuLayerCount = gpuLayerCount,
            Seed = 1337,
            UseMemorymap = true,
            UseMemoryLock = false
        };

        _model = LLamaWeights.LoadFromFile(parameters);
        _context = _model.CreateContext(parameters);

        // Create default sampling pipeline
        var samplingPipeline = new DefaultSamplingPipeline
        {
            Temperature = 0.7f,
            TopP = 0.95f,
            TopK = 40,
            RepeatPenalty = 1.1f
        };

        _defaultParams = new InferenceParams
        {
            MaxTokens = 512,
            SamplingPipeline = samplingPipeline,
            AntiPrompts = new List<string> { "</s>", "[/INST]", "User:", "Human:" }
        };

        _logger.LogInformation("LLM model loaded successfully");
    }

    public async Task<(string response, int tokensGenerated)> GenerateAsync(
        string prompt,
        int maxTokens = 512,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var executor = new InteractiveExecutor(_context);

            // Create sampling pipeline with custom temperature
            var samplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = temperature,
                TopP = 0.95f,
                TopK = 40,
                RepeatPenalty = 1.1f
            };

            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                SamplingPipeline = samplingPipeline,
                AntiPrompts = _defaultParams.AntiPrompts
            };

            var responseBuilder = new StringBuilder();
            int tokenCount = 0;

            _logger.LogDebug("Starting inference with prompt length: {PromptLength}", prompt.Length);

            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                responseBuilder.Append(token);
                tokenCount++;

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            var response = responseBuilder.ToString().Trim();
            _logger.LogDebug("Inference complete. Generated {TokenCount} tokens", tokenCount);

            return (response, tokenCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<(string response, int tokensGenerated)> GenerateWithContextAsync(
        string systemPrompt,
        string userQuestion,
        List<string> contextChunks,
        int maxTokens = 512,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        var context = string.Join("\n\n", contextChunks.Select((c, i) => $"[Context {i + 1}]\n{c}"));

        var prompt = BuildRagPrompt(systemPrompt, context, userQuestion);

        return await GenerateAsync(prompt, maxTokens, temperature, cancellationToken);
    }

    private string BuildRagPrompt(string systemPrompt, string context, string question)
    {
        // Llama/Mistral instruction format
        return $@"<s>[INST] <<SYS>>
{systemPrompt}
<</SYS>>

Context information is below:
---
{context}
---

Based on the context information above, answer the following question. If the answer cannot be found in the context, say ""I don't have enough information to answer this question based on the provided context.""

Question: {question}
[/INST]

Answer: ";
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
        _context?.Dispose();
        _model?.Dispose();
    }
}
