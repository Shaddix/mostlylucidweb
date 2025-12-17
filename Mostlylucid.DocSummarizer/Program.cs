using System.CommandLine;
using System.CommandLine.Invocation;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;

// Root command
var rootCommand = new RootCommand("Document summarization tool using local LLMs");

// Global options
var configOption = new Option<string?>("--config", "Path to configuration file (JSON)");
configOption.AddAlias("-c");

var fileOption = new Option<FileInfo?>("--file", "Path to the document (DOCX, PDF, or MD)");
fileOption.AddAlias("-f");

var directoryOption = new Option<DirectoryInfo?>("--directory", "Path to directory for batch processing");
directoryOption.AddAlias("-d");

var urlOption = new Option<string?>("--url", "Web URL to fetch and summarize (requires --web-enabled)");
urlOption.AddAlias("-u");

var webEnabledOption = new Option<bool>("--web-enabled", () => false, "Enable web URL fetching (required when using --url)");

var modeOption = new Option<SummarizationMode>("--mode", () => SummarizationMode.MapReduce, "Summarization mode: MapReduce, Rag, or Iterative");
modeOption.AddAlias("-m");

var focusOption = new Option<string?>("--focus", "Focus query for RAG mode (e.g., 'pricing terms', 'security requirements')");

var queryOption = new Option<string?>("--query", "Query the document instead of summarizing");
queryOption.AddAlias("-q");

var modelOption = new Option<string?>("--model", "Ollama model to use (overrides config)");

var verboseOption = new Option<bool>("--verbose", () => false, "Show detailed progress");
verboseOption.AddAlias("-v");

var outputFormatOption = new Option<OutputFormat>("--output-format", () => OutputFormat.Console, "Output format: Console, Text, Markdown, Json");
outputFormatOption.AddAlias("-o");

var outputDirOption = new Option<string?>("--output-dir", "Output directory for file outputs");

var extensionsOption = new Option<string[]?>("--extensions", "File extensions to process in batch mode (e.g., .pdf .docx .md)");
extensionsOption.AddAlias("-e");

var recursiveOption = new Option<bool>("--recursive", () => false, "Process directories recursively");
recursiveOption.AddAlias("-r");

var templateOption = new Option<string?>("--template", "Summary template (e.g., 'bookreport' or 'bookreport:500' for custom word count). Available: default, brief, oneliner, bullets, executive, detailed, technical, academic, citations, bookreport, meeting");
templateOption.AddAlias("-t");

var wordsOption = new Option<int?>("--words", "Target word count (overrides template default)");
wordsOption.AddAlias("-w");

// Add options to root command
rootCommand.AddOption(configOption);
rootCommand.AddOption(fileOption);
rootCommand.AddOption(directoryOption);
rootCommand.AddOption(urlOption);
rootCommand.AddOption(webEnabledOption);
rootCommand.AddOption(modeOption);
rootCommand.AddOption(focusOption);
rootCommand.AddOption(queryOption);
rootCommand.AddOption(modelOption);
rootCommand.AddOption(verboseOption);
rootCommand.AddOption(outputFormatOption);
rootCommand.AddOption(outputDirOption);
rootCommand.AddOption(extensionsOption);
rootCommand.AddOption(recursiveOption);
rootCommand.AddOption(templateOption);
rootCommand.AddOption(wordsOption);

// Main handler (using InvocationContext to avoid parameter limits)
rootCommand.SetHandler(async (InvocationContext context) =>
{
    var configPath = context.ParseResult.GetValueForOption(configOption);
    var file = context.ParseResult.GetValueForOption(fileOption);
    var directory = context.ParseResult.GetValueForOption(directoryOption);
    var url = context.ParseResult.GetValueForOption(urlOption);
    var webEnabled = context.ParseResult.GetValueForOption(webEnabledOption);
    var mode = context.ParseResult.GetValueForOption(modeOption);
    var focus = context.ParseResult.GetValueForOption(focusOption);
    var query = context.ParseResult.GetValueForOption(queryOption);
    var model = context.ParseResult.GetValueForOption(modelOption);
    var verbose = context.ParseResult.GetValueForOption(verboseOption);
    var outputFormat = context.ParseResult.GetValueForOption(outputFormatOption);
    var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
    var extensions = context.ParseResult.GetValueForOption(extensionsOption);
    var recursive = context.ParseResult.GetValueForOption(recursiveOption);
    var templateName = context.ParseResult.GetValueForOption(templateOption);
    var targetWords = context.ParseResult.GetValueForOption(wordsOption);
    
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
        
        // Get template - supports "template:wordcount" syntax (e.g., "bookreport:500")
        var template = ParseTemplate(templateName ?? "default", targetWords);
        
        // Show template info if verbose
        if (verbose && !string.IsNullOrEmpty(templateName))
        {
            Console.WriteLine($"Template: {template.Name} ({template.Description}){(template.TargetWords > 0 ? $" [~{template.TargetWords} words]" : "")}");
        }

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
        if (!string.IsNullOrEmpty(url))
        {
            // Web URL mode
            if (!webEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: --web-enabled must be specified when using --url");
                Console.ResetColor();
                context.ExitCode = 1;
                return;
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
                context.ExitCode = 1;
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
        
        context.ExitCode = 1;
    }
});

// Check command - verify dependencies
var checkCommand = new Command("check", "Verify dependencies are available");
var checkVerboseOption = new Option<bool>("--verbose", () => false, "Show detailed model information");
checkVerboseOption.AddAlias("-v");
checkCommand.AddOption(checkVerboseOption);
checkCommand.SetHandler(async (InvocationContext context) =>
{
    var verbose = context.ParseResult.GetValueForOption(checkVerboseOption);
    Console.WriteLine("Checking dependencies...\n");

    // Check Ollama
    var ollama = new OllamaService();
    var ollamaOk = await ollama.IsAvailableAsync();
    Console.WriteLine($"  Ollama: {(ollamaOk ? "✓" : "✗")} (http://localhost:11434)");

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
    Console.WriteLine($"  Docling: {(doclingOk ? "✓" : "✗")} (http://localhost:5001)");

    // Check Qdrant (using HTTP client for AOT compatibility)
    var qdrantOk = false;
    try
    {
        var qdrant = new QdrantHttpClient("localhost", 6333);
        await qdrant.ListCollectionsAsync();
        qdrantOk = true;
    }
    catch { }
    Console.WriteLine($"  Qdrant: {(qdrantOk ? "✓" : "✗")} (localhost:6333)");

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
});
rootCommand.AddCommand(checkCommand);

// Config command - generate default configuration
var configCommand = new Command("config", "Generate default configuration file");
var configOutputOption = new Option<string>("--output", () => "docsummarizer.json", "Output file path");
configOutputOption.AddAlias("-o");
configCommand.AddOption(configOutputOption);
configCommand.SetHandler((InvocationContext context) =>
{
    var outputPath = context.ParseResult.GetValueForOption(configOutputOption) ?? "docsummarizer.json";
    ConfigurationLoader.CreateDefault(outputPath);
    Console.WriteLine($"Created default configuration: {outputPath}");
});
rootCommand.AddCommand(configCommand);

// Tool command - for LLM tool integration, outputs JSON
var toolCommand = new Command("tool", "Summarize and output JSON for LLM tool integration");

var toolUrlOption = new Option<string?>("--url", "URL to fetch and summarize");
toolUrlOption.AddAlias("-u");

var toolFileOption = new Option<FileInfo?>("--file", "File to summarize");
toolFileOption.AddAlias("-f");

var toolQueryOption = new Option<string?>("--query", "Optional focus query");
toolQueryOption.AddAlias("-q");

var toolModeOption = new Option<SummarizationMode>("--mode", () => SummarizationMode.MapReduce, "Summarization mode");
toolModeOption.AddAlias("-m");

var toolModelOption = new Option<string?>("--model", "Ollama model to use");

var toolConfigOption = new Option<string?>("--config", "Configuration file path");
toolConfigOption.AddAlias("-c");

toolCommand.AddOption(toolUrlOption);
toolCommand.AddOption(toolFileOption);
toolCommand.AddOption(toolQueryOption);
toolCommand.AddOption(toolModeOption);
toolCommand.AddOption(toolModelOption);
toolCommand.AddOption(toolConfigOption);

toolCommand.SetHandler(async (InvocationContext context) =>
{
    var url = context.ParseResult.GetValueForOption(toolUrlOption);
    var file = context.ParseResult.GetValueForOption(toolFileOption);
    var query = context.ParseResult.GetValueForOption(toolQueryOption);
    var mode = context.ParseResult.GetValueForOption(toolModeOption);
    var model = context.ParseResult.GetValueForOption(toolModelOption);
    var configPath = context.ParseResult.GetValueForOption(toolConfigOption);

    var output = await RunToolModeAsync(url, file?.FullName, query, mode, model, configPath);
    
    // Output JSON to stdout
    var json = System.Text.Json.JsonSerializer.Serialize(output, DocSummarizerJsonContext.Default.ToolOutput);
    Console.WriteLine(json);
    
    context.ExitCode = output.Success ? 0 : 1;
});
rootCommand.AddCommand(toolCommand);

return await rootCommand.InvokeAsync(args);

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

    // Use WebFetcher to get content (Simple mode only - Playwright requires non-AOT build)
    var fetcher = new WebFetcher(config.WebFetch);
    using var result = await fetcher.FetchAsync(url, WebFetchMode.Simple);
    
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

/// <summary>
/// Run in tool mode - fetch/read, summarize, and return structured JSON output.
/// Designed for LLM tool integration with evidence-grounded claims.
/// </summary>
static async Task<ToolOutput> RunToolModeAsync(
    string? url,
    string? filePath,
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
            var fetchResult = await fetcher.FetchAsync(url, WebFetchMode.Simple);
            
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
        
        // Create summarizer
        var summarizer = new DocumentSummarizer(
            config.Ollama.Model,
            config.Docling.BaseUrl,
            config.Qdrant.Host,
            verbose: false, // Silent for tool mode
            config.Docling,
            config.Processing,
            config.Qdrant);
        
        // Summarize with chunks for evidence tracking
        var (summary, chunks) = await summarizer.SummarizeWithChunksAsync(tempFilePath, mode, focusQuery);
        
        // Build chunk ID lookup for evidence references
        var chunkIds = chunks.Select(c => c.Id).ToHashSet();
        
        // Convert to tool output format with grounded claims
        var toolSummary = ConvertToToolSummary(summary, chunkIds);
        
        var processingTime = DateTime.UtcNow - startTime;
        
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

/// <summary>
/// Convert DocumentSummary to ToolSummary with grounded claims
/// </summary>
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

/// <summary>
/// Extract grounded claims from text, parsing citation references
/// </summary>
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

/// <summary>
/// Remove citation markers from text
/// </summary>
static string StripCitations(string text)
{
    return System.Text.RegularExpressions.Regex.Replace(text, @"\s*\[[^\]]+\]", "").Trim();
}

/// <summary>
/// Parse template specification with optional word count.
/// Supports formats: "bookreport", "bookreport:500", "executive:100"
/// </summary>
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
