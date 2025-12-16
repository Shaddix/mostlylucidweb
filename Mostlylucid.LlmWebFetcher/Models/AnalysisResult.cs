namespace Mostlylucid.LlmWebFetcher.Models;

/// <summary>
/// Result of analyzing web content with an LLM.
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// The source URL that was analyzed.
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// The question that was asked.
    /// </summary>
    public string Question { get; set; } = "";
    
    /// <summary>
    /// The LLM's answer.
    /// </summary>
    public string Answer { get; set; } = "";
    
    /// <summary>
    /// Number of chunks used in the context.
    /// </summary>
    public int ChunksUsed { get; set; }
    
    /// <summary>
    /// Total tokens in the context.
    /// </summary>
    public int TokensUsed { get; set; }
    
    /// <summary>
    /// Time taken for the analysis.
    /// </summary>
    public TimeSpan AnalysisTime { get; set; }
    
    /// <summary>
    /// Whether the analysis was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if the analysis failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Comprehensive blog post analysis result.
/// </summary>
public class BlogAnalysis
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> KeyPoints { get; set; } = new();
    public string Category { get; set; } = "";
    public int ReadingTimeMinutes { get; set; }
    public int WordCount { get; set; }
    public TimeSpan AnalysisTime { get; set; }
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Blog Analysis");
        sb.AppendLine($"URL: {Url}");
        if (!string.IsNullOrEmpty(Title))
            sb.AppendLine($"Title: {Title}");
        sb.AppendLine($"Category: {Category}");
        sb.AppendLine($"Reading Time: {ReadingTimeMinutes} min ({WordCount} words)");
        sb.AppendLine($"Analysis Time: {AnalysisTime.TotalSeconds:F1}s");
        sb.AppendLine();
        sb.AppendLine($"Summary:");
        sb.AppendLine($"  {Summary}");
        sb.AppendLine();
        sb.AppendLine($"Key Points:");
        foreach (var point in KeyPoints)
        {
            sb.AppendLine($"  - {point}");
        }
        
        return sb.ToString();
    }
}
