using System.CommandLine;
using System.CommandLine.Parsing;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;

// Root command
var rootCommand = new RootCommand("Document summarization tool using local LLMs");

// Global options
var configOption = new Option<string?>("--config", "-c") { Description = "Path to configuration file (JSON)" };
var fileOption = new Option<FileInfo?>("--file", "-f") { Description = "Path to the document (DOCX, PDF, or MD)" };
var directoryOption = new Option<DirectoryInfo?>("--directory", "-d") { Description = "Path to directory for batch processing" };
var urlOption = new Option<string?>("--url", "-u") { Description = "Web URL to fetch and summarize (requires --web-enabled)" };
var webEnabledOption = new Option<bool>("--web-enabled") { Description = "Enable web URL fetching (required when using --url)", DefaultValueFactory = _ => false };
var modeOption = new Option<SummarizationMode>("--mode", "-m") { Description = "Summarization mode: MapReduce, Rag, or Iterative", DefaultValueFactory = _ => SummarizationMode.MapReduce };
var focusOption = new Option<string?>("--focus") { Description = "Focus query for RAG mode (e.g., 'pricing terms', 'security requirements')" };
var queryOption = new Option<string?>("--query", "-q") { Description = "Query the document instead of summarizing" };
var modelOption = new Option<string?>("--model") { Description = "Ollama model to use (overrides config)" };
var verboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed progress", DefaultValueFactory = _ => false };
var outputFormatOption = new Option<OutputFormat>("--output-format", "-o") { Description = "Output format: Console, Text, Markdown, Json", DefaultValueFactory = _ => OutputFormat.Console };
var outputDirOption = new Option<string?>("--output-dir") { Description = "Output directory for file outputs" };
var extensionsOption = new Option<string[]?>("--extensions", "-e") { Description = "File extensions to process in batch mode (e.g., .pdf .docx .md)" };
var recursiveOption = new Option<bool>("--recursive", "-r") { Description = "Process directories recursively", DefaultValueFactory = _ => false };
var templateOption = new Option<string?>("--template", "-t") { Description = "Summary template (e.g., 'bookreport' or 'bookreport:500' for custom word count). Available: default, brief, oneliner, bullets, executive, detailed, technical, academic, citations, bookreport, meeting" };
var wordsOption = new Option<int?>("--words", "-w") { Description = "Target word count (overrides template default)" };
var showStructureOption = new Option<bool>("--show-structure", "-s") { Description = "Include document structure/chunk index in output", DefaultValueFactory = _ => false };
var embeddingBackendOption = new Option<EmbeddingBackend?>("--embedding-backend") { Description = "Embedding backend: Onnx (default, fast, zero-config) or Ollama (requires server)" };
var embeddingModelOption = new Option<string?>("--embedding-model") { Description = "ONNX embedding model: AllMiniLmL6V2 (default), BgeSmallEnV15, GteSmall, MultiQaMiniLm, ParaphraseMiniLmL3" };
var webModeOption = new Option<WebFetchMode?>("--web-mode") { Description = "Web fetch mode: Simple (fast HTTP) or Playwright (headless browser for JS-rendered pages)" };

// Add options to root command
rootCommand.Options.Add(configOption);
rootCommand.Options.Add(fileOption);
rootCommand.Options.Add(directoryOption);
rootCommand.Options.Add(urlOption);
rootCommand.Options.Add(webEnabledOption);
rootCommand.Options.Add(modeOption);
rootCommand.Options.Add(focusOption);
rootCommand.Options.Add(queryOption);
rootCommand.Options.Add(modelOption);
rootCommand.Options.Add(verboseOption);
rootCommand.Options.Add(outputFormatOption);
rootCommand.Options.Add(outputDirOption);
rootCommand.Options.Add(extensionsOption);
rootCommand.Options.Add(recursiveOption);
rootCommand.Options.Add(templateOption);
rootCommand.Options.Add(wordsOption);
rootCommand.Options.Add(showStructureOption);
rootCommand.Options.Add(embeddingBackendOption);
rootCommand.Options.Add(embeddingModelOption);
rootCommand.Options.Add(webModeOption);

// Main handler
rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var configPath = parseResult.GetValue(configOption);
    var file = parseResult.GetValue(fileOption);
    var directory = parseResult.GetValue(directoryOption);
    var url = parseResult.GetValue(urlOption);
    var webEnabled = parseResult.GetValue(webEnabledOption);
    var mode = parseResult.GetValue(modeOption);
    var focus = parseResult.GetValue(focusOption);
    var query = parseResult.GetValue(queryOption);
    var model = parseResult.GetValue(modelOption);
    var verbose = parseResult.GetValue(verboseOption);
    var outputFormat = parseResult.GetValue(outputFormatOption);
    var outputDir = parseResult.GetValue(outputDirOption);
    var extensions = parseResult.GetValue(extensionsOption);
    var recursive = parseResult.GetValue(recursiveOption);
    var templateName = parseResult.GetValue(templateOption);
    var targetWords = parseResult.GetValue(wordsOption);
    var showStructure = parseResult.GetValue(showStructureOption);
    
    try
    {
        // Load configuration
        var config = ConfigurationLoader.Load(configPath);
        
        // Override configuration with command-line options
        if (model != null) config.Ollama.Model = model;
        config.Output.Verbose = verbose;
        config.Output.Format = outputFormat;
        config.Output.IncludeChunkIndex = showStructure;
        if (outputDir != null) config.Output.OutputDirectory = outputDir;
        if (extensions != null && extensions.Length > 0) config.Batch.FileExtensions = extensions.ToList();
        config.Batch.Recursive = recursive;
        
        // Parse embedding options
        var embeddingBackend = parseResult.GetValue(embeddingBackendOption);
        var embeddingModel = parseResult.GetValue(embeddingModelOption);
        
        if (embeddingBackend.HasValue) config.EmbeddingBackend = embeddingBackend.Value;
        if (!string.IsNullOrEmpty(embeddingModel))
            config.Onnx.EmbeddingModel = Enum.Parse<OnnxEmbeddingModel>(embeddingModel, ignoreCase: true);
        
        // Parse web fetch mode
        var webMode = parseResult.GetValue(webModeOption);
        if (webMode.HasValue) config.WebFetch.Mode = webMode.Value;
        
        // Get template - supports "template:wordcount" syntax (e.g., "bookreport:500")
        var template = ParseTemplate(templateName ?? "default", targetWords);
        
        // Show template info if verbose
        if (verbose && !string.IsNullOrEmpty(templateName))
        {
            Console.WriteLine($"Template: {template.Name} ({template.Description}){(template.TargetWords > 0 ? $" [~{template.TargetWords} words]" : "")}");
        }

        // Create summarizer with ONNX embedding by default (fast, no external deps)
        var summarizer = new DocumentSummarizer(
            config.Ollama.Model,
            config.Docling.BaseUrl,
            config.Qdrant.Host,
            config.Output.Verbose,
            config.Docling,
            config.Processing,
            config.Qdrant,
            ollamaConfig: config.Ollama,
            onnxConfig: config.Onnx,
            embeddingBackend: config.EmbeddingBackend);

        // Determine operation mode
        if (!string.IsNullOrEmpty(url))
        {
            // Web URL mode
            if (!webEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: --web-enabled must be specified when using --url");
                Console.ResetColor();
                return 1;
            }
            
            // Enable web fetch in config
            config.WebFetch.Enabled = true;
            await ProcessUrlAsync(summarizer, url, mode, focus, query, config);
        }
        else if (directory != null)
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
                Console.WriteLine("Error: Either --file, --directory, or --url must be specified");
                Console.WriteLine("       (or run from a directory containing README.md)");
                return 1;
            }
        }
        return 0;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        
        if (verbose)
            Console.WriteLine(ex.StackTrace);
        
        return 1;
    }
});

// Check command - verify dependencies
var checkCommand = new Command("check", "Verify dependencies are available");
var checkVerboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed model information", DefaultValueFactory = _ => false };
checkCommand.Options.Add(checkVerboseOption);
checkCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var verbose = parseResult.GetValue(checkVerboseOption);
    Console.WriteLine("Checking dependencies...\n");

    // Check Ollama
    var ollama = new OllamaService();
    var ollamaOk = await ollama.IsAvailableAsync();
    Console.WriteLine($"  Ollama: {(ollamaOk ? "OK" : "FAIL")} (http://localhost:11434)");

    if (ollamaOk && verbose)
    {
        Console.WriteLine("\n  Available models:");
        var models = await ollama.GetAvailableModelsAsync();
        foreach (var modelItem in models.Take(10))
        {
            Console.WriteLine($"    - {modelItem}");
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
    Console.WriteLine($"  Docling: {(doclingOk ? "OK" : "FAIL")} (http://localhost:5001)");

    // Check Qdrant (using HTTP client for AOT compatibility)
    var qdrantOk = false;
    try
    {
        var qdrant = new QdrantHttpClient("localhost", 6333);
        await qdrant.ListCollectionsAsync();
        qdrantOk = true;
    }
    catch { }
    Console.WriteLine($"  Qdrant: {(qdrantOk ? "OK" : "FAIL")} (localhost:6333)");

    Console.WriteLine();
    if (!ollamaOk || !doclingOk || !qdrantOk)
    {
        Console.WriteLine("Some dependencies are not available. To start them:");
        if (!ollamaOk) Console.WriteLine("  ollama serve");
        if (!doclingOk) Console.WriteLine("  docker run -p 5001:5001 quay.io/docling-project/docling-serve");
        if (!qdrantOk) Console.WriteLine("  docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant");
        Console.WriteLine();
        Console.WriteLine("To pull the default models:");
        Console.WriteLine("  ollama pull llama3.2:3b");
        Console.WriteLine("  ollama pull nomic-embed-text");
        return 1;
    }
    else
    {
        Console.WriteLine("All dependencies available!");
        return 0;
    }
});
rootCommand.Subcommands.Add(checkCommand);

// Config command - generate default configuration
var configCommand = new Command("config", "Generate default configuration file");
var configOutputOption = new Option<string>("--output", "-o") { Description = "Output file path", DefaultValueFactory = _ => "docsummarizer.json" };
configCommand.Options.Add(configOutputOption);
configCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var outputPath = parseResult.GetValue(configOutputOption) ?? "docsummarizer.json";
    ConfigurationLoader.CreateDefault(outputPath);
    Console.WriteLine($"Created default configuration: {outputPath}");
    await Task.CompletedTask;
    return 0;
});
rootCommand.Subcommands.Add(configCommand);

// Tool command - for LLM tool integration, outputs JSON
var toolCommand = new Command("tool", "Summarize or query documents and output JSON for LLM tool integration");

var toolUrlOption = new Option<string?>("--url", "-u") { Description = "URL to fetch and process" };
var toolFileOption = new Option<FileInfo?>("--file", "-f") { Description = "File to process" };
var toolAskOption = new Option<string?>("--ask", "-a") { Description = "Ask a question about the document (Q&A mode using RAG)" };
var toolQueryOption = new Option<string?>("--query", "-q") { Description = "Focus query for summarization (filters summary to specific topic)" };
var toolModeOption = new Option<SummarizationMode>("--mode", "-m") { Description = "Summarization mode (ignored if --ask is used)", DefaultValueFactory = _ => SummarizationMode.MapReduce };
var toolModelOption = new Option<string?>("--model") { Description = "Ollama model to use" };
var toolConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };

toolCommand.Options.Add(toolUrlOption);
toolCommand.Options.Add(toolFileOption);
toolCommand.Options.Add(toolAskOption);
toolCommand.Options.Add(toolQueryOption);
toolCommand.Options.Add(toolModeOption);
toolCommand.Options.Add(toolModelOption);
toolCommand.Options.Add(toolConfigOption);

toolCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var url = parseResult.GetValue(toolUrlOption);
    var file = parseResult.GetValue(toolFileOption);
    var ask = parseResult.GetValue(toolAskOption);
    var query = parseResult.GetValue(toolQueryOption);
    var mode = parseResult.GetValue(toolModeOption);
    var model = parseResult.GetValue(toolModelOption);
    var configPath = parseResult.GetValue(toolConfigOption);

    var output = await RunToolModeAsync(url, file?.FullName, ask, query, mode, model, configPath);
    
    // Output JSON to stdout
    var json = System.Text.Json.JsonSerializer.Serialize(output, DocSummarizerJsonContext.Default.ToolOutput);
    Console.WriteLine(json);
    
    return output.Success ? 0 : 1;
});
rootCommand.Subcommands.Add(toolCommand);

return rootCommand.Parse(args).Invoke();

// Helper methods
static async Task ProcessUrlAsync(
    DocumentSummarizer summarizer,
    string url,
    SummarizationMode mode,
    string? focus,
    string? query,
    DocSummarizerConfig config)
{
    Console.WriteLine($"Fetching URL: {url}");
    Console.WriteLine($"Mode: {mode}");
    Console.WriteLine($"Model: {config.Ollama.Model}");
    if (!string.IsNullOrEmpty(focus)) Console.WriteLine($"Focus: {focus}");
    Console.WriteLine();
    Console.Out.Flush();

    // Use WebFetcher with configured mode (Simple or Playwright)
    var fetcher = new WebFetcher(config.WebFetch);
    using var result = await fetcher.FetchAsync(url, config.WebFetch.Mode);
    
    if (string.IsNullOrWhiteSpace(result.TempFilePath) || !File.Exists(result.TempFilePath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Error: Failed to fetch content from URL");
        Console.ResetColor();
        return;
    }

    // Process the file - cleanup happens automatically via IDisposable
    await ProcessFileAsync(summarizer, result.TempFilePath, mode, focus, query, config, url);
}

static async Task ProcessFileAsync(
    DocumentSummarizer summarizer,
    string filePath,
    SummarizationMode mode,
    string? focus,
    string? query,
    DocSummarizerConfig config,
    string? sourceUrl = null)
{
    var fileName = sourceUrl ?? Path.GetFileName(filePath);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    if (!string.IsNullOrEmpty(query))
    {
        // Query mode
        SpectreProgressService.WriteHeader("DocSummarizer", "Query Mode");
        SpectreProgressService.WriteDocumentInfo(fileName, "Query", config.Ollama.Model);
        
        var answer = await SpectreProgressService.WithSpinnerAsync("Querying document...", 
            () => summarizer.QueryAsync(filePath, query));
        
        SpectreProgressService.WriteSummaryPanel(answer, "Answer");
        SpectreProgressService.WriteCompletion(sw.Elapsed);
    }
    else
    {
        // Summarize mode with Spectre progress
        SpectreProgressService.WriteHeader("DocSummarizer");
        SpectreProgressService.WriteDocumentInfo(fileName, mode.ToString(), config.Ollama.Model, focus);
        
        // Use SummarizeAsync directly - it now has built-in Spectre progress for conversion
        var summary = await summarizer.SummarizeAsync(filePath, mode, focus);
        
        sw.Stop();
        SpectreProgressService.WriteCompletion(sw.Elapsed);
        
        // Format and output
        var output = OutputFormatter.Format(summary, config.Output, fileName);
        
        if (config.Output.Format == OutputFormat.Console)
        {
            Console.WriteLine();
            SpectreProgressService.WriteSummaryPanel(summary.ExecutiveSummary, "Summary");
            
            // Show topics if available
            if (summary.TopicSummaries?.Count > 0)
            {
                Console.WriteLine();
                SpectreProgressService.WriteTopicsTree(
                    summary.TopicSummaries.Select(t => (t.Topic, t.Summary)));
            }
            
            // Auto-save to .summary.md file
            var fileDir = sourceUrl != null ? Environment.CurrentDirectory : (Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory);
            var baseName = sourceUrl != null ? SanitizeFileName(new Uri(sourceUrl).Host) : Path.GetFileNameWithoutExtension(filePath);
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

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
}

static async Task ProcessBatchAsync(
    DocumentSummarizer summarizer,
    string directoryPath,
    SummarizationMode mode,
    string? focus,
    DocSummarizerConfig config)
{
    SpectreProgressService.WriteHeader("DocSummarizer", "Batch Mode");
    SpectreProgressService.WriteDocumentInfo(directoryPath, mode.ToString(), config.Ollama.Model, focus);

    var batchProcessor = new BatchProcessor(summarizer, config.Batch, config.Output.Verbose);
    var processed = 0;
    
    // Callback to save each file IMMEDIATELY after processing - avoids OOM
    async Task OnFileCompleted(BatchResult result)
    {
        processed++;
        var fileName = Path.GetFileName(result.FilePath);
        Console.WriteLine($"  [{(result.Success ? "OK" : "FAIL")}] {fileName}");
        
        if (result.Success && result.Summary != null)
        {
            var output = OutputFormatter.Format(result.Summary, config.Output, fileName);
            
            if (config.Output.Format != OutputFormat.Console)
            {
                await OutputFormatter.WriteOutputAsync(output, config.Output, fileName, config.Output.OutputDirectory);
            }
        }
    }
    
    Console.WriteLine();
    
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var batchSummary = await batchProcessor.ProcessDirectoryAsync(directoryPath, mode, focus, OnFileCompleted);
    sw.Stop();

    // Output batch summary
    Console.WriteLine();
    SpectreProgressService.WriteCompletion(sw.Elapsed, batchSummary.FailureCount == 0);
    Console.WriteLine($"Processed: {batchSummary.SuccessCount} succeeded, {batchSummary.FailureCount} failed");
    
    if (config.Output.Format != OutputFormat.Console)
    {
        var batchOutput = OutputFormatter.FormatBatch(batchSummary, config.Output);
        await OutputFormatter.WriteOutputAsync(batchOutput, config.Output, "_batch_summary", config.Output.OutputDirectory);
    }
}

// Run in tool mode - fetch/read, summarize or query, and return structured JSON output.
// Designed for LLM tool integration with evidence-grounded claims.
static async Task<ToolOutput> RunToolModeAsync(
    string? url,
    string? filePath,
    string? askQuestion,
    string? focusQuery,
    SummarizationMode mode,
    string? modelOverride,
    string? configPath)
{
    var startTime = DateTime.UtcNow;
    string source;
    string? finalUrl = null;
    string? contentType = null;
    string tempFilePath;
    bool shouldCleanup = false;
    
    try
    {
        // Load configuration
        var config = ConfigurationLoader.Load(configPath);
        if (modelOverride != null) config.Ollama.Model = modelOverride;
        config.Output.Verbose = false; // Suppress console output in tool mode
        
        // Determine source
        if (!string.IsNullOrEmpty(url))
        {
            source = url;
            config.WebFetch.Enabled = true;
            
            var fetcher = new WebFetcher(config.WebFetch);
            var fetchResult = await fetcher.FetchAsync(url, config.WebFetch.Mode);
            
            tempFilePath = fetchResult.TempFilePath;
            finalUrl = fetchResult.SourceUrl;
            contentType = fetchResult.ContentType;
            shouldCleanup = true;
        }
        else if (!string.IsNullOrEmpty(filePath))
        {
            source = filePath;
            if (!File.Exists(filePath))
            {
                return new ToolOutput
                {
                    Success = false,
                    Source = source,
                    Error = $"File not found: {filePath}"
                };
            }
            tempFilePath = filePath;
        }
        else
        {
            return new ToolOutput
            {
                Success = false,
                Source = "none",
                Error = "Either --url or --file must be specified"
            };
        }
        
        // Create summarizer with ONNX embedding by default (fast, no external deps)
        var summarizer = new DocumentSummarizer(
            config.Ollama.Model,
            config.Docling.BaseUrl,
            config.Qdrant.Host,
            verbose: false, // Silent for tool mode
            config.Docling,
            config.Processing,
            config.Qdrant,
            ollamaConfig: config.Ollama,
            onnxConfig: config.Onnx,
            embeddingBackend: config.EmbeddingBackend);
        
        var processingTime = DateTime.UtcNow - startTime;
        
        // Q&A mode (--ask) vs Summarization mode
        if (!string.IsNullOrEmpty(askQuestion))
        {
            // Query mode - uses RAG to answer a specific question
            var answer = await summarizer.QueryAsync(tempFilePath, askQuestion);
            
            processingTime = DateTime.UtcNow - startTime;
            
            return new ToolOutput
            {
                Success = true,
                Source = source,
                ContentType = contentType,
                Answer = new ToolAnswer
                {
                    Question = askQuestion,
                    Response = answer,
                    Mode = "RAG"
                },
                Metadata = new ToolMetadata
                {
                    ProcessingSeconds = processingTime.TotalSeconds,
                    ChunksProcessed = 0, // Not tracked in query mode currently
                    Model = config.Ollama.Model,
                    Mode = "Query",
                    CoverageScore = 0,
                    CitationRate = 0,
                    FetchedAt = startTime.ToString("o"),
                    FinalUrl = finalUrl
                }
            };
        }
        
        // Summarize with chunks for evidence tracking
        var (summary, chunks) = await summarizer.SummarizeWithChunksAsync(tempFilePath, mode, focusQuery);
        
        // Build chunk ID lookup for evidence references
        var chunkIds = chunks.Select(c => c.Id).ToHashSet();
        
        // Convert to tool output format with grounded claims
        var toolSummary = ConvertToToolSummary(summary, chunkIds);
        
        processingTime = DateTime.UtcNow - startTime;
        
        return new ToolOutput
        {
            Success = true,
            Source = source,
            ContentType = contentType,
            Summary = toolSummary,
            Metadata = new ToolMetadata
            {
                ProcessingSeconds = processingTime.TotalSeconds,
                ChunksProcessed = summary.Trace.ChunksProcessed,
                Model = config.Ollama.Model,
                Mode = mode.ToString(),
                CoverageScore = summary.Trace.CoverageScore,
                CitationRate = summary.Trace.CitationRate,
                FetchedAt = startTime.ToString("o"),
                FinalUrl = finalUrl
            }
        };
    }
    catch (SecurityException ex)
    {
        return new ToolOutput
        {
            Success = false,
            Source = url ?? filePath ?? "unknown",
            Error = $"Security blocked: {ex.Message}"
        };
    }
    catch (Exception ex)
    {
        return new ToolOutput
        {
            Success = false,
            Source = url ?? filePath ?? "unknown",
            Error = ex.Message
        };
    }
    finally
    {
        // Cleanup temp file from web fetch
        if (shouldCleanup && !string.IsNullOrEmpty(url))
        {
            // Note: cleanup is handled by the using statement if we use WebFetchResult properly
            // This is a fallback
        }
    }
}

// Convert DocumentSummary to ToolSummary with grounded claims
static ToolSummary ConvertToToolSummary(DocumentSummary summary, HashSet<string> validChunkIds)
{
    // Extract key facts from executive summary
    // Parse citations from the summary to create grounded claims
    var keyFacts = ExtractGroundedClaims(summary.ExecutiveSummary, validChunkIds);
    
    // Convert topic summaries
    var topics = summary.TopicSummaries.Select(t => new ToolTopic
    {
        Name = t.Topic,
        Summary = t.Summary,
        Evidence = t.SourceChunks.Where(validChunkIds.Contains).ToList()
    }).ToList();
    
    // Extract entities if available
    ToolEntities? entities = null;
    if (summary.Entities != null)
    {
        entities = new ToolEntities
        {
            People = summary.Entities.Characters?.Count > 0 ? summary.Entities.Characters : null,
            Organizations = summary.Entities.Organizations?.Count > 0 ? summary.Entities.Organizations : null,
            Locations = summary.Entities.Locations?.Count > 0 ? summary.Entities.Locations : null,
            Dates = summary.Entities.Dates?.Count > 0 ? summary.Entities.Dates : null,
            Concepts = summary.Entities.Events?.Count > 0 ? summary.Entities.Events : null,
            Links = null // ExtractedEntities doesn't have URLs
        };
    }
    
    return new ToolSummary
    {
        Executive = StripCitations(summary.ExecutiveSummary),
        KeyFacts = keyFacts,
        Topics = topics,
        Entities = entities,
        OpenQuestions = summary.OpenQuestions?.Count > 0 ? summary.OpenQuestions : null
    };
}

// Extract grounded claims from text, parsing citation references
static List<GroundedClaim> ExtractGroundedClaims(string text, HashSet<string> validChunkIds)
{
    var claims = new List<GroundedClaim>();
    
    // Split into sentences and extract claims with their citations
    var citationPattern = new System.Text.RegularExpressions.Regex(@"\[([^\]]+)\]");
    var sentences = text.Split(new[] { ". ", ".\n", ".\r\n" }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var sentence in sentences)
    {
        var trimmed = sentence.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length < 10)
            continue;
        
        // Extract citations from this sentence
        var matches = citationPattern.Matches(trimmed);
        var evidence = matches
            .Select(m => m.Groups[1].Value)
            .Where(id => id.StartsWith("chunk-") && validChunkIds.Contains(id))
            .Distinct()
            .ToList();
        
        // Clean the claim text
        var claimText = citationPattern.Replace(trimmed, "").Trim();
        if (!claimText.EndsWith(".")) claimText += ".";
        
        // Determine confidence based on evidence
        var confidence = evidence.Count switch
        {
            >= 2 => "high",
            1 => "medium",
            _ => "low"
        };
        
        claims.Add(new GroundedClaim
        {
            Claim = claimText,
            Confidence = confidence,
            Evidence = evidence,
            Type = evidence.Count > 0 ? "fact" : "inference"
        });
    }
    
    return claims;
}

// Remove citation markers from text
static string StripCitations(string text)
{
    return System.Text.RegularExpressions.Regex.Replace(text, @"\s*\[[^\]]+\]", "").Trim();
}

// Parse template specification with optional word count.
// Supports formats: "bookreport", "bookreport:500", "executive:100"
static SummaryTemplate ParseTemplate(string templateSpec, int? wordCountOverride)
{
    var parts = templateSpec.Split(':', 2);
    var templateName = parts[0].Trim();
    
    // Get base template
    var template = SummaryTemplate.Presets.GetByName(templateName);
    
    // Check for word count in template spec (e.g., "bookreport:500")
    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out var specWords))
    {
        template.TargetWords = specWords;
    }
    
    // Command-line --words option takes precedence
    if (wordCountOverride.HasValue)
    {
        template.TargetWords = wordCountOverride.Value;
    }
    
    return template;
}
