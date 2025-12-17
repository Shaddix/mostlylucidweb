using System.CommandLine;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;

// Root command
var rootCommand = new RootCommand("Document summarization tool using local LLMs");

// Global options
var configOption = new Option<string?>(
    new[] { "--config", "-c" },
    "Path to configuration file (JSON)");

var fileOption = new Option<FileInfo?>(
    new[] { "--file", "-f" },
    "Path to the document (DOCX, PDF, or MD)");

var directoryOption = new Option<DirectoryInfo?>(
    new[] { "--directory", "-d" },
    "Path to directory for batch processing");

var modeOption = new Option<SummarizationMode>(
    new[] { "--mode", "-m" },
    () => SummarizationMode.MapReduce,
    "Summarization mode: MapReduce, Rag, or Iterative");

var focusOption = new Option<string?>(
    "--focus",
    "Focus query for RAG mode (e.g., 'pricing terms', 'security requirements')");

var queryOption = new Option<string?>(
    new[] { "--query", "-q" },
    "Query the document instead of summarizing");

var modelOption = new Option<string?>(
    new[] { "--model" },
    "Ollama model to use (overrides config)");

var verboseOption = new Option<bool>(
    new[] { "--verbose", "-v" },
    () => false,
    "Show detailed progress");

var outputFormatOption = new Option<OutputFormat>(
    new[] { "--output-format", "-o" },
    () => OutputFormat.Console,
    "Output format: Console, Text, Markdown, Json");

var outputDirOption = new Option<string?>(
    new[] { "--output-dir" },
    "Output directory for file outputs");

var extensionsOption = new Option<string[]?>(
    new[] { "--extensions", "-e" },
    "File extensions to process in batch mode (e.g., .pdf .docx .md)");

var recursiveOption = new Option<bool>(
    new[] { "--recursive", "-r" },
    () => false,
    "Process directories recursively");

// Add options to root command
rootCommand.AddOption(configOption);
rootCommand.AddOption(fileOption);
rootCommand.AddOption(directoryOption);
rootCommand.AddOption(modeOption);
rootCommand.AddOption(focusOption);
rootCommand.AddOption(queryOption);
rootCommand.AddOption(modelOption);
rootCommand.AddOption(verboseOption);
rootCommand.AddOption(outputFormatOption);
rootCommand.AddOption(outputDirOption);
rootCommand.AddOption(extensionsOption);
rootCommand.AddOption(recursiveOption);

// Main handler (split into multiple SetHandlers due to parameter limits)
rootCommand.SetHandler(async (context) =>
{
    var configPath = context.ParseResult.GetValueForOption(configOption);
    var file = context.ParseResult.GetValueForOption(fileOption);
    var directory = context.ParseResult.GetValueForOption(directoryOption);
    var mode = context.ParseResult.GetValueForOption(modeOption);
    var focus = context.ParseResult.GetValueForOption(focusOption);
    var query = context.ParseResult.GetValueForOption(queryOption);
    var model = context.ParseResult.GetValueForOption(modelOption);
    var verbose = context.ParseResult.GetValueForOption(verboseOption);
    var outputFormat = context.ParseResult.GetValueForOption(outputFormatOption);
    var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
    var extensions = context.ParseResult.GetValueForOption(extensionsOption);
    var recursive = context.ParseResult.GetValueForOption(recursiveOption);
    
    try
    {
        // Load configuration
        var config = ConfigurationLoader.Load(configPath);
        
        // Override configuration with command-line options
        if (model != null) config.Ollama.Model = model;
        config.Output.Verbose = verbose;
        config.Output.Format = outputFormat;
        if (outputDir != null) config.Output.OutputDirectory = outputDir;
        if (extensions != null && extensions.Length > 0) config.Batch.FileExtensions = extensions.ToList();
        config.Batch.Recursive = recursive;

        // Create summarizer
        var summarizer = new DocumentSummarizer(
            config.Ollama.Model,
            config.Docling.BaseUrl,
            config.Qdrant.Host,
            config.Output.Verbose,
            config.Docling,
            config.Processing,
            config.Qdrant);

        // Determine operation mode
        if (directory != null)
        {
            // Batch processing mode
            await ProcessBatchAsync(summarizer, directory.FullName, mode, focus, config);
        }
        else if (file != null)
        {
            // Single file mode
            await ProcessFileAsync(summarizer, file.FullName, mode, focus, query, config);
        }
        else
        {
            // Default: look for README.md in current directory
            var defaultFile = Path.Combine(Environment.CurrentDirectory, "README.md");
            if (File.Exists(defaultFile))
            {
                Console.WriteLine("No file specified, using README.md in current directory");
                Console.WriteLine();
                await ProcessFileAsync(summarizer, defaultFile, mode, focus, query, config);
            }
            else
            {
                Console.WriteLine("Error: Either --file or --directory must be specified");
                Console.WriteLine("       (or run from a directory containing README.md)");
                Environment.Exit(1);
            }
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
});

// Check command - verify dependencies
var checkCommand = new Command("check", "Verify dependencies are available");
var checkVerboseOption = new Option<bool>(
    new[] { "--verbose", "-v" },
    () => false,
    "Show detailed model information");
checkCommand.AddOption(checkVerboseOption);
checkCommand.SetHandler(async (verbose) =>
{
    Console.WriteLine("Checking dependencies...\n");

    // Check Ollama
    var ollama = new OllamaService();
    var ollamaOk = await ollama.IsAvailableAsync();
    Console.WriteLine($"  Ollama: {(ollamaOk ? "✓" : "✗")} (http://localhost:11434)");

    if (ollamaOk && verbose)
    {
        Console.WriteLine("\n  Available models:");
        var models = await ollama.GetAvailableModelsAsync();
        foreach (var model in models.Take(10))
        {
            Console.WriteLine($"    - {model}");
        }
        if (models.Count > 10)
        {
            Console.WriteLine($"    ... and {models.Count - 10} more");
        }

        Console.WriteLine("\n  Default model info:");
        var modelInfo = await ollama.GetModelInfoAsync();
        if (modelInfo != null)
        {
            Console.WriteLine($"    Name: {modelInfo.Name}");
            Console.WriteLine($"    Family: {modelInfo.Family}");
            Console.WriteLine($"    Parameters: {modelInfo.ParameterCount}");
            Console.WriteLine($"    Quantization: {modelInfo.QuantizationLevel}");
            Console.WriteLine($"    Context Window: {modelInfo.ContextWindow:N0} tokens");
            Console.WriteLine($"    Format: {modelInfo.Format}");
        }
        
        Console.WriteLine("\n  Embed model info:");
        var embedInfo = await ollama.GetModelInfoAsync(ollama.EmbedModel);
        if (embedInfo != null)
        {
            Console.WriteLine($"    Name: {embedInfo.Name}");
            Console.WriteLine($"    Family: {embedInfo.Family}");
            Console.WriteLine($"    Parameters: {embedInfo.ParameterCount}");
        }
    }

    // Check Docling
    Console.WriteLine();
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
        Console.WriteLine();
        Console.WriteLine("To pull the default models:");
        Console.WriteLine("  ollama pull ministral-3:3b");
        Console.WriteLine("  ollama pull nomic-embed-text");
    }
    else
    {
        Console.WriteLine("All dependencies available!");
    }
}, checkVerboseOption);
rootCommand.AddCommand(checkCommand);

// Config command - generate default configuration
var configCommand = new Command("config", "Generate default configuration file");
var configOutputOption = new Option<string>(
    new[] { "--output", "-o" },
    () => "docsummarizer.json",
    "Output file path");
configCommand.AddOption(configOutputOption);
configCommand.SetHandler((outputPath) =>
{
    ConfigurationLoader.CreateDefault(outputPath);
    Console.WriteLine($"Created default configuration: {outputPath}");
}, configOutputOption);
rootCommand.AddCommand(configCommand);

return await rootCommand.InvokeAsync(args);

// Helper methods
static async Task ProcessFileAsync(
    DocumentSummarizer summarizer,
    string filePath,
    SummarizationMode mode,
    string? focus,
    string? query,
    DocSummarizerConfig config)
{
    var fileName = Path.GetFileName(filePath);
    
    if (!string.IsNullOrEmpty(query))
    {
        // Query mode
        Console.WriteLine($"Querying: {fileName}");
        Console.WriteLine($"Question: {query}\n");
        
        var answer = await summarizer.QueryAsync(filePath, query);
        Console.WriteLine("Answer:");
        Console.WriteLine(answer);
    }
    else
    {
        // Summarize mode - always show basic progress
        Console.WriteLine($"Summarizing: {fileName}");
        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"Model: {config.Ollama.Model}");
        if (!string.IsNullOrEmpty(focus)) Console.WriteLine($"Focus: {focus}");
        Console.WriteLine();
        Console.Out.Flush();

        var summary = await summarizer.SummarizeAsync(filePath, mode, focus);
        
        // Format and output
        var output = OutputFormatter.Format(summary, config.Output, fileName);
        
        if (config.Output.Format == OutputFormat.Console)
        {
            Console.WriteLine(output);
            
            // Auto-save to .summary.md file
            var fileDir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var summaryPath = Path.Combine(fileDir, $"{baseName}.summary.md");
            
            // Format as markdown for file output
            var markdownConfig = new OutputConfig { Format = OutputFormat.Markdown, IncludeTrace = true };
            var markdownOutput = OutputFormatter.Format(summary, markdownConfig, fileName);
            await File.WriteAllTextAsync(summaryPath, markdownOutput);
            
            Console.WriteLine();
            Console.WriteLine($"Saved to: {summaryPath}");
        }
        else
        {
            await OutputFormatter.WriteOutputAsync(output, config.Output, fileName, config.Output.OutputDirectory);
        }
    }
}

static async Task ProcessBatchAsync(
    DocumentSummarizer summarizer,
    string directoryPath,
    SummarizationMode mode,
    string? focus,
    DocSummarizerConfig config)
{
    Console.WriteLine($"Batch processing directory: {directoryPath}");
    Console.WriteLine($"Mode: {mode}");
    if (!string.IsNullOrEmpty(focus)) Console.WriteLine($"Focus: {focus}");
    Console.WriteLine();

    var batchProcessor = new BatchProcessor(summarizer, config.Batch, config.Output.Verbose);
    
    // Callback to save each file IMMEDIATELY after processing - avoids OOM
    async Task OnFileCompleted(BatchResult result)
    {
        if (result.Success && result.Summary != null)
        {
            var fileName = Path.GetFileName(result.FilePath);
            var output = OutputFormatter.Format(result.Summary, config.Output, fileName);
            
            if (config.Output.Format == OutputFormat.Console)
            {
                Console.WriteLine($"\n\n=== {fileName} ===\n");
                Console.WriteLine(output);
            }
            else
            {
                await OutputFormatter.WriteOutputAsync(output, config.Output, fileName, config.Output.OutputDirectory);
            }
        }
    }
    
    var batchSummary = await batchProcessor.ProcessDirectoryAsync(directoryPath, mode, focus, OnFileCompleted);

    // Output batch summary (only contains failed files now, not full results)
    var batchOutput = OutputFormatter.FormatBatch(batchSummary, config.Output);
    
    if (config.Output.Format == OutputFormat.Console)
    {
        Console.WriteLine(batchOutput);
    }
    else
    {
        await OutputFormatter.WriteOutputAsync(batchOutput, config.Output, "_batch_summary", config.Output.OutputDirectory);
    }
}
