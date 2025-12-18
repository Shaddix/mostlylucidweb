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
    private readonly OnnxConfig _config;
    private InferenceSession? _session;
    private HuggingFaceTokenizer? _tokenizer;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly OnnxModelDownloader _downloader;
    private readonly bool _verbose;

    public OnnxEmbeddingService(OnnxConfig config, bool verbose = false)
    {
        _config = config;
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
            
            var options = CreateSessionOptions();

            _session = new InferenceSession(paths.ModelPath, options);
            
            if (ProgressService.ShouldShowVerbose(_verbose))
            {
                Console.WriteLine($"[ONNX] Model loaded: {_modelInfo.Name} ({_modelInfo.EmbeddingDimension}d)");
            }
            
            // Prefer tokenizer.json (universal format) with vocab.txt fallback
            _tokenizer = File.Exists(paths.TokenizerPath)
                ? HuggingFaceTokenizer.FromFile(paths.TokenizerPath)
                : HuggingFaceTokenizer.FromVocabFile(paths.VocabPath);
            
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
    ///     Generate embeddings for multiple texts with parallelism
    /// </summary>
    public async Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        
        var textList = texts.ToList();
        var results = new float[textList.Count][];
        
        // Process in parallel - ONNX runtime is thread-safe for inference
        // Use bounded parallelism to avoid memory issues
        var maxParallel = Math.Min(Environment.ProcessorCount, 8);
        
        await Parallel.ForEachAsync(
            textList.Select((text, index) => (text, index)),
            new ParallelOptions { MaxDegreeOfParallelism = maxParallel, CancellationToken = ct },
            async (item, token) =>
            {
                results[item.index] = await EmbedSingleAsync(item.text, token);
            });
        
        return results;
    }

    /// <summary>
    ///     Internal single embedding (no init check - called from batch)
    /// </summary>
    private Task<float[]> EmbedSingleAsync(string text, CancellationToken ct)
    {
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

        // Run inference (synchronous but called from parallel context)
        using var results = _session.Run(inputs);
        
        // Get last_hidden_state output
        var output = results.First(r => r.Name == "last_hidden_state" || r.Name == "output_0");
        var outputTensor = output.AsTensor<float>();

        // Mean pooling with attention mask
        return Task.FromResult(MeanPool(outputTensor, attentionMask, _modelInfo.EmbeddingDimension));
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
    
    private SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        
        if (_config.InferenceThreads > 0)
        {
            options.IntraOpNumThreads = _config.InferenceThreads;
        }
        
        // Configure execution provider based on config
        switch (_config.ExecutionProvider)
        {
            case OnnxExecutionProvider.Cuda:
                try
                {
                    options.AppendExecutionProvider_CUDA(_config.GpuDeviceId);
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] Using CUDA GPU device {_config.GpuDeviceId}");
                }
                catch (Exception ex)
                {
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] CUDA not available: {ex.Message}, falling back to CPU");
                }
                break;
                
            case OnnxExecutionProvider.DirectMl:
                try
                {
                    options.AppendExecutionProvider_DML(_config.GpuDeviceId);
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] Using DirectML GPU device {_config.GpuDeviceId}");
                }
                catch (Exception ex)
                {
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] DirectML not available: {ex.Message}, falling back to CPU");
                }
                break;
                
            case OnnxExecutionProvider.Auto:
                // Try DirectML first (has package installed), then CUDA, then CPU
                var gpuSelected = false;
                try
                {
                    options.AppendExecutionProvider_DML(_config.GpuDeviceId);
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] Auto-selected DirectML GPU device {_config.GpuDeviceId}");
                    gpuSelected = true;
                }
                catch (Exception dmlEx)
                {
                    ProgressService.WriteVerbose(_verbose, $"[ONNX] DirectML not available: {dmlEx.Message}");
                    try
                    {
                        options.AppendExecutionProvider_CUDA(_config.GpuDeviceId);
                        ProgressService.WriteVerbose(_verbose, $"[ONNX] Auto-selected CUDA GPU device {_config.GpuDeviceId}");
                        gpuSelected = true;
                    }
                    catch (Exception cudaEx)
                    {
                        ProgressService.WriteVerbose(_verbose, $"[ONNX] CUDA not available: {cudaEx.Message}");
                    }
                }
                if (!gpuSelected) ProgressService.WriteVerbose(_verbose, "[ONNX] No GPU available, using CPU");
                break;
                
            case OnnxExecutionProvider.Cpu:
            default:
                ProgressService.WriteVerbose(_verbose, "[ONNX] Using CPU");
                break;
        }
        
        return options;
    }
}


