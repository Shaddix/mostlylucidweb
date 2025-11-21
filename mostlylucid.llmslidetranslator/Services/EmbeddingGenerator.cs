using LLama;
using LLama.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
///     Generates embeddings using LlamaSharp
/// </summary>
public class EmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly LlmSlideTranslatorConfig _config;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger<EmbeddingGenerator> _logger;
    private LLamaEmbedder? _embedder;
    private bool _initialized;

    public EmbeddingGenerator(
        ILogger<EmbeddingGenerator> logger,
        IOptions<LlmSlideTranslatorConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _initLock.Dispose();
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        if (_embedder == null) throw new InvalidOperationException("Embedder not initialized");

        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

        try
        {
            var embeddings = await Task.Run(() => _embedder.GetEmbeddings(text), cancellationToken);
            // GetEmbeddings returns IReadOnlyList<float[]>, take the first one for single text input
            return embeddings.FirstOrDefault() ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding");
            throw;
        }
    }

    public async Task<List<TranslationBlock>> GenerateEmbeddingsAsync(
        List<TranslationBlock> blocks,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        _logger.LogInformation("Generating embeddings for {Count} blocks", blocks.Count);

        var tasks = blocks.Select(async block =>
        {
            if (block.ShouldTranslate) block.Embedding = await GenerateEmbeddingAsync(block.Text, cancellationToken);
            return block;
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public float CalculateSimilarity(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension");

        // Calculate cosine similarity
        var dotProduct = 0.0f;
        var magnitude1 = 0.0f;
        var magnitude2 = 0.0f;

        for (var i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            magnitude1 += embedding1[i] * embedding1[i];
            magnitude2 += embedding2[i] * embedding2[i];
        }

        magnitude1 = MathF.Sqrt(magnitude1);
        magnitude2 = MathF.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0) return 0;

        return dotProduct / (magnitude1 * magnitude2);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            _logger.LogInformation("Initializing embedding model from {ModelPath}",
                _config.Embedding.ModelPath);

            if (!File.Exists(_config.Embedding.ModelPath))
                throw new FileNotFoundException(
                    $"Embedding model not found at {_config.Embedding.ModelPath}. " +
                    "Please download a GGUF embedding model (e.g., nomic-embed-text).");

            var parameters = new ModelParams(_config.Embedding.ModelPath)
            {
                ContextSize = (uint)_config.Embedding.ContextSize,
                Embeddings = true,
                GpuLayerCount = 0 // Use CPU for embeddings
            };

            var weights = LLamaWeights.LoadFromFile(parameters);
            _embedder = new LLamaEmbedder(weights, parameters);

            _initialized = true;
            _logger.LogInformation("Embedding model initialized successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }
}