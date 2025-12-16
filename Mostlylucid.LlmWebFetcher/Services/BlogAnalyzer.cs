using System.Diagnostics;
using System.Text;
using Mostlylucid.LlmWebFetcher.Helpers;
using Mostlylucid.LlmWebFetcher.Models;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.LlmWebFetcher.Services;

/// <summary>
/// Specialized analyzer for blog posts and articles.
/// Provides summary, key points, classification, and reading time.
/// </summary>
public class BlogAnalyzer : IDisposable
{
    private readonly WebFetcher _fetcher;
    private readonly HtmlCleaner _cleaner;
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly bool _verbose;
    private bool _disposed;
    
    private static readonly string[] DefaultCategories = 
    {
        "Technical Tutorial",
        "Opinion/Commentary",
        "News/Announcement",
        "Research/Analysis",
        "How-To Guide",
        "Review",
        "Case Study"
    };
    
    public BlogAnalyzer(
        string model = "llama3.2:3b",
        string ollamaUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _fetcher = new WebFetcher();
        _cleaner = new HtmlCleaner();
        _ollama = new OllamaApiClient(new Uri(ollamaUrl));
        _model = model;
        _verbose = verbose;
    }
    
    /// <summary>
    /// Performs a comprehensive analysis of a blog post.
    /// </summary>
    /// <param name="url">URL of the blog post.</param>
    /// <param name="categories">Optional custom categories for classification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete blog analysis.</returns>
    public async Task<BlogAnalysis> AnalyzeAsync(
        string url,
        string[]? categories = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        categories ??= DefaultCategories;
        
        if (_verbose) Console.WriteLine($"Fetching {url}...");
        var page = await _fetcher.FetchAsync(url, cancellationToken);
        
        if (_verbose) Console.WriteLine("Cleaning HTML...");
        var title = _cleaner.ExtractTitle(page.Html);
        var content = _cleaner.Clean(page.Html);
        
        // Calculate reading metrics
        var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var readingTime = Math.Max(1, wordCount / 200); // ~200 wpm average
        
        // Limit content for LLM processing
        var contentForAnalysis = content.Length > 10000 
            ? content[..10000] + "..." 
            : content;
        
        if (_verbose) Console.WriteLine("Generating summary...");
        var summary = await GetSummaryAsync(contentForAnalysis, cancellationToken);
        
        if (_verbose) Console.WriteLine("Extracting key points...");
        var keyPoints = await GetKeyPointsAsync(contentForAnalysis, cancellationToken);
        
        if (_verbose) Console.WriteLine("Classifying content...");
        var category = await ClassifyAsync(contentForAnalysis, categories, cancellationToken);
        
        stopwatch.Stop();
        
        return new BlogAnalysis
        {
            Url = url,
            Title = title,
            Summary = summary,
            KeyPoints = keyPoints,
            Category = category,
            WordCount = wordCount,
            ReadingTimeMinutes = readingTime,
            AnalysisTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Analyzes multiple blog posts.
    /// </summary>
    public async Task<List<BlogAnalysis>> AnalyzeManyAsync(
        IEnumerable<string> urls,
        string[]? categories = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BlogAnalysis>();
        
        foreach (var url in urls)
        {
            try
            {
                var analysis = await AnalyzeAsync(url, categories, cancellationToken);
                results.Add(analysis);
            }
            catch (Exception ex)
            {
                if (_verbose) Console.WriteLine($"Failed to analyze {url}: {ex.Message}");
                results.Add(new BlogAnalysis
                {
                    Url = url,
                    Summary = $"Error: {ex.Message}"
                });
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Compares two blog posts.
    /// </summary>
    public async Task<string> CompareAsync(
        string url1,
        string url2,
        CancellationToken cancellationToken = default)
    {
        var page1 = await _fetcher.FetchAsync(url1, cancellationToken);
        var page2 = await _fetcher.FetchAsync(url2, cancellationToken);
        
        var content1 = _cleaner.Clean(page1.Html);
        var content2 = _cleaner.Clean(page2.Html);
        
        // Limit content size
        if (content1.Length > 5000) content1 = content1[..5000] + "...";
        if (content2.Length > 5000) content2 = content2[..5000] + "...";
        
        var prompt = $@"Compare these two articles and highlight their key differences and similarities.
Be specific and concise.

=== ARTICLE 1 ===
Source: {url1}
{content1}

=== ARTICLE 2 ===
Source: {url2}
{content2}

Comparison:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        return response.Response.Trim();
    }
    
    private async Task<string> GetSummaryAsync(string content, CancellationToken cancellationToken)
    {
        var prompt = $@"Provide a 2-3 sentence summary of this article.
Focus on the main thesis and key takeaways.

{content}

Summary:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        return response.Response.Trim();
    }
    
    private async Task<List<string>> GetKeyPointsAsync(string content, CancellationToken cancellationToken)
    {
        var prompt = $@"Extract the 3-5 main points from this article.
Format: Start each point with '-'
Be specific and actionable when possible.

{content}

Main points:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        return response.Response
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('-'))
            .Select(l => l.TrimStart('-').Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Take(5)
            .ToList();
    }
    
    private async Task<string> ClassifyAsync(
        string content, 
        string[] categories,
        CancellationToken cancellationToken)
    {
        var categoryList = string.Join(", ", categories);
        
        var prompt = $@"Classify this article into ONE category: {categoryList}

Respond with ONLY the category name.

Article (excerpt):
{content[..Math.Min(2000, content.Length)]}

Category:";

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync(cancellationToken);
        
        var result = response.Response.Trim();
        
        return categories.FirstOrDefault(c => 
            result.Contains(c, StringComparison.OrdinalIgnoreCase)) ?? "General";
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
