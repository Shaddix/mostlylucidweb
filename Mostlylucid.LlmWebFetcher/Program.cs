using Mostlylucid.LlmWebFetcher.Services;

namespace Mostlylucid.LlmWebFetcher;

/// <summary>
/// Sample application demonstrating web content fetching and analysis with LLMs.
/// 
/// Prerequisites:
/// 1. Install Ollama: https://ollama.ai
/// 2. Pull a model: ollama pull llama3.2:3b
/// 3. Run this project: dotnet run
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== LLM Web Fetcher Demo ===\n");
        
        // Check if Ollama is running
        if (!await CheckOllamaAsync())
        {
            Console.WriteLine("Error: Ollama is not running.");
            Console.WriteLine("Please install Ollama from https://ollama.ai and run 'ollama serve'");
            Console.WriteLine("Then pull a model: ollama pull llama3.2:3b");
            return;
        }
        
        // Get URL from args or use default
        var url = args.Length > 0 
            ? args[0] 
            : "https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview";
        
        Console.WriteLine($"Target URL: {url}\n");
        
        // Run demos
        await DemoFetchAndCleanAsync(url);
        await DemoQuestionAnsweringAsync(url);
        await DemoBlogAnalysisAsync(url);
        
        Console.WriteLine("\n=== Demo Complete ===");
    }
    
    /// <summary>
    /// Demo 1: Fetch and clean HTML
    /// </summary>
    static async Task DemoFetchAndCleanAsync(string url)
    {
        Console.WriteLine("--- Demo 1: Fetch and Clean ---\n");
        
        using var fetcher = new WebFetcher();
        var cleaner = new HtmlCleaner();
        var chunker = new ContentChunker();
        
        try
        {
            // Fetch
            Console.WriteLine("Fetching page...");
            var page = await fetcher.FetchAsync(url);
            Console.WriteLine($"  Status: {page.StatusCode}");
            Console.WriteLine($"  HTML size: {page.Html.Length:N0} bytes");
            Console.WriteLine($"  Fetch time: {page.FetchTime.TotalMilliseconds:F0}ms");
            
            // Clean
            Console.WriteLine("\nCleaning HTML...");
            var cleanText = cleaner.Clean(page.Html);
            Console.WriteLine($"  Clean text size: {cleanText.Length:N0} chars");
            Console.WriteLine($"  Estimated tokens: {ContentChunker.EstimateTokens(cleanText):N0}");
            
            // Preview
            Console.WriteLine($"\nFirst 500 chars:");
            Console.WriteLine($"  {cleanText[..Math.Min(500, cleanText.Length)]}...\n");
            
            // Chunk
            Console.WriteLine("Chunking by sentences...");
            var chunks = chunker.ChunkBySentence(cleanText, maxTokens: 2000);
            Console.WriteLine($"  Created {chunks.Count} chunks");
            
            // Show first chunk
            if (chunks.Count > 0)
            {
                Console.WriteLine($"\nFirst chunk ({chunks[0].EstimatedTokens} tokens):");
                Console.WriteLine($"  {chunks[0].Content[..Math.Min(300, chunks[0].Content.Length)]}...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Demo 2: Question answering
    /// </summary>
    static async Task DemoQuestionAnsweringAsync(string url)
    {
        Console.WriteLine("--- Demo 2: Question Answering ---\n");
        
        using var analyzer = new WebContentAnalyzer(model: "llama3.2:3b", verbose: true);
        
        var questions = new[]
        {
            "What are the main new features?",
            "What performance improvements are mentioned?",
            "Is there anything about cloud or containers?"
        };
        
        foreach (var question in questions)
        {
            Console.WriteLine($"\nQ: {question}");
            
            try
            {
                var result = await analyzer.AnalyzeAsync(url, question);
                
                if (result.Success)
                {
                    Console.WriteLine($"A: {result.Answer}");
                    Console.WriteLine($"   (Used {result.ChunksUsed} chunks, ~{result.TokensUsed} tokens, {result.AnalysisTime.TotalSeconds:F1}s)");
                }
                else
                {
                    Console.WriteLine($"Error: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        Console.WriteLine();
    }
    
    /// <summary>
    /// Demo 3: Blog post analysis
    /// </summary>
    static async Task DemoBlogAnalysisAsync(string url)
    {
        Console.WriteLine("--- Demo 3: Blog Analysis ---\n");
        
        using var analyzer = new BlogAnalyzer(model: "llama3.2:3b", verbose: true);
        
        try
        {
            var analysis = await analyzer.AnalyzeAsync(url);
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine(analysis.ToString());
            Console.WriteLine(new string('=', 60));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Check if Ollama is running and has a model available.
    /// </summary>
    static async Task<bool> CheckOllamaAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
