using Microsoft.EntityFrameworkCore;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Mostlylucid.SegmentCommerce.Services.Embeddings;

/// <summary>
/// ONNX-based embedding service using all-MiniLM-L6-v2 model.
/// Runs locally without external API dependencies.
/// Auto-downloads model from HuggingFace on first use.
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<OnnxEmbeddingService> _logger;
    private InferenceSession? _session;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly int _maxSequenceLength;
    private readonly int _embeddingDimension;
    private readonly string _modelPath;
    private readonly string _vocabPath;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized;

    // HuggingFace model URLs for all-MiniLM-L6-v2
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    public OnnxEmbeddingService(
        SegmentCommerceDbContext context,
        ILogger<OnnxEmbeddingService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _vocabulary = new Dictionary<string, int>();
        _maxSequenceLength = configuration.GetValue("Onnx:MaxSequenceLength", 128);
        _embeddingDimension = configuration.GetValue("Onnx:EmbeddingDimension", 384);
        _modelPath = configuration["Onnx:ModelPath"] ?? "Models/all-MiniLM-L6-v2.onnx";
        _vocabPath = configuration["Onnx:VocabPath"] ?? "Models/vocab.txt";
    }

    public bool IsAvailable => _initialized;

    /// <summary>
    /// Ensures the model is initialized, downloading from HuggingFace if necessary.
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            // Ensure directory exists
            var modelDir = Path.GetDirectoryName(_modelPath);
            if (!string.IsNullOrEmpty(modelDir) && !Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
                _logger.LogInformation("Created model directory: {Path}", modelDir);
            }

            // Download model if not exists
            if (!File.Exists(_modelPath))
            {
                _logger.LogInformation("Downloading ONNX embedding model to {Path}...", _modelPath);
                await DownloadFileAsync(ModelUrl, _modelPath, cancellationToken);
                _logger.LogInformation("ONNX model downloaded successfully");
            }

            // Download vocab if not exists
            if (!File.Exists(_vocabPath))
            {
                _logger.LogInformation("Downloading vocabulary file to {Path}...", _vocabPath);
                await DownloadFileAsync(VocabUrl, _vocabPath, cancellationToken);
                _logger.LogInformation("Vocabulary file downloaded successfully");
            }

            // Load vocabulary
            LoadVocabulary(_vocabPath);

            // Create ONNX session
            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            _session = new InferenceSession(_modelPath, sessionOptions);
            _logger.LogInformation("ONNX embedding model loaded successfully from {Path}", _modelPath);
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX embedding service");
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10); // Large file timeout

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await contentStream.CopyToAsync(fileStream, cancellationToken);
    }

    private void LoadVocabulary(string vocabPath)
    {
        var lines = File.ReadAllLines(vocabPath);
        for (int i = 0; i < lines.Length; i++)
        {
            _vocabulary[lines[i]] = i;
        }
        _logger.LogDebug("Loaded vocabulary with {Count} tokens", _vocabulary.Count);
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings[0];
    }

    public async Task<Vector[]> GenerateEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_session == null)
        {
            throw new InvalidOperationException("ONNX embedding service failed to initialize.");
        }

        var results = new List<Vector>();

        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Tokenize
            var (inputIds, attentionMask, tokenTypeIds) = Tokenize(text);

            // Create tensors
            var inputIdsTensor = new DenseTensor<long>(inputIds, [1, inputIds.Length]);
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, tokenTypeIds.Length]);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };

            // Run inference
            using var outputs = _session.Run(inputs);
            
            // Get the last hidden state and perform mean pooling
            var lastHiddenState = outputs.First().AsTensor<float>();
            var embedding = MeanPooling(lastHiddenState, attentionMask);
            
            // Normalize
            var normalized = L2Normalize(embedding);
            
            results.Add(new Vector(normalized));
        }

        return results.ToArray();
    }

    private (long[] inputIds, long[] attentionMask, long[] tokenTypeIds) Tokenize(string text)
    {
        // Simple WordPiece-style tokenization
        var tokens = new List<string> { "[CLS]" };
        
        // Normalize and split
        text = text.ToLowerInvariant();
        var words = Regex.Split(text, @"\s+").Where(w => !string.IsNullOrEmpty(w));

        foreach (var word in words)
        {
            if (_vocabulary.ContainsKey(word))
            {
                tokens.Add(word);
            }
            else
            {
                // Simple subword tokenization
                var remaining = word;
                var isFirst = true;
                while (remaining.Length > 0)
                {
                    var found = false;
                    for (int len = Math.Min(remaining.Length, 20); len > 0; len--)
                    {
                        var subword = isFirst ? remaining[..len] : "##" + remaining[..len];
                        if (_vocabulary.ContainsKey(subword))
                        {
                            tokens.Add(subword);
                            remaining = remaining[len..];
                            isFirst = false;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        tokens.Add("[UNK]");
                        break;
                    }
                }
            }
        }

        tokens.Add("[SEP]");

        // Truncate if necessary
        if (tokens.Count > _maxSequenceLength)
        {
            tokens = tokens.Take(_maxSequenceLength - 1).Append("[SEP]").ToList();
        }

        // Convert to IDs
        var inputIds = tokens.Select(t => (long)_vocabulary.GetValueOrDefault(t, _vocabulary["[UNK]"])).ToArray();
        var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();
        var tokenTypeIds = Enumerable.Repeat(0L, inputIds.Length).ToArray();

        // Pad to max length
        var padLength = _maxSequenceLength - inputIds.Length;
        if (padLength > 0)
        {
            var padId = (long)_vocabulary.GetValueOrDefault("[PAD]", 0);
            inputIds = inputIds.Concat(Enumerable.Repeat(padId, padLength)).ToArray();
            attentionMask = attentionMask.Concat(Enumerable.Repeat(0L, padLength)).ToArray();
            tokenTypeIds = tokenTypeIds.Concat(Enumerable.Repeat(0L, padLength)).ToArray();
        }

        return (inputIds, attentionMask, tokenTypeIds);
    }

    private float[] MeanPooling(Tensor<float> lastHiddenState, long[] attentionMask)
    {
        var seqLen = attentionMask.Length;
        var embedding = new float[_embeddingDimension];
        var count = 0;

        for (int i = 0; i < seqLen; i++)
        {
            if (attentionMask[i] == 1)
            {
                for (int j = 0; j < _embeddingDimension; j++)
                {
                    embedding[j] += lastHiddenState[0, i, j];
                }
                count++;
            }
        }

        if (count > 0)
        {
            for (int j = 0; j < _embeddingDimension; j++)
            {
                embedding[j] /= count;
            }
        }

        return embedding;
    }

    private static float[] L2Normalize(float[] vector)
    {
        var norm = (float)Math.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
        return vector;
    }

    public async Task<IEnumerable<ProductSimilarityResult>> FindSimilarProductsAsync(
        Vector queryEmbedding,
        int limit = 10,
        float minSimilarity = 0.5f,
        CancellationToken cancellationToken = default)
    {
        var results = await _context.ProductEmbeddings
            .Include(e => e.Product)
            .Select(e => new
            {
                e.ProductId,
                e.Product.Name,
                e.Product.Category,
                e.Product.Price,
                Distance = e.Embedding.CosineDistance(queryEmbedding)
            })
            .Where(x => (1 - x.Distance) >= minSimilarity)
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return results.Select(r => new ProductSimilarityResult
        {
            ProductId = r.ProductId,
            ProductName = r.Name,
            Category = r.Category,
            Price = r.Price,
            Similarity = 1 - (float)r.Distance
        });
    }

    public async Task<IEnumerable<ProductSimilarityResult>> SearchProductsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        return await FindSimilarProductsAsync(queryEmbedding, limit, minSimilarity: 0.3f, cancellationToken);
    }

    public async Task<bool> IndexProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var product = await _context.Products.FindAsync([productId], cancellationToken);
        if (product == null)
        {
            _logger.LogWarning("Product {ProductId} not found for indexing", productId);
            return false;
        }

        var text = $"{product.Name}. {product.Description}. Category: {product.Category}. Tags: {string.Join(", ", product.Tags)}";

        try
        {
            var embedding = await GenerateEmbeddingAsync(text, cancellationToken);

            var existing = await _context.ProductEmbeddings
                .FirstOrDefaultAsync(e => e.ProductId == productId, cancellationToken);

            if (existing != null)
            {
                existing.Embedding = embedding;
                existing.SourceText = text;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ProductEmbeddings.Add(new ProductEmbeddingEntity
                {
                    ProductId = productId,
                    Embedding = embedding,
                    SourceText = text,
                    Model = "all-MiniLM-L6-v2-onnx"
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Indexed product {ProductId} with ONNX embeddings", productId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index product {ProductId}", productId);
            return false;
        }
    }

    public async Task<int> ReindexAllProductsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var productIds = await _context.Products
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var indexed = 0;
        var failures = 0;

        foreach (var productId in productIds)
        {
            if (await IndexProductAsync(productId, cancellationToken))
            {
                indexed++;
            }
            else
            {
                failures++;
            }
        }

        _logger.LogInformation("Reindexed {Indexed}/{Total} products with ONNX ({Failures} failures)", indexed, productIds.Count, failures);
        return indexed;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initSemaphore.Dispose();
    }
}
