using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.DocSummarizer.Config;

namespace Mostlylucid.DocSummarizer.Services.Onnx;

/// <summary>
///     ONNX-based embedding service - no external dependencies required
/// </summary>
public class OnnxEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly EmbeddingModelInfo _modelInfo;
    private readonly int _maxSequenceLength;
    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly OnnxModelDownloader _downloader;
    private readonly bool _verbose;

    public OnnxEmbeddingService(OnnxConfig config, bool verbose = false)
    {
        _modelInfo = OnnxModelRegistry.GetEmbeddingModel(config.EmbeddingModel, config.UseQuantized);
        _maxSequenceLength = Math.Min(config.MaxEmbeddingSequenceLength, _modelInfo.MaxSequenceLength);
        _downloader = new OnnxModelDownloader(config, verbose);
        _verbose = verbose;
    }

    /// <summary>
    ///     Embedding dimension for this model
    /// </summary>
    public int EmbeddingDimension => _modelInfo.EmbeddingDimension;

    /// <summary>
    ///     Initialize the model (downloads if needed)
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var paths = await _downloader.EnsureEmbeddingModelAsync(_modelInfo, ct);
            
            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            _session = new InferenceSession(paths.ModelPath, options);
            _tokenizer = new BertTokenizer(paths.VocabPath);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    ///     Generate embedding for text
    /// </summary>
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        
        if (_session == null || _tokenizer == null)
            throw new InvalidOperationException("Model not initialized");

        // Prepend instruction if model requires it
        if (_modelInfo.RequiresInstruction && !string.IsNullOrEmpty(_modelInfo.QueryInstruction))
            text = _modelInfo.QueryInstruction + text;

        // Tokenize
        var (inputIds, attentionMask, tokenTypeIds) = _tokenizer.Encode(text, _maxSequenceLength);

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        // Run inference
        using var results = _session.Run(inputs);
        
        // Get last_hidden_state output
        var output = results.First(r => r.Name == "last_hidden_state" || r.Name == "output_0");
        var outputTensor = output.AsTensor<float>();

        // Mean pooling with attention mask
        return MeanPool(outputTensor, attentionMask, _modelInfo.EmbeddingDimension);
    }

    /// <summary>
    ///     Generate embeddings for multiple texts
    /// </summary>
    public async Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await EmbedAsync(text, ct));
        }
        return results.ToArray();
    }

    private static float[] MeanPool(Tensor<float> hiddenStates, long[] attentionMask, int hiddenSize)
    {
        var result = new float[hiddenSize];
        var dims = hiddenStates.Dimensions.ToArray();
        var seqLen = (int)dims[1];
        
        float maskSum = attentionMask.Sum();
        if (maskSum == 0) maskSum = 1; // Avoid division by zero

        for (int h = 0; h < hiddenSize; h++)
        {
            float sum = 0;
            for (int s = 0; s < seqLen; s++)
            {
                if (attentionMask[s] == 1)
                    sum += hiddenStates[0, s, h];
            }
            result[h] = sum / maskSum;
        }

        // L2 normalize
        float norm = MathF.Sqrt(result.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < result.Length; i++)
                result[i] /= norm;
        }

        return result;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}

/// <summary>
///     Simple BERT WordPiece tokenizer
/// </summary>
public class BertTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private const int ClsTokenId = 101;  // [CLS]
    private const int SepTokenId = 102;  // [SEP]
    private const int PadTokenId = 0;    // [PAD]
    private const int UnkTokenId = 100;  // [UNK]

    public BertTokenizer(string vocabPath)
    {
        _vocab = File.ReadAllLines(vocabPath)
            .Select((word, index) => (word, index))
            .ToDictionary(x => x.word, x => x.index);
    }

    public (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Encode(string text, int maxLength)
    {
        var tokens = Tokenize(text.ToLowerInvariant()).ToList();
        
        // Truncate to fit special tokens
        if (tokens.Count > maxLength - 2)
            tokens = tokens.Take(maxLength - 2).ToList();

        // Add special tokens
        var inputIds = new List<long> { ClsTokenId };
        inputIds.AddRange(tokens.Select(t => (long)GetTokenId(t)));
        inputIds.Add(SepTokenId);

        // Pad to maxLength
        var padCount = maxLength - inputIds.Count;
        inputIds.AddRange(Enumerable.Repeat((long)PadTokenId, padCount));

        var attentionMask = inputIds.Select(id => id != PadTokenId ? 1L : 0L).ToArray();
        var tokenTypeIds = new long[maxLength]; // All zeros for single sentence

        return (inputIds.ToArray(), attentionMask, tokenTypeIds);
    }

    private IEnumerable<string> Tokenize(string text)
    {
        // Basic whitespace + punctuation tokenization
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            // Split on punctuation
            var cleanWord = word.Trim();
            foreach (var subword in WordPieceTokenize(cleanWord))
                yield return subword;
        }
    }

    private IEnumerable<string> WordPieceTokenize(string word)
    {
        if (string.IsNullOrEmpty(word))
            yield break;

        if (_vocab.ContainsKey(word))
        {
            yield return word;
            yield break;
        }

        int start = 0;
        while (start < word.Length)
        {
            int end = word.Length;
            string? curSubstr = null;

            while (start < end)
            {
                var substr = word[start..end];
                if (start > 0) substr = "##" + substr;

                if (_vocab.ContainsKey(substr))
                {
                    curSubstr = substr;
                    break;
                }
                end--;
            }

            if (curSubstr == null)
            {
                yield return "[UNK]";
                yield break;
            }

            yield return curSubstr;
            start = end;
        }
    }

    private int GetTokenId(string token) => 
        _vocab.GetValueOrDefault(token, UnkTokenId);
}
