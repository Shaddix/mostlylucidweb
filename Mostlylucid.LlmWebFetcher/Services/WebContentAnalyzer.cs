using System.Diagnostics;
using System.Text;
using Mostlylucid.LlmWebFetcher.Helpers;
using Mostlylucid.LlmWebFetcher.Models;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.LlmWebFetcher.Services;

/// <summary>
/// Analyzes web content using a local LLM via Ollama.
/// Fetches, cleans, chunks, and queries content.
/// </summary>
public class WebContentAnalyzer : IDisposable
{
    private readonly WebFetcher _fetcher;
    private readonly HtmlCleaner _cleaner;
    private readonly ContentChunker _chunker;
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly bool _verbose;
    private bool _disposed;
    
    public WebContentAnalyzer(
        string model = "llama3.2:3b",
        string ollamaUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _fetcher = new WebFetcher();
        _cleaner = new HtmlCleaner();
        _chunker = new ContentChunker();
        _ollama = new OllamaApiClient(new Uri(ollamaUrl));
        _model = model;
        _verbose = verbose;
    }
    
    /// <summary>
    /// Analyzes a URL to answer a specific question.
    /// </summary>
    /// <param name="url">URL to analyze.</param>
    /// <param name="question">Question to answer.</param>
    /// <param name="maxChunks">Maximum number of chunks to include in context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis result with answer.</returns>
    public async Task<AnalysisResult> AnalyzeAsync(
        string url, 
        string question,
        int maxChunks = 3,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AnalysisResult { Url = url, Question = question };
        
        try
        {
            // Step 1: Fetch
            if (_verbose) Console.WriteLine($"Fetching {url}...");
            var page = await _fetcher.FetchAsync(url, cancellationToken);
            
            // Step 2: Clean
            if (_verbose) Console.WriteLine("Cleaning HTML...");
            var cleanText = _cleaner.Clean(page.Html);
            
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                result.Success = false;
                result.Error = "No content extracted from page";
                return result;
            }
            
            // Step 3: Chunk
            if (_verbose) Console.WriteLine("Chunking content...");
            var chunks = _chunker.ChunkBySentence(cleanText, maxTokens: 2000);
            
            // Step 4: Select relevant chunks
            if (_verbose) Console.WriteLine($"Selecting from {chunks.Count} chunks...");
            var relevantChunks = _chunker.FilterByKeywords(chunks, question, topK: maxChunks);
            
            // Step 5: Build prompt
            var prompt = BuildQuestionPrompt(url, relevantChunks, question);
            result.ChunksUsed = relevantChunks.Count;
            result.TokensUsed = ContentChunker.EstimateTokens(prompt);
            
            // Step 6: Query LLM
            if (_verbose) Console.WriteLine("Querying LLM...");
            var request = new GenerateRequest { Model = _model, Prompt = prompt };
            var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
            
            result.Answer = response.Response.Trim();
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        stopwatch.Stop();
        result.AnalysisTime = stopwatch.Elapsed;
        
        return result;
    }
    
    /// <summary>
    /// Summarizes the content at a URL.
    /// </summary>
    public async Task<string> SummarizeAsync(
        string url, 
        int sentences = 3,
        CancellationToken cancellationToken = default)
    {
        if (_verbose) Console.WriteLine($"Fetching {url}...");
        var page = await _fetcher.FetchAsync(url, cancellationToken);
        
        if (_verbose) Console.WriteLine("Cleaning HTML...");
        var cleanText = _cleaner.Clean(page.Html);
        
        // Limit content size for summarization
        if (cleanText.Length > 10000)
            cleanText = cleanText[..10000] + "...";
        
        var prompt = $@"Summarize the following content in {sentences} sentences or less.
Focus on the main points only. Be concise.

Content:
{cleanText}

Summary:";

        if (_verbose) Console.WriteLine("Generating summary...");
        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        return response.Response.Trim();
    }
    
    /// <summary>
    /// Extracts key facts from a URL as bullet points.
    /// </summary>
    public async Task<List<string>> ExtractFactsAsync(
        string url,
        int maxFacts = 10,
        CancellationToken cancellationToken = default)
    {
        var page = await _fetcher.FetchAsync(url, cancellationToken);
        var cleanText = _cleaner.Clean(page.Html);
        
        if (cleanText.Length > 8000)
            cleanText = cleanText[..8000] + "...";
        
        var prompt = $@"Extract up to {maxFacts} key facts from this content.
Format: One fact per line, starting with '-'
Be specific and factual.

Content:
{cleanText}

Facts:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        return response.Response
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('-'))
            .Select(line => line.TrimStart('-').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxFacts)
            .ToList();
    }
    
    /// <summary>
    /// Classifies content into one of the provided categories.
    /// </summary>
    public async Task<string> ClassifyAsync(
        string url,
        string[] categories,
        CancellationToken cancellationToken = default)
    {
        var page = await _fetcher.FetchAsync(url, cancellationToken);
        var cleanText = _cleaner.Clean(page.Html);
        
        // Use just the beginning for classification
        if (cleanText.Length > 3000)
            cleanText = cleanText[..3000];
        
        var categoryList = string.Join(", ", categories);
        
        var prompt = $@"Classify this content into exactly ONE of these categories:
{categoryList}

Respond with ONLY the category name, nothing else.

Content:
{cleanText}

Category:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        var result = response.Response.Trim();
        
        // Validate against allowed categories
        return categories.FirstOrDefault(c => 
            result.Contains(c, StringComparison.OrdinalIgnoreCase)) ?? "Unknown";
    }
    
    private static string BuildQuestionPrompt(string url, List<ContentChunk> chunks, string question)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are analyzing web content to answer a question.");
        sb.AppendLine("IMPORTANT RULES:");
        sb.AppendLine("- Answer ONLY using the content provided below");
        sb.AppendLine("- If the answer is not in the content, say 'Not enough information'");
        sb.AppendLine("- Quote relevant parts when possible");
        sb.AppendLine("- Be concise and direct");
        sb.AppendLine();
        
        for (int i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine($"=== CONTENT SECTION {i + 1} ===");
            sb.AppendLine($"Source: {url}");
            if (!string.IsNullOrEmpty(chunks[i].Heading))
                sb.AppendLine($"Section: {chunks[i].Heading}");
            sb.AppendLine(chunks[i].Content);
            sb.AppendLine();
        }
        
        sb.AppendLine($"Question: {question}");
        sb.AppendLine();
        sb.AppendLine("Answer (based only on the content above):");
        
        return sb.ToString();
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _fetcher.Dispose();
            _disposed = true;
        }
    }
}
