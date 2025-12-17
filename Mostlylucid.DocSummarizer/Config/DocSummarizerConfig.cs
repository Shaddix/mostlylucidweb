using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
///     Configuration for the document summarizer
/// </summary>
public class DocSummarizerConfig
{
    /// <summary>
    ///     Ollama configuration
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
    ///     Vector size for embeddings
    /// </summary>
    public int VectorSize { get; set; } = 768;

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
    ///     Browser executable path (optional)
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
