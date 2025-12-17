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
///     ONNX embedding configuration
/// </summary>
public class OnnxConfig
{
    /// <summary>
    ///     Directory to store downloaded models.
    ///     Default: ~/.docsummarizer/models
    /// </summary>
    public string ModelDirectory { get; set; } = GetDefaultModelDirectory();
    
    /// <summary>
    ///     Embedding model to use
    /// </summary>
    public OnnxEmbeddingModel EmbeddingModel { get; set; } = OnnxEmbeddingModel.AllMiniLmL6V2;
    
    /// <summary>
    ///     Use quantized models (smaller, faster, slightly lower quality)
    /// </summary>
    public bool UseQuantized { get; set; } = true;
    
    /// <summary>
    ///     Maximum sequence length for embedding
    /// </summary>
    public int MaxEmbeddingSequenceLength { get; set; } = 256;
    
    /// <summary>
    ///     Number of threads for ONNX inference (0 = auto)
    /// </summary>
    public int InferenceThreads { get; set; } = 0;
    
    private static string GetDefaultModelDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".docsummarizer", "models");
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
    ///     gte-small: 384 dims, 512 seq, ~34MB quantized. Good all-around.
    /// </summary>
    GteSmall,
    
    /// <summary>
    ///     multi-qa-MiniLM-L6-cos-v1: 384 dims, 512 seq, ~23MB quantized. QA-optimized.
    /// </summary>
    MultiQaMiniLm,
    
    /// <summary>
    ///     paraphrase-MiniLM-L3-v2: 384 dims, 128 seq, ~17MB quantized. Smallest/fastest.
    /// </summary>
    ParaphraseMiniLmL3
}
