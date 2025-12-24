using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mostlylucid.DocSummarizer;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Extensions;
using Mostlylucid.DocSummarizer.Services;
using Mostlylucid.DocSummarizer.Models;

// Check if we should generate word lists
if (args.Length > 0 && args[0] == "--generate-wordlists")
{
    var outputDir = args.Length > 1 ? args[1] : @"C:\Blog\mostlylucidweb\Mostlylucid.DocSummarizer.Core\Resources";
    GenerateWordLists(outputDir);
    return;
}

// Build the host with DocSummarizer services
var builder = Host.CreateApplicationBuilder(args);

// Configure DocSummarizer
builder.Services.AddDocSummarizer(options =>
{
    // Use local ONNX embeddings (no external services needed)
    options.EmbeddingBackend = EmbeddingBackend.Onnx;
    
    // Use in-memory vector store for testing
    options.BertRag.VectorStore = VectorStoreBackend.InMemory;
    
    // Verbose output
    options.Output.Verbose = true;
});

var host = builder.Build();

// Get the summarizer service
var summarizer = host.Services.GetRequiredService<IDocumentSummarizer>();

Console.WriteLine("=== DocSummarizer Blog Test ===\n");

// Test with real blog posts
var blogDir = @"C:\Blog\mostlylucidweb\Mostlylucid\Markdown";
var testFiles = new[]
{
    "tencommandments.md",
    "docsummarizer-tool.md",
    "textsearchingpt1.md",
    "botdetection-introduction.md"
};

foreach (var fileName in testFiles)
{
    var filePath = Path.Combine(blogDir, fileName);
    if (!File.Exists(filePath))
    {
        Console.WriteLine($"Skipping {fileName} - not found");
        continue;
    }
    
    Console.WriteLine($"\n{'=',-60}");
    Console.WriteLine($"Processing: {fileName}");
    Console.WriteLine($"{'=',-60}\n");
    
    var markdown = await File.ReadAllTextAsync(filePath);
    var docId = Path.GetFileNameWithoutExtension(filePath);
    
    // Extract segments
    var extraction = await summarizer.ExtractSegmentsAsync(markdown, docId);
    
    Console.WriteLine($"Document Statistics:");
    Console.WriteLine($"  Total segments: {extraction.AllSegments.Count}");
    Console.WriteLine($"  Top by salience: {extraction.TopBySalience.Count}");
    Console.WriteLine($"  Content type: {extraction.ContentType}");
    Console.WriteLine($"  Extraction time: {extraction.ExtractionTime.TotalSeconds:F2}s");
    Console.WriteLine();
    
    // Group by segment type
    var byType = extraction.AllSegments
        .GroupBy(s => s.Type)
        .OrderByDescending(g => g.Count());
    
    Console.WriteLine("Segment breakdown:");
    foreach (var group in byType)
    {
        Console.WriteLine($"  {group.Key}: {group.Count()}");
    }
    Console.WriteLine();
    
    // Show top 5 segments
    Console.WriteLine("Top 5 segments by salience:");
    foreach (var segment in extraction.TopBySalience.Take(5))
    {
        var preview = segment.Text.Length > 100 
            ? segment.Text[..100].Replace("\n", " ") + "..." 
            : segment.Text.Replace("\n", " ");
        Console.WriteLine($"  [{segment.Type}] Score: {segment.SalienceScore:F3}");
        Console.WriteLine($"    Section: {segment.SectionTitle}");
        Console.WriteLine($"    Text: {preview}");
        Console.WriteLine();
    }
    
    // Show unique sections found
    var sections = extraction.AllSegments
        .Where(s => !string.IsNullOrEmpty(s.SectionTitle))
        .Select(s => s.SectionTitle)
        .Distinct()
        .Take(10);
    
    Console.WriteLine("Document sections found:");
    foreach (var section in sections)
    {
        Console.WriteLine($"  - {section}");
    }
}

Console.WriteLine("\n=== Test Complete ===");

// Word list generation function
static void GenerateWordLists(string outputDirectory)
{
    Console.WriteLine($"Generating word lists to: {outputDirectory}");
    Directory.CreateDirectory(outputDirectory);
    
    // Generate day names
    var dayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
    {
        try
        {
            var dateFormat = culture.DateTimeFormat;
            foreach (var day in dateFormat.DayNames)
                if (!string.IsNullOrWhiteSpace(day)) dayNames.Add(day.Trim());
            foreach (var day in dateFormat.AbbreviatedDayNames)
                if (!string.IsNullOrWhiteSpace(day)) dayNames.Add(day.Trim().TrimEnd('.'));
            foreach (var day in dateFormat.ShortestDayNames)
                if (!string.IsNullOrWhiteSpace(day)) dayNames.Add(day.Trim().TrimEnd('.'));
        }
        catch { }
    }
    WriteWordList(Path.Combine(outputDirectory, "day-names.txt"), 
        "Day names from .NET CultureInfo (all cultures)", dayNames);
    Console.WriteLine($"  Generated {dayNames.Count} day names");
    
    // Generate month names
    var monthNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
    {
        try
        {
            var dateFormat = culture.DateTimeFormat;
            foreach (var month in dateFormat.MonthNames)
                if (!string.IsNullOrWhiteSpace(month)) monthNames.Add(month.Trim());
            foreach (var month in dateFormat.AbbreviatedMonthNames)
                if (!string.IsNullOrWhiteSpace(month)) monthNames.Add(month.Trim().TrimEnd('.'));
            foreach (var month in dateFormat.MonthGenitiveNames)
                if (!string.IsNullOrWhiteSpace(month)) monthNames.Add(month.Trim());
            foreach (var month in dateFormat.AbbreviatedMonthGenitiveNames)
                if (!string.IsNullOrWhiteSpace(month)) monthNames.Add(month.Trim().TrimEnd('.'));
        }
        catch { }
    }
    WriteWordList(Path.Combine(outputDirectory, "month-names.txt"), 
        "Month names from .NET CultureInfo (all cultures)", monthNames);
    Console.WriteLine($"  Generated {monthNames.Count} month names");
    
    Console.WriteLine("Done!");
}

static void WriteWordList(string path, string description, HashSet<string> words)
{
    var sb = new StringBuilder();
    sb.AppendLine($"# {description}");
    sb.AppendLine("# Auto-generated from .NET's globalization data");
    sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
    sb.AppendLine($"# Total: {words.Count} entries");
    sb.AppendLine();
    
    foreach (var word in words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase))
    {
        sb.AppendLine(word);
    }
    
    File.WriteAllText(path, sb.ToString());
}
