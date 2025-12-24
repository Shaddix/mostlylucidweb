using Microsoft.Extensions.Options;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services.Onnx;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Main implementation of <see cref="IDocumentSummarizer"/> for the library.
/// Wraps the internal DocumentSummarizer with a clean, DI-friendly API.
/// </summary>
public class DocumentSummarizerService : IDocumentSummarizer, IDisposable
{
    private readonly DocSummarizerConfig _config;
    private readonly IVectorStore? _vectorStore;
    private readonly bool _verbose;
    
    // Lazy-initialized services
    private OllamaService? _ollama;
    private DoclingClient? _docling;
    private BertRagSummarizer? _bertRag;
    private OnnxEmbeddingService? _embedder;
    private WebFetcher? _webFetcher;
    private bool _initialized;
    private readonly object _initLock = new();
    
    /// <inheritdoc />
    public SummaryTemplate Template { get; set; }

    public DocumentSummarizerService(
        IOptions<DocSummarizerConfig> config,
        IVectorStore? vectorStore = null)
    {
        _config = config.Value;
        _vectorStore = vectorStore;
        _verbose = _config.Output.Verbose;
        Template = SummaryTemplate.Presets.Default;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        
        lock (_initLock)
        {
            if (_initialized) return;
            
            // Initialize Ollama client
            var ollamaConfig = _config.Ollama;
            _ollama = new OllamaService(
                model: ollamaConfig.Model,
                embedModel: ollamaConfig.EmbedModel,
                baseUrl: ollamaConfig.BaseUrl,
                timeout: TimeSpan.FromSeconds(ollamaConfig.TimeoutSeconds),
                embeddingConfig: _config.Embedding,
                classifierModel: ollamaConfig.ClassifierModel);
            
            // Initialize Docling client if configured
            if (!string.IsNullOrEmpty(_config.Docling?.BaseUrl))
            {
                _docling = new DoclingClient(_config.Docling);
            }
            
            // Initialize embedding service
            _embedder = new OnnxEmbeddingService(_config.Onnx, _verbose);
            
            // Initialize BERT-RAG summarizer with converted configs
            _bertRag = new BertRagSummarizer(
                _config.Onnx,
                _ollama,
                _config.Extraction.ToExtractionConfig(),
                _config.Retrieval.ToRetrievalConfig(),
                Template,
                _verbose,
                _vectorStore,
                _config.BertRag);
            
            // Initialize web fetcher
            _webFetcher = new WebFetcher(_config.WebFetch);
            
            _initialized = true;
        }
    }

    /// <inheritdoc />
    public async Task<DocumentSummary> SummarizeMarkdownAsync(
        string markdown,
        string? documentId = null,
        string? focusQuery = null,
        SummarizationMode mode = SummarizationMode.Auto,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var docId = documentId ?? ComputeDocumentId(markdown);
        
        // For BertRag mode (default/auto), use the BERT-RAG pipeline
        if (mode == SummarizationMode.Auto || mode == SummarizationMode.BertRag)
        {
            _bertRag!.SetTemplate(Template);
            return await _bertRag.SummarizeAsync(docId, markdown, focusQuery, ContentType.Unknown, cancellationToken);
        }
        
        // For other modes, we'd need the full DocumentSummarizer
        // For now, fall back to BertRag
        _bertRag!.SetTemplate(Template);
        return await _bertRag.SummarizeAsync(docId, markdown, focusQuery, ContentType.Unknown, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DocumentSummary> SummarizeFileAsync(
        string filePath,
        string? focusQuery = null,
        SummarizationMode mode = SummarizationMode.Auto,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string markdown;
        
        // Handle different file types
        switch (ext)
        {
            case ".md":
            case ".txt":
                markdown = await File.ReadAllTextAsync(filePath, cancellationToken);
                break;
                
            case ".pdf":
            case ".docx":
                if (_docling == null)
                {
                    throw new InvalidOperationException(
                        "Docling is not configured. PDF/DOCX conversion requires Docling service.");
                }
                markdown = await _docling.ConvertAsync(filePath, cancellationToken);
                break;
                
            case ".html":
            case ".htm":
                var html = await File.ReadAllTextAsync(filePath, cancellationToken);
                markdown = HtmlToMarkdown(html);
                break;
                
            default:
                throw new NotSupportedException($"File type '{ext}' is not supported.");
        }
        
        var docId = Path.GetFileNameWithoutExtension(filePath);
        return await SummarizeMarkdownAsync(markdown, docId, focusQuery, mode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DocumentSummary> SummarizeUrlAsync(
        string url,
        string? focusQuery = null,
        SummarizationMode mode = SummarizationMode.Auto,
        bool usePlaywright = false,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var fetchMode = usePlaywright ? WebFetchMode.Playwright : WebFetchMode.Simple;
        using var result = await _webFetcher!.FetchAsync(url, fetchMode);
        
        // Read the content from the temp file
        string content;
        if (result.IsHtmlContent)
        {
            var html = await File.ReadAllTextAsync(result.TempFilePath, cancellationToken);
            content = HtmlToMarkdown(html);
        }
        else
        {
            content = await File.ReadAllTextAsync(result.TempFilePath, cancellationToken);
        }
        
        var docId = new Uri(url).Host;
        return await SummarizeMarkdownAsync(content, docId, focusQuery, mode, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueryAnswer> QueryAsync(
        string markdown,
        string question,
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var docId = documentId ?? ComputeDocumentId(markdown);
        
        // Use BertRag with the question as focus query to get segments first
        var (extraction, retrieved) = await _bertRag!.ExtractAndRetrieveAsync(
            docId, markdown, question, ContentType.Unknown, cancellationToken);
        
        // Now summarize with the focus query
        var result = await _bertRag.SummarizeAsync(docId, markdown, question, ContentType.Unknown, cancellationToken);
        
        // Convert retrieved segments to evidence format
        var evidence = retrieved
            .Take(5)
            .Select(s => new EvidenceSegment(
                s.Id,
                s.Text,
                s.RetrievalScore,
                s.SectionTitle))
            .ToList();
        
        return new QueryAnswer(
            Answer: result.ExecutiveSummary,
            Confidence: ConfidenceLevel.Medium, // Could be computed from scores
            Evidence: evidence,
            Question: question);
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ExtractSegmentsAsync(
        string markdown,
        string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var docId = documentId ?? ComputeDocumentId(markdown);
        
        // Use the extract-and-retrieve method to get segments
        var (extraction, _) = await _bertRag!.ExtractAndRetrieveAsync(
            docId, markdown, null, ContentType.Unknown, cancellationToken);
        
        return extraction;
    }

    /// <inheritdoc />
    public async Task<ServiceAvailability> CheckServicesAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        
        var ollamaAvailable = false;
        var doclingAvailable = false;
        string? ollamaModel = null;
        
        try
        {
            ollamaAvailable = await _ollama!.IsAvailableAsync();
            if (ollamaAvailable)
            {
                ollamaModel = _config.Ollama.Model;
            }
        }
        catch
        {
            // Ollama not available
        }
        
        try
        {
            if (_docling != null)
            {
                doclingAvailable = await _docling.IsAvailableAsync();
            }
        }
        catch
        {
            // Docling not available
        }
        
        return new ServiceAvailability(
            OllamaAvailable: ollamaAvailable,
            DoclingAvailable: doclingAvailable,
            OllamaModel: ollamaModel,
            EmbeddingReady: _embedder != null);
    }

    private static string ComputeDocumentId(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string HtmlToMarkdown(string html)
    {
        // Simple HTML to markdown conversion
        // In production, use a proper library like ReverseMarkdown
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    public void Dispose()
    {
        _docling?.Dispose();
        _bertRag?.Dispose();
        _embedder?.Dispose();
    }
}
