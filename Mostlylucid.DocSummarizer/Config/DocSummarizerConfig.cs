using System.Text.Json.Serialization;

namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
/// Configuration for the document summarizer
/// </summary>
public class DocSummarizerConfig
{
    /// <summary>
    /// Ollama configuration
    /// </summary>
    public OllamaConfig Ollama { get; set; } = new();

    /// <summary>
    /// Docling configuration
    /// </summary>
    public DoclingConfig Docling { get; set; } = new();

    /// <summary>
    /// Qdrant configuration
    /// </summary>
    public QdrantConfig Qdrant { get; set; } = new();

    /// <summary>
    /// Processing configuration
    /// </summary>
    public ProcessingConfig Processing { get; set; } = new();

    /// <summary>
    /// Output configuration
    /// </summary>
    public OutputConfig Output { get; set; } = new();

    /// <summary>
    /// Batch processing configuration
    /// </summary>
    public BatchConfig Batch { get; set; } = new();
}

/// <summary>
/// Ollama service configuration
/// </summary>
public class OllamaConfig
{
    /// <summary>
    /// Base URL for Ollama service
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use for generation. Default is ministral-3:3b for good quality.
    /// Models under 3B parameters (e.g., gemma3:1b) produce poor quality summaries.
    /// </summary>
    public string Model { get; set; } = "ministral-3:3b";

    /// <summary>
    /// Model to use for embeddings
    /// </summary>
    public string EmbedModel { get; set; } = "mxbai-embed-large";

    /// <summary>
    /// Temperature for generation
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Docling service configuration
/// </summary>
public class DoclingConfig
{
    /// <summary>
    /// Base URL for Docling service
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// PDF backend to use. Options: pypdfium2, dlparse_v1, dlparse_v2, dlparse_v4
    /// pypdfium2 is more compatible with various PDFs but may be slower.
    /// dlparse_v4 is faster but may fail on some PDFs with non-standard page dimensions.
    /// </summary>
    public string PdfBackend { get; set; } = "pypdfium2";
    
    /// <summary>
    /// Number of pages per chunk for split processing of large PDFs
    /// </summary>
    public int PagesPerChunk { get; set; } = 50;
    
    /// <summary>
    /// Maximum concurrent chunks when split processing PDFs
    /// </summary>
    public int MaxConcurrentChunks { get; set; } = 4;
    
    /// <summary>
    /// Enable split processing for large PDFs (better progress feedback)
    /// </summary>
    public bool EnableSplitProcessing { get; set; } = true;
}

/// <summary>
/// Qdrant service configuration
/// </summary>
public class QdrantConfig
{
    /// <summary>
    /// Qdrant host
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Qdrant REST port (use 6333 for REST API, not 6334 which is gRPC)
    /// </summary>
    public int Port { get; set; } = 6333;

    /// <summary>
    /// Qdrant API key (optional, only if your Qdrant instance requires authentication)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Collection name for documents
    /// </summary>
    public string CollectionName { get; set; } = "documents";

    /// <summary>
    /// Vector size for embeddings
    /// </summary>
    public int VectorSize { get; set; } = 768;

    /// <summary>
    /// Delete the Qdrant collection after summarization completes.
    /// Default is true to prevent stale data and collection name collisions.
    /// Set to false if you want to reuse the index for subsequent queries.
    /// </summary>
    public bool DeleteCollectionAfterSummarization { get; set; } = true;
}

/// <summary>
/// Processing configuration
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// Maximum heading level to use for chunking (1-6).
    /// Default is 2 (split on H1 and H2 only). Lower values = fewer, larger chunks.
    /// </summary>
    public int MaxHeadingLevel { get; set; } = 2;

    /// <summary>
    /// Target chunk size in tokens. Default 0 means auto-calculate based on model context window.
    /// Small sections will be merged until they approach this size.
    /// </summary>
    public int TargetChunkTokens { get; set; } = 0;

    /// <summary>
    /// Minimum chunk size in tokens before merging with adjacent sections.
    /// Default 0 means auto-calculate (1/8 of target).
    /// </summary>
    public int MinChunkTokens { get; set; } = 0;

    /// <summary>
    /// Number of chunks to retrieve in RAG mode
    /// </summary>
    public int RagTopK { get; set; } = 3;
    
    /// <summary>
    /// Maximum parallel LLM requests. Ollama processes one request at a time per model,
    /// so high values just queue requests. 8 is a good balance for throughput vs memory.
    /// </summary>
    public int MaxLlmParallelism { get; set; } = 8;

    /// <summary>
    /// Number of topics to extract in RAG mode
    /// </summary>
    public int RagMaxTopics { get; set; } = 8;

    /// <summary>
    /// Enable parallel processing where possible
    /// </summary>
    public bool EnableParallel { get; set; } = true;

    /// <summary>
    /// Maximum degree of parallelism
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1; // Use system default
}

/// <summary>
/// Output configuration
/// </summary>
public class OutputConfig
{
    /// <summary>
    /// Output format: console, text, markdown, json
    /// </summary>
    public OutputFormat Format { get; set; } = OutputFormat.Console;

    /// <summary>
    /// Output directory for file outputs
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Include trace information in output
    /// </summary>
    public bool IncludeTrace { get; set; } = true;

    /// <summary>
    /// Include topic summaries in output
    /// </summary>
    public bool IncludeTopics { get; set; } = true;

    /// <summary>
    /// Include open questions in output
    /// </summary>
    public bool IncludeOpenQuestions { get; set; } = true;

    /// <summary>
    /// Verbose output
    /// </summary>
    public bool Verbose { get; set; } = false;
}

/// <summary>
/// Batch processing configuration
/// </summary>
public class BatchConfig
{
    /// <summary>
    /// File extensions to process (e.g., ".pdf", ".docx", ".md")
    /// </summary>
    public List<string> FileExtensions { get; set; } = new() { ".pdf", ".docx", ".md" };

    /// <summary>
    /// Recursive directory processing
    /// </summary>
    public bool Recursive { get; set; } = false;

    /// <summary>
    /// File name patterns to include (glob patterns)
    /// </summary>
    public List<string> IncludePatterns { get; set; } = new() { "*" };

    /// <summary>
    /// File name patterns to exclude (glob patterns)
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>
    /// Continue on error
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Create summary index file
    /// </summary>
    public bool CreateIndex { get; set; } = true;
}

/// <summary>
/// Output format enumeration
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OutputFormat>))]
public enum OutputFormat
{
    /// <summary>
    /// Console output with formatting
    /// </summary>
    Console,

    /// <summary>
    /// Plain text file
    /// </summary>
    Text,

    /// <summary>
    /// Markdown file
    /// </summary>
    Markdown,

    /// <summary>
    /// JSON file
    /// </summary>
    Json
}
