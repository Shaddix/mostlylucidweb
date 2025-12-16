using System.CommandLine;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;

// Root command
var rootCommand = new RootCommand("Document summarization tool using local LLMs");

// Global options
var fileOption = new Option<FileInfo>(
    ["--file", "-f"],
    "Path to the document (DOCX, PDF, or MD)") { IsRequired = true };

var modeOption = new Option<SummarizationMode>(
    ["--mode", "-m"],
    () => SummarizationMode.MapReduce,
    "Summarization mode: MapReduce, Rag, or Iterative");

var focusOption = new Option<string?>(
    ["--focus"],
    "Focus query for RAG mode (e.g., 'pricing terms', 'security requirements')");

var queryOption = new Option<string?>(
    ["--query", "-q"],
    "Query the document instead of summarizing");

var modelOption = new Option<string>(
    ["--model"],
    () => "llama3.2:3b",
    "Ollama model to use");

var verboseOption = new Option<bool>(
    ["--verbose", "-v"],
    () => false,
    "Show detailed progress");

var doclingOption = new Option<string>(
    ["--docling-url"],
    () => "http://localhost:5001",
    "Docling service URL");

var qdrantOption = new Option<string>(
    ["--qdrant-host"],
    () => "localhost",
    "Qdrant host");

// Main summarize command
rootCommand.AddOption(fileOption);
rootCommand.AddOption(modeOption);
rootCommand.AddOption(focusOption);
rootCommand.AddOption(queryOption);
rootCommand.AddOption(modelOption);
rootCommand.AddOption(verboseOption);
rootCommand.AddOption(doclingOption);
rootCommand.AddOption(qdrantOption);

rootCommand.SetHandler(async (file, mode, focus, query, model, verbose, doclingUrl, qdrantHost) =>
{
    try
    {
        var summarizer = new DocumentSummarizer(model, doclingUrl, qdrantHost, verbose);

        if (!string.IsNullOrEmpty(query))
        {
            // Query mode
            Console.WriteLine($"Querying: {file.Name}");
            Console.WriteLine($"Question: {query}\n");
            
            var answer = await summarizer.QueryAsync(file.FullName, query);
            Console.WriteLine("Answer:");
            Console.WriteLine(answer);
        }
        else
        {
            // Summarize mode
            Console.WriteLine($"Summarizing: {file.Name}");
            Console.WriteLine($"Mode: {mode}");
            if (!string.IsNullOrEmpty(focus)) Console.WriteLine($"Focus: {focus}");
            Console.WriteLine();

            var summary = await summarizer.SummarizeAsync(file.FullName, mode, focus);
            
            // Output
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine(summary.ExecutiveSummary);
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            if (summary.TopicSummaries.Count > 0 && verbose)
            {
                Console.WriteLine("\n### Topic Summaries\n");
                foreach (var topic in summary.TopicSummaries)
                {
                    Console.WriteLine($"**{topic.Topic}** [{string.Join(", ", topic.SourceChunks)}]");
                    Console.WriteLine(topic.Summary);
                    Console.WriteLine();
                }
            }

            if (summary.OpenQuestions.Count > 0)
            {
                Console.WriteLine("\n### Open Questions\n");
                foreach (var q in summary.OpenQuestions)
                    Console.WriteLine($"- {q}");
            }

            // Trace
            Console.WriteLine("\n### Trace\n");
            Console.WriteLine($"- Document: {summary.Trace.DocumentId}");
            Console.WriteLine($"- Chunks: {summary.Trace.TotalChunks} total, {summary.Trace.ChunksProcessed} processed");
            Console.WriteLine($"- Topics: {summary.Trace.Topics.Count}");
            Console.WriteLine($"- Time: {summary.Trace.TotalTime.TotalSeconds:F1}s");
            Console.WriteLine($"- Coverage: {summary.Trace.CoverageScore:P0}");
            Console.WriteLine($"- Citation rate: {summary.Trace.CitationRate:F2}");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        
        if (verbose)
            Console.WriteLine(ex.StackTrace);
        
        Environment.Exit(1);
    }
}, fileOption, modeOption, focusOption, queryOption, modelOption, verboseOption, doclingOption, qdrantOption);

// Check command - verify dependencies
var checkCommand = new Command("check", "Verify dependencies are available");
checkCommand.SetHandler(async () =>
{
    Console.WriteLine("Checking dependencies...\n");

    // Check Ollama
    var ollama = new OllamaService();
    var ollamaOk = await ollama.IsAvailableAsync();
    Console.WriteLine($"  Ollama: {(ollamaOk ? "✓" : "✗")} (http://localhost:11434)");

    // Check Docling
    using var docling = new DoclingClient();
    var doclingOk = await docling.IsAvailableAsync();
    Console.WriteLine($"  Docling: {(doclingOk ? "✓" : "✗")} (http://localhost:5001)");

    // Check Qdrant
    var qdrantOk = false;
    try
    {
        var qdrant = new Qdrant.Client.QdrantClient("localhost");
        await qdrant.ListCollectionsAsync();
        qdrantOk = true;
    }
    catch { }
    Console.WriteLine($"  Qdrant: {(qdrantOk ? "✓" : "✗")} (localhost:6334)");

    Console.WriteLine();
    if (!ollamaOk || !doclingOk || !qdrantOk)
    {
        Console.WriteLine("Some dependencies are not available. To start them:");
        if (!ollamaOk) Console.WriteLine("  ollama serve");
        if (!doclingOk) Console.WriteLine("  docker run -p 5001:5001 quay.io/docling-project/docling-serve");
        if (!qdrantOk) Console.WriteLine("  docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant");
    }
    else
    {
        Console.WriteLine("All dependencies available!");
    }
});
rootCommand.AddCommand(checkCommand);

return await rootCommand.InvokeAsync(args);
