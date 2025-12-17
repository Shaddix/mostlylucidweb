using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
///     Configuration for the document summarizer
/// </summary>
public class DocSummarizerConfig
{
    /// <summary>
    ///     Embedding backend (Onnx = fast/zero-config, Ollama = uses Ollama server)
    /// </summary>
    public EmbeddingBackend EmbeddingBackend { get; set; } = EmbeddingBackend.Onnx;

    /// <summary>
    ///     ONNX configuration (used when Backend = Onnx)
    /// </summary>
    public OnnxConfig Onnx { get; set; } = new();

    /// <summary>
    ///     Ollama configuration (used when Backend = Ollama)
    /// </summary>
    public OllamaConfig Ollama { get; set; } = new();

    /// <summary>
    ///     Docling configuration
    /// </summary>
    public DoclingConfig Docling { get; set; } = new();

    /// <summary>
    ///     Qdrant configuration
    /// </summary>
    public QdrantConfig Qdrant { get; set; } = new();

    /// <summary>
    ///     Processing configuration
    /// </summary>
    public ProcessingConfig Processing { get; set; } = new();

    /// <summary>
    ///     Output configuration
    /// </summary>
    public OutputConfig Output { get; set; } = new();

    /// <summary>
    ///     Web fetch configuration
    /// </summary>
    public WebFetchConfig WebFetch { get; set; } = new();

    /// <summary>
    ///     Batch processing configuration
    /// </summary>
    public BatchConfig Batch { get; set; } = new();

    /// <summary>
    ///     Embedding resilience configuration
    /// </summary>
    public EmbeddingConfig Embedding { get; set; } = new();
}

/// <summary>
///     Embedding service resilience configuration
/// </summary>
public class EmbeddingConfig
{
    /// <summary>
    ///     Maximum requests per second to Ollama embedding endpoint.
    ///     Ollama processes embeddings sequentially, so this prevents overwhelming it.
    ///     Default is 2 requests/second.
    /// </summary>
    public double RequestsPerSecond { get; set; } = 2.0;

    /// <summary>
    ///     Maximum retry attempts for failed embedding requests.
    ///     Default is 5 retries.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    ///     Initial delay before first retry in milliseconds.
    ///     Uses exponential backoff: delay * 2^attempt.
    ///     Default is 1000ms (1 second).
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    ///     Maximum delay between retries in milliseconds.
    ///     Default is 30000ms (30 seconds).
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;

    /// <summary>
    ///     Delay between embedding requests in milliseconds.
    ///     Added on top of rate limiting for extra stability.
    ///     Default is 100ms.
    /// </summary>
    public int DelayBetweenRequestsMs { get; set; } = 100;

    /// <summary>
    ///     Enable circuit breaker to fail fast after repeated failures.
    ///     Default is true.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    ///     Number of consecutive failures before opening circuit.
    ///     Default is 3.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>
    ///     How long to keep circuit open before trying again in seconds.
    ///     Default is 30 seconds.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}

/// <summary>
///     Ollama service configuration
/// </summary>
public class OllamaConfig
{
    /// <summary>
    ///     Base URL for Ollama service
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    ///     Model to use for generation
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Model to use for embeddings
    /// </summary>
    public string EmbedModel { get; set; } = "nomic-embed-text";

    /// <summary>
    ///     Temperature for generation
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    ///     Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 1200;
}

/// <summary>
///     Docling service configuration
/// </summary>
public class DoclingConfig
{
    /// <summary>
    ///     Docling service base URL
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>
    ///     Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 1200;

    /// <summary>
    ///     Enable split processing for large PDFs
    /// </summary>
    public bool EnableSplitProcessing { get; set; } = true;

    /// <summary>
    ///     Pages per chunk for split processing
    /// </summary>
    public int PagesPerChunk { get; set; } = 10;

    /// <summary>
    ///     Maximum concurrent chunks
    /// </summary>
    public int MaxConcurrentChunks { get; set; } = 4;

    /// <summary>
    ///     PDF backend to use
    /// </summary>
    public string PdfBackend { get; set; } = "pypdfium2";
}

/// <summary>
///     Qdrant service configuration
/// </summary>
public class QdrantConfig
{
    /// <summary>
    ///     Qdrant host
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    ///     Qdrant REST port
    /// </summary>
    public int Port { get; set; } = 6333;

    /// <summary>
    ///     Qdrant API key (optional)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Collection name for documents
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    ///     Vector size for embeddings (384 for ONNX all-MiniLM, 768 for Ollama nomic-embed-text)
    /// </summary>
    public int VectorSize { get; set; } = 384;

    /// <summary>
    ///     Delete collection after summarization
    /// </summary>
    public bool DeleteCollectionAfterSummarization { get; set; } = true;
}

/// <summary>
///     Processing configuration
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    ///     Maximum heading level for chunking
    /// </summary>
    public int MaxHeadingLevel { get; set; } = 2;

    /// <summary>
    ///     Maximum chunk size in tokens
    /// </summary>
    public int MaxChunkSize { get; set; } = 2000;

    /// <summary>
    ///     Chunk overlap in tokens
    /// </summary>
    public int ChunkOverlap { get; set; } = 100;

    /// <summary>
    ///     Maximum concurrent chunks
    /// </summary>
    public int MaxConcurrentChunks { get; set; } = 4;

    /// <summary>
    ///     Enable split processing
    /// </summary>
    public bool EnableSplitProcessing { get; set; } = true;

    /// <summary>
    ///     Maximum LLM parallelism
    /// </summary>
    public int MaxLlmParallelism { get; set; } = 2;

    /// <summary>
    ///     Target chunk tokens
    /// </summary>
    public int TargetChunkTokens { get; set; } = 1500;

    /// <summary>
    ///     Minimum chunk tokens
    /// </summary>
    public int MinChunkTokens { get; set; } = 200;

    /// <summary>
    ///     Memory management settings
    /// </summary>
    public MemoryConfig Memory { get; set; } = new();
}

/// <summary>
///     Memory management configuration for large document processing
/// </summary>
public class MemoryConfig
{
    /// <summary>
    ///     Enable disk-backed chunk storage for large documents.
    ///     When enabled, chunk content is stored on disk instead of memory
    ///     when the document exceeds DiskStorageThreshold chunks.
    /// </summary>
    public bool EnableDiskStorage { get; set; } = true;

    /// <summary>
    ///     Number of chunks before switching to disk storage.
    ///     Default is 100 chunks (~400KB-1.6MB of content).
    /// </summary>
    public int DiskStorageThreshold { get; set; } = 100;

    /// <summary>
    ///     Use streaming chunker for markdown files larger than this size (in bytes).
    ///     Streaming processes line-by-line without loading entire file.
    ///     Default is 5MB.
    /// </summary>
    public long StreamingThresholdBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    ///     Batch size for embedding operations. Smaller batches use less memory
    ///     but may be slower. Default is 10.
    /// </summary>
    public int EmbeddingBatchSize { get; set; } = 10;

    /// <summary>
    ///     Trigger GC after processing this many chunks.
    ///     Set to 0 to disable periodic GC.
    /// </summary>
    public int GcIntervalChunks { get; set; } = 50;

    /// <summary>
    ///     Maximum memory usage in MB before forcing GC.
    ///     Set to 0 to disable memory-based GC triggers.
    /// </summary>
    public int MaxMemoryMB { get; set; } = 0;
}

/// <summary>
///     Output configuration
/// </summary>
public class OutputConfig
{
    /// <summary>
    ///     Output format
    /// </summary>
    public OutputFormat Format { get; set; } = OutputFormat.Console;

    /// <summary>
    ///     Output directory for file outputs
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    ///     Show detailed progress information
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    ///     Include processing trace in output
    /// </summary>
    public bool IncludeTrace { get; set; } = false;

    /// <summary>
    ///     Include topics in output
    /// </summary>
    public bool IncludeTopics { get; set; } = true;

    /// <summary>
    ///     Include open questions in output
    /// </summary>
    public bool IncludeOpenQuestions { get; set; } = false;

    /// <summary>
    ///     Include document structure/chunk index in output
    /// </summary>
    public bool IncludeChunkIndex { get; set; } = false;
}

/// <summary>
///     Web fetch mode - determines how web pages are fetched
/// </summary>
public enum WebFetchMode
{
    /// <summary>
    ///     Simple HTTP client fetch - fast but cannot execute JavaScript
    /// </summary>
    Simple,
    
    /// <summary>
    ///     Playwright headless browser - slower but handles JavaScript-rendered pages (SPAs, React apps)
    /// </summary>
    Playwright
}

/// <summary>
///     Web fetch configuration
/// </summary>
public class WebFetchConfig
{
    /// <summary>
    ///     Enable web fetching functionality
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Fetch mode: Simple (HTTP client) or Playwright (headless browser for JS pages).
    ///     When set to Playwright, Chromium browser will be auto-installed on first use.
    /// </summary>
    public WebFetchMode Mode { get; set; } = WebFetchMode.Simple;

    /// <summary>
    ///     Browser executable path (optional, for Playwright mode)
    /// </summary>
    public string? BrowserExecutablePath { get; set; }

    /// <summary>
    ///     Default timeout for web requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     User agent to use for web requests
    /// </summary>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 DocSummarizer/1.0";
}

/// <summary>
///     Batch processing configuration
/// </summary>
public class BatchConfig
{
    /// <summary>
    ///     File extensions to process
    /// </summary>
    public List<string> FileExtensions { get; set; } = new() 
    { 
        ".txt", ".md", ".pdf", ".docx", ".xlsx", ".pptx", 
        ".html", ".csv", ".png", ".jpg", ".tiff", ".vtt", ".adoc" 
    };

    /// <summary>
    ///     Process directories recursively
    /// </summary>
    public bool Recursive { get; set; } = false;

    /// <summary>
    ///     Maximum concurrent files to process
    /// </summary>
    public int MaxConcurrentFiles { get; set; } = 4;

    /// <summary>
    ///     Continue on error
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    ///     Include patterns for files
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new();

    /// <summary>
    ///     Exclude patterns for files
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new();
}
