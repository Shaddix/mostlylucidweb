using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Fast ONNX-based embedding service using all-MiniLM-L6-v2.
/// Downloads model on first use.
/// </summary>
public class EmbeddingService : IDisposable
{
    private readonly EmbeddingConfig _config;
    private InferenceSession? _session;
    private readonly Dictionary<string, int> _vocabulary = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    private const int MaxSequenceLength = 256;
    private const string ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";
    private const string VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt";

    public EmbeddingService(EmbeddingConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Initialize the embedding model, downloading if necessary.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized || !_config.Enabled) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var modelDir = Path.GetDirectoryName(_config.ModelPath);
            if (!string.IsNullOrEmpty(modelDir) && !Directory.Exists(modelDir))
            {
                Directory.CreateDirectory(modelDir);
            }

            // Download model if not exists
            if (!File.Exists(_config.ModelPath))
            {
                AnsiConsole.MarkupLine($"[blue]Downloading ONNX model to {_config.ModelPath}...[/]");
                await DownloadFileAsync(ModelUrl, _config.ModelPath, ct);
            }

            // Download vocab if not exists
            if (!File.Exists(_config.VocabPath))
            {
                AnsiConsole.MarkupLine($"[blue]Downloading vocabulary to {_config.VocabPath}...[/]");
                await DownloadFileAsync(VocabUrl, _config.VocabPath, ct);
            }

            // Load vocabulary
            if (File.Exists(_config.VocabPath))
            {
                var lines = await File.ReadAllLinesAsync(_config.VocabPath, ct);
                for (int i = 0; i < lines.Length; i++)
                {
                    var token = lines[i].Trim();
                    if (!string.IsNullOrEmpty(token))
                        _vocabulary[token] = i;
                }
                AnsiConsole.MarkupLine($"[green]Loaded {_vocabulary.Count} vocabulary tokens[/]");
            }

            // Create ONNX session
            var sessionOptions = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            _session = new InferenceSession(_config.ModelPath, sessionOptions);
            _initialized = true;
            AnsiConsole.MarkupLine("[green]ONNX embedding model loaded[/]");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[blue]Downloading {Path.GetFileName(path)}[/]");
                task.IsIndeterminate = true;

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                if (totalBytes > 0)
                {
                    task.MaxValue = totalBytes;
                    task.IsIndeterminate = false;
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    task.Increment(bytesRead);
                }
            });
    }

    /// <summary>
    /// Generate embedding for a single text.
    /// </summary>
    public async Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        if (!_config.Enabled) return new float[_config.VectorSize];
        
        await InitializeAsync(ct);
        
        if (_session == null || string.IsNullOrWhiteSpace(text))
            return new float[_config.VectorSize];

        return await Task.Run(() => GenerateEmbedding(text), ct);
    }

    /// <summary>
    /// Generate embeddings for multiple texts.
    /// </summary>
    public async Task<List<float[]>> GenerateBatchAsync(
        IEnumerable<string> texts,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<float[]>();
        var textList = texts.ToList();
        var completed = 0;

        foreach (var text in textList)
        {
            results.Add(await GenerateAsync(text, ct));
            completed++;
            progress?.Report(completed);
        }

        return results;
    }

    private float[] GenerateEmbedding(string text)
    {
        try
        {
            var tokens = Tokenize(text);
            var actualLength = Math.Min(tokens.Count, MaxSequenceLength);

            var inputIds = CreateInputTensor(tokens);
            var attentionMask = CreateAttentionMask(tokens.Count);
            var tokenTypeIds = CreateTokenTypeIds();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
            };

            using var results = _session!.Run(inputs);
            var output = results.First().AsTensor<float>();
            var dims = output.Dimensions.ToArray();

            var pooled = MeanPooling(output, actualLength, dims);
            return Normalize(pooled);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Embedding error: {ex.Message}[/]");
            return new float[_config.VectorSize];
        }
    }

    private List<int> Tokenize(string text)
    {
        var tokens = new List<int>();

        // Add [CLS]
        if (_vocabulary.TryGetValue("[CLS]", out var clsId))
            tokens.Add(clsId);

        // Tokenize text
        var words = Regex.Split(text.ToLowerInvariant(), @"(\W+)")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Take(MaxSequenceLength - 2);

        foreach (var word in words)
        {
            if (_vocabulary.TryGetValue(word, out var tokenId))
                tokens.Add(tokenId);
            else if (_vocabulary.TryGetValue("[UNK]", out var unkId))
                tokens.Add(unkId);
        }

        // Add [SEP]
        if (_vocabulary.TryGetValue("[SEP]", out var sepId))
            tokens.Add(sepId);

        return tokens;
    }

    private Tensor<long> CreateInputTensor(List<int> tokens)
    {
        var length = Math.Min(tokens.Count, MaxSequenceLength);
        var data = new long[MaxSequenceLength];

        for (int i = 0; i < length; i++)
            data[i] = tokens[i];

        var padId = _vocabulary.TryGetValue("[PAD]", out var id) ? id : 0;
        for (int i = length; i < MaxSequenceLength; i++)
            data[i] = padId;

        return new DenseTensor<long>(data.AsMemory(), new[] { 1, MaxSequenceLength });
    }

    private Tensor<long> CreateAttentionMask(int actualLength)
    {
        var length = Math.Min(actualLength, MaxSequenceLength);
        var data = new long[MaxSequenceLength];

        for (int i = 0; i < length; i++)
            data[i] = 1;

        return new DenseTensor<long>(data.AsMemory(), new[] { 1, MaxSequenceLength });
    }

    private Tensor<long> CreateTokenTypeIds()
    {
        var data = new long[MaxSequenceLength];
        return new DenseTensor<long>(data.AsMemory(), new[] { 1, MaxSequenceLength });
    }

    private float[] MeanPooling(Tensor<float> output, int actualLength, int[] dims)
    {
        if (dims.Length == 2)
        {
            var result = new float[dims[1]];
            for (int i = 0; i < dims[1]; i++)
                result[i] = output[0, i];
            return result;
        }

        var hiddenSize = dims[2];
        var pooled = new float[hiddenSize];
        var tokensToPool = Math.Min(actualLength, dims[1]);

        for (int t = 0; t < tokensToPool; t++)
        {
            for (int h = 0; h < hiddenSize; h++)
                pooled[h] += output[0, t, h];
        }

        if (tokensToPool > 0)
        {
            for (int h = 0; h < hiddenSize; h++)
                pooled[h] /= tokensToPool;
        }

        return pooled;
    }

    private float[] Normalize(float[] vector)
    {
        var sumOfSquares = vector.Sum(v => v * v);
        var magnitude = MathF.Sqrt(sumOfSquares);

        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _session?.Dispose();
        _initLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Embedding configuration.
/// </summary>
public class EmbeddingConfig
{
    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = "./Models/model.onnx";
    public string VocabPath { get; set; } = "./Models/vocab.txt";
    public int VectorSize { get; set; } = 384;
}
