using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mostlylucid.DocSummarizer.Models;

public record DocumentChunk(
    int Order,
    string Heading,
    int HeadingLevel,
    string Content,
    string Hash)
{
    public string Id => $"chunk-{Order}";
}

public record ChunkSummary(
    string ChunkId,
    string Heading,
    string Summary,
    int Order);

public record TopicSummary(
    string Topic,
    string Summary,
    List<string> SourceChunks);

public record DocumentSummary(
    string ExecutiveSummary,
    List<TopicSummary> TopicSummaries,
    List<string> OpenQuestions,
    SummarizationTrace Trace);

public record SummarizationTrace(
    string DocumentId,
    int TotalChunks,
    int ChunksProcessed,
    List<string> Topics,
    TimeSpan TotalTime,
    double CoverageScore,
    double CitationRate);

public record ValidationResult(
    int TotalCitations,
    int InvalidCount,
    bool IsValid,
    List<string> InvalidCitations);

public enum SummarizationMode
{
    MapReduce,
    Rag,
    Iterative
}

public static class HashHelper
{
    public static string ComputeHash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16];
    }
}

public static class CitationValidator
{
    // Flexible: matches any bracketed reference like [chunk-0], [chunk-12], etc.
    private static readonly Regex CitationPattern = new(@"\[([^\]]+)\]", RegexOptions.Compiled);

    public static ValidationResult Validate(string summary, HashSet<string> validChunkIds)
    {
        var matches = CitationPattern.Matches(summary);
        var citations = matches
            .Select(m => m.Groups[1].Value)
            .Where(id => id.StartsWith("chunk-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var invalid = citations.Where(c => !validChunkIds.Contains(c)).ToList();
        
        return new ValidationResult(
            citations.Count,
            invalid.Count,
            invalid.Count == 0 && citations.Count > 0,
            invalid);
    }
}

public record BatchResult(
    string FilePath,
    bool Success,
    DocumentSummary? Summary,
    string? Error,
    TimeSpan ProcessingTime);

public record BatchSummary(
    int TotalFiles,
    int SuccessCount,
    int FailureCount,
    List<BatchResult> Results,
    TimeSpan TotalTime)
{
    public double SuccessRate => TotalFiles > 0 ? (double)SuccessCount / TotalFiles : 0;
}
