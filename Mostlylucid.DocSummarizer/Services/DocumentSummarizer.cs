using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentSummarizer
{
    private readonly DoclingClient _docling;
    private readonly DocumentChunker _chunker;
    private readonly MapReduceSummarizer _mapReduce;
    private readonly RagSummarizer _rag;
    private readonly bool _verbose;

    public DocumentSummarizer(
        string ollamaModel = "llama3.2:3b",
        string doclingUrl = "http://localhost:5001",
        string qdrantHost = "localhost",
        bool verbose = false)
    {
        _verbose = verbose;
        _docling = new DoclingClient(doclingUrl);
        _chunker = new DocumentChunker();
        
        var ollama = new OllamaService(ollamaModel);
        _mapReduce = new MapReduceSummarizer(ollama, verbose);
        _rag = new RagSummarizer(ollama, qdrantHost, verbose);
    }

    public async Task<DocumentSummary> SummarizeAsync(
        string filePath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        var docId = Path.GetFileName(filePath);
        
        if (_verbose) Console.WriteLine($"[Ingest] Converting {docId} with Docling...");

        // Check if it's already markdown
        string markdown;
        if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            markdown = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            markdown = await _docling.ConvertAsync(filePath);
        }

        if (_verbose) Console.WriteLine($"[Chunk] Parsing markdown...");
        var chunks = _chunker.ChunkByStructure(markdown);
        
        if (_verbose) Console.WriteLine($"[Chunk] Created {chunks.Count} chunks");

        return mode switch
        {
            SummarizationMode.MapReduce => await _mapReduce.SummarizeAsync(docId, chunks),
            SummarizationMode.Rag => await _rag.SummarizeAsync(docId, chunks, focus),
            SummarizationMode.Iterative => await SummarizeIterativeAsync(docId, chunks),
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
    }

    public async Task<string> QueryAsync(string filePath, string query)
    {
        var docId = Path.GetFileName(filePath);
        
        // Check if already markdown
        string markdown;
        if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            markdown = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            markdown = await _docling.ConvertAsync(filePath);
        }

        var chunks = _chunker.ChunkByStructure(markdown);
        
        // Index and query
        await _rag.IndexDocumentAsync(docId, chunks);
        
        var summary = await _rag.SummarizeAsync(docId, chunks, query);
        return summary.ExecutiveSummary;
    }

    private async Task<DocumentSummary> SummarizeIterativeAsync(string docId, List<DocumentChunk> chunks)
    {
        // Simple iterative approach - not recommended for long documents
        var ollama = new OllamaService();
        var summary = "";

        foreach (var chunk in chunks.OrderBy(c => c.Order))
        {
            var prompt = summary.Length == 0
                ? $"Summarize this section:\n\n{chunk.Content}\n\nSummary:"
                : $"""
                    Current summary:
                    {summary}
                    
                    New section: {chunk.Heading}
                    {chunk.Content}
                    
                    Update the summary to incorporate this section. Be concise.
                    
                    Updated summary:
                    """;

            summary = await ollama.GenerateAsync(prompt);
            
            if (_verbose) Console.WriteLine($"  Incorporated [{chunk.Id}] {chunk.Heading}");
        }

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunks.Count, chunks.Count, [], TimeSpan.Zero, 1.0, 0));
    }
}
