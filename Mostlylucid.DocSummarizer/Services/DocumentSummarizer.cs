using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentSummarizer
{
    private readonly DoclingClient _docling;
    private readonly DocumentChunker _chunker;
    private readonly MapReduceSummarizer _mapReduce;
    private readonly RagSummarizer _rag;
    private readonly ProgressService _progress;
    private readonly bool _verbose;
    private readonly int _maxLlmParallelism;

    public DocumentSummarizer(
        string ollamaModel = "ministral-3:3b",
        string doclingUrl = "http://localhost:5001",
        string qdrantHost = "localhost",
        bool verbose = false,
        DoclingConfig? doclingConfig = null,
        ProcessingConfig? processingConfig = null)
    {
        _verbose = verbose;
        _progress = new ProgressService(verbose);
        _docling = new DoclingClient(doclingConfig ?? new DoclingConfig { BaseUrl = doclingUrl });
        _chunker = new DocumentChunker();
        
        var processing = processingConfig ?? new ProcessingConfig();
        _maxLlmParallelism = processing.MaxLlmParallelism > 0 
            ? processing.MaxLlmParallelism 
            : MapReduceSummarizer.DefaultMaxParallelism;
        
        var ollama = new OllamaService(ollamaModel);
        _mapReduce = new MapReduceSummarizer(ollama, verbose, _maxLlmParallelism);
        _rag = new RagSummarizer(ollama, qdrantHost, verbose, _maxLlmParallelism);
    }

    public async Task<DocumentSummary> SummarizeAsync(
        string filePath,
        SummarizationMode mode = SummarizationMode.MapReduce,
        string? focus = null)
    {
        var docId = Path.GetFileName(filePath);
        
        if (_verbose)
        {
            AnsiConsole.Write(new FigletText("DocSummarizer").Color(Color.Blue));
            _progress.Rule("Document Processing");
            _progress.Info($"Document: {docId}");
            _progress.Info($"Mode: {mode}");
            _progress.Info($"Timeout: {OllamaService.DefaultTimeout.TotalMinutes:F0} minutes per operation");
            if (!string.IsNullOrEmpty(focus)) _progress.Info($"Focus: {focus}");
            AnsiConsole.WriteLine();
        }

        // Check if it's already markdown
        string markdown;
        if (filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Reading markdown file...");
            Console.Out.Flush();
            markdown = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            // Always show conversion progress - this can take a long time
            Console.WriteLine($"Converting document with Docling...");
            Console.Out.Flush();
            
            markdown = await _progress.WithStatusAsync(
                $"Converting {docId} with Docling (timeout: 5 min)...",
                async () => await _docling.ConvertAsync(filePath));
            
            Console.WriteLine("Document converted to markdown");
        }

        // Chunk the document
        Console.WriteLine("Parsing document structure...");
        Console.Out.Flush();
        
        var chunks = await _progress.WithStatusAsync(
            "Parsing document structure...",
            () =>
            {
                var result = _chunker.ChunkByStructure(markdown);
                return Task.FromResult(result);
            });
        
        Console.WriteLine($"Created {chunks.Count} chunks from document structure");
        Console.WriteLine();

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
        _progress.Rule("Iterative Summarization");
        _progress.Warning("Iterative mode is slower and may lose context on long documents (>10 chunks)");
        
        // Simple iterative approach - not recommended for long documents
        var ollama = new OllamaService();
        var summary = "";
        var orderedChunks = chunks.OrderBy(c => c.Order).ToList();

        if (_verbose)
        {
            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[blue]Processing chunks sequentially[/]", maxValue: orderedChunks.Count);
                    
                    foreach (var chunk in orderedChunks)
                    {
                        task.Description = $"[blue]Processing: {(chunk.Heading.Length > 30 ? chunk.Heading[..27] + "..." : chunk.Heading)}[/]";
                        
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
                        task.Increment(1);
                    }
                });
        }
        else
        {
            foreach (var chunk in orderedChunks)
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
            }
        }

        _progress.Success("Iterative summarization complete");

        return new DocumentSummary(
            summary,
            [],
            [],
            new SummarizationTrace(docId, chunks.Count, chunks.Count, [], TimeSpan.Zero, 1.0, 0));
    }
}
