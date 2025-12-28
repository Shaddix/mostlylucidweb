namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
///     Backend type for embedding operations
/// </summary>
public enum EmbeddingBackend
{
    /// <summary>
    ///     ONNX Runtime - zero-config, auto-downloads models, fast
    /// </summary>
    Onnx,
    
    /// <summary>
    ///     Ollama - requires Ollama server
    /// </summary>
    Ollama
}

/// <summary>
///     ONNX execution provider (GPU vs CPU)
/// </summary>
public enum OnnxExecutionProvider
{
    /// <summary>CPU only (default, always works)</summary>
    Cpu,
    /// <summary>NVIDIA CUDA GPU</summary>
    Cuda,
    /// <summary>DirectML (Windows GPU, works with AMD/Intel/NVIDIA)</summary>
    DirectMl,
    /// <summary>Auto-detect best available</summary>
    Auto
}

/// <summary>
///     ONNX embedding configuration
/// </summary>
public class OnnxConfig
{
    /// <summary>
    ///     Directory to store downloaded models.
    ///     Default: [app directory]/models (travels with the tool)
    /// </summary>
    public string ModelDirectory { get; set; } = GetDefaultModelDirectory();
    
    /// <summary>
    ///     Embedding model to use.
    ///     Default: BgeBaseEnV15 (768d) - best quality/speed balance.
    ///     Use BgeLargeEnV15 (1024d) for maximum quality, AllMiniLmL6V2 (384d) for speed.
    /// </summary>
    public OnnxEmbeddingModel EmbeddingModel { get; set; } = OnnxEmbeddingModel.BgeBaseEnV15;
    
    /// <summary>
    ///     Use quantized models (smaller, faster, slightly lower quality)
    /// </summary>
    public bool UseQuantized { get; set; } = true;
    
    /// <summary>
    ///     Maximum sequence length for embedding.
    ///     Default: 512 (matches BGE-base). Increase for Jina/Nomic (8192).
    /// </summary>
    public int MaxEmbeddingSequenceLength { get; set; } = 512;
    
    /// <summary>
    ///     Number of threads for ONNX inference (0 = auto)
    /// </summary>
    public int InferenceThreads { get; set; } = 0;
    
    /// <summary>
    ///     Execution provider for ONNX Runtime.
    ///     Cpu = CPU only (always works, default for stability)
    ///     Cuda = NVIDIA GPU (requires CUDA runtime)
    ///     DirectMl = Windows GPU (AMD/Intel/NVIDIA - may crash on some systems)
    ///     Auto = Try DirectML first, then CUDA, fall back to CPU
    /// </summary>
    public OnnxExecutionProvider ExecutionProvider { get; set; } = OnnxExecutionProvider.Cpu;
    
    /// <summary>
    ///     GPU device ID (0 = first GPU, 1 = second GPU, etc.)
    ///     Use this to select NVIDIA GPU when you have integrated graphics.
    ///     Run 'nvidia-smi -L' to list GPU IDs.
    /// </summary>
    public int GpuDeviceId { get; set; } = 0;
    
    /// <summary>
    ///     Batch size for embedding inference. Higher = more throughput but more memory.
    ///     Default: 32. Set to 1 for sequential processing.
    /// </summary>
    public int EmbeddingBatchSize { get; set; } = 32;
    
    /// <summary>
    ///     Enable parallel execution mode in ONNX Runtime.
    ///     True = better throughput for batched inference on multi-core CPUs.
    ///     False = sequential (lower latency for single embeddings).
    /// </summary>
    public bool UseParallelExecution { get; set; } = true;
    
    /// <summary>
    ///     Number of threads for inter-op parallelism (between graph nodes).
    ///     0 = auto (uses ProcessorCount). Only applies when UseParallelExecution = true.
    /// </summary>
    public int InterOpThreads { get; set; } = 0;
    
    private static string GetDefaultModelDirectory()
    {
        // Use app directory/models so models travel with the tool
        var appDir = AppContext.BaseDirectory;
        return Path.Combine(appDir, "models");
    }
}

/// <summary>
///     Available ONNX embedding models
/// </summary>
public enum OnnxEmbeddingModel
{
    /// <summary>
    ///     all-MiniLM-L6-v2: 384 dims, 256 seq, ~23MB quantized. Fast general-purpose.
    /// </summary>
    AllMiniLmL6V2,

    /// <summary>
    ///     bge-small-en-v1.5: 384 dims, 512 seq, ~34MB quantized. Best quality for size.
    /// </summary>
    BgeSmallEnV15,

    /// <summary>
    ///     bge-base-en-v1.5: 768 dims, 512 seq, ~110MB quantized. High quality.
    /// </summary>
    BgeBaseEnV15,

    /// <summary>
    ///     bge-large-en-v1.5: 1024 dims, 512 seq, ~335MB quantized. Best BGE quality.
    /// </summary>
    BgeLargeEnV15,

    /// <summary>
    ///     gte-small: 384 dims, 512 seq, ~34MB quantized. Good all-around.
    /// </summary>
    GteSmall,

    /// <summary>
    ///     gte-base: 768 dims, 512 seq, ~110MB. Strong performer on MTEB.
    /// </summary>
    GteBase,

    /// <summary>
    ///     gte-large: 1024 dims, 512 seq, ~335MB. Top-tier quality.
    /// </summary>
    GteLarge,

    /// <summary>
    ///     multi-qa-MiniLM-L6-cos-v1: 384 dims, 512 seq, ~23MB quantized. QA-optimized.
    /// </summary>
    MultiQaMiniLm,

    /// <summary>
    ///     paraphrase-MiniLM-L3-v2: 384 dims, 128 seq, ~17MB quantized. Smallest/fastest.
    /// </summary>
    ParaphraseMiniLmL3,

    /// <summary>
    ///     jina-embeddings-v2-base-en: 768 dims, 8192 seq, ~137MB. Long context specialist.
    /// </summary>
    JinaEmbeddingsV2BaseEn,

    /// <summary>
    ///     snowflake-arctic-embed-m: 768 dims, 512 seq, ~110MB. Top MTEB retrieval.
    /// </summary>
    SnowflakeArcticEmbedM,

    /// <summary>
    ///     nomic-embed-text-v1.5: 768 dims, 8192 seq, ~137MB. Long context, Matryoshka support.
    /// </summary>
    NomicEmbedTextV15
}
