using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;
using Spectre.Console;

// Create UI service for consistent output
var ui = new UIService();

// Root command
var rootCommand = new RootCommand("Document summarization tool using local LLMs");

// Global options
var configOption = new Option<string?>("--config", "-c") { Description = "Path to configuration file (JSON)" };
var fileOption = new Option<FileInfo?>("--file", "-f") { Description = "Path to the document (DOCX, PDF, or MD)" };
var directoryOption = new Option<DirectoryInfo?>("--directory", "-d") { Description = "Path to directory for batch processing" };
var urlOption = new Option<string?>("--url", "-u") { Description = "Web URL to fetch and summarize (requires --web-enabled)" };
var webEnabledOption = new Option<bool>("--web-enabled") { Description = "Enable web URL fetching (required when using --url)", DefaultValueFactory = _ => false };
var modeOption = new Option<SummarizationMode>("--mode", "-m") { Description = "Summarization mode: Auto (smart selection), BertRag (production pipeline), Bert (fast, no LLM), BertHybrid (BERT+LLM polish), Iterative (small docs), MapReduce (legacy), Rag (legacy)", DefaultValueFactory = _ => SummarizationMode.Auto };
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
            embeddingBackend: config.EmbeddingBackend,
            bertRagConfig: config.BertRag);
        summarizer.SetTemplate(template);
 
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
            await ProcessUrlAsync(summarizer, url, mode, focus, query, config, ui);
        }
        else if (directory != null)
        {
            // Batch processing mode
            await ProcessBatchAsync(summarizer, directory.FullName, mode, focus, config, ui);
        }
        else if (file != null)
        {
            // Single file mode
            await ProcessFileAsync(summarizer, file.FullName, mode, focus, query, config, ui);
        }
        else
        {
            // Default: look for README.md in current directory
            var defaultFile = Path.Combine(Environment.CurrentDirectory, "README.md");
            if (File.Exists(defaultFile))
            {
                ui.Info("No file specified, using README.md in current directory");
                Console.WriteLine();
                await ProcessFileAsync(summarizer, defaultFile, mode, focus, query, config, ui);
            }
            else
            {
                ui.Error("Either --file, --directory, or --url must be specified (or run from a directory containing README.md)");
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
    
    SpectreProgressService.WriteHeader("DocSummarizer", "Dependency Check");

    // Check dependencies with spinner
    bool ollamaOk = false, doclingOk = false, qdrantOk = false;
    ModelInfo? modelInfo = null;
    List<string>? availableModels = null;
    
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("cyan"))
        .StartAsync("Checking dependencies...", async _ =>
        {
            var ollama = new OllamaService();
            ollamaOk = await ollama.IsAvailableAsync();
            
            using var docling = new DoclingClient();
            doclingOk = await docling.IsAvailableAsync();
            
            try
            {
                var qdrant = new QdrantHttpClient("localhost", 6333);
                await qdrant.ListCollectionsAsync();
                qdrantOk = true;
            }
            catch { }
            
            if (ollamaOk && verbose)
            {
                modelInfo = await ollama.GetModelInfoAsync();
                availableModels = await ollama.GetAvailableModelsAsync();
            }
        });

    // Display status table
    var statusTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Blue)
        .Title("[cyan]Dependency Status[/]");
    
    statusTable.AddColumn(new TableColumn("[blue]Service[/]").LeftAligned());
    statusTable.AddColumn(new TableColumn("[blue]Status[/]").Centered());
    statusTable.AddColumn(new TableColumn("[blue]Endpoint[/]").LeftAligned());
    
    statusTable.AddRow(
        "[cyan]Ollama[/]",
        ollamaOk ? "[green]OK[/]" : "[red]FAIL[/]",
        "http://localhost:11434");
    statusTable.AddRow(
        "[cyan]Docling[/]",
        doclingOk ? "[green]OK[/]" : "[yellow]Optional[/]",
        "http://localhost:5001");
    statusTable.AddRow(
        "[cyan]Qdrant[/]",
        qdrantOk ? "[green]OK[/]" : "[yellow]Optional[/]",
        "localhost:6333");
    
    AnsiConsole.Write(statusTable);
    AnsiConsole.WriteLine();

    // Show verbose model info
    if (verbose && ollamaOk)
    {
        if (modelInfo != null)
        {
            var modelTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
                .Title("[green]Default Model Info[/]");
            
            modelTable.AddColumn(new TableColumn("[green]Property[/]"));
            modelTable.AddColumn(new TableColumn("[green]Value[/]"));
            
            modelTable.AddRow("Name", Markup.Escape(modelInfo.Name ?? "N/A"));
            modelTable.AddRow("Family", Markup.Escape(modelInfo.Family ?? "N/A"));
            modelTable.AddRow("Parameters", Markup.Escape(modelInfo.ParameterCount ?? "N/A"));
            modelTable.AddRow("Quantization", Markup.Escape(modelInfo.QuantizationLevel ?? "N/A"));
            modelTable.AddRow("Context Window", $"{modelInfo.ContextWindow:N0} tokens");
            modelTable.AddRow("Format", Markup.Escape(modelInfo.Format ?? "N/A"));
            
            AnsiConsole.Write(modelTable);
            AnsiConsole.WriteLine();
        }
        
        if (availableModels != null && availableModels.Count > 0)
        {
            AnsiConsole.MarkupLine("[cyan]Available Models:[/]");
            var modelList = new Tree("[blue]Models[/]");
            foreach (var m in availableModels.Take(10))
            {
                modelList.AddNode(Markup.Escape(m));
            }
            if (availableModels.Count > 10)
            {
                modelList.AddNode($"[dim]... and {availableModels.Count - 10} more[/]");
            }
            AnsiConsole.Write(modelList);
            AnsiConsole.WriteLine();
        }
    }

    // Show help for missing dependencies
    if (!ollamaOk || (!doclingOk && verbose) || (!qdrantOk && verbose))
    {
        var helpPanel = new Panel(
            new Rows(
                ollamaOk ? Text.Empty : new Text("ollama serve", new Style(Color.Yellow)),
                doclingOk ? Text.Empty : new Text("docker run -p 5001:5001 quay.io/docling-project/docling-serve", new Style(Color.Yellow)),
                qdrantOk ? Text.Empty : new Text("docker run -p 6333:6333 qdrant/qdrant", new Style(Color.Yellow)),
                Text.Empty,
                new Text("Pull default models:", new Style(Color.Cyan1)),
                new Text("  ollama pull llama3.2:3b", new Style(Color.White))))
        {
            Header = new PanelHeader("[yellow] Missing Dependencies [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(helpPanel);
    }

    // Final status
    if (ollamaOk)
    {
        AnsiConsole.MarkupLine("[green]Ready to summarize![/] Ollama is available.");
        if (!doclingOk) AnsiConsole.MarkupLine("[dim]Note: Docling unavailable - PDF/DOCX conversion disabled[/]");
        if (!qdrantOk) AnsiConsole.MarkupLine("[dim]Note: Qdrant unavailable - RAG mode will use in-memory vectors[/]");
        return 0;
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Ollama is required but not available.[/]");
        return 1;
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

// Benchmark command - compare models on the same document
var benchmarkCommand = new Command("benchmark", "Compare multiple models on the same document");
var benchmarkFileOption = new Option<FileInfo?>("--file", "-f") { Description = "Document to summarize (required)" };
var benchmarkModelsOption = new Option<string?>("--models", "-m") { Description = "Comma-separated list of models to compare (e.g., 'qwen2.5:1.5b,llama3.2:3b,ministral-3:3b')" };
var benchmarkModeOption = new Option<SummarizationMode>("--mode") { Description = "Summarization mode to use", DefaultValueFactory = _ => SummarizationMode.MapReduce };
var benchmarkConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };

benchmarkCommand.Options.Add(benchmarkFileOption);
benchmarkCommand.Options.Add(benchmarkModelsOption);
benchmarkCommand.Options.Add(benchmarkModeOption);
benchmarkCommand.Options.Add(benchmarkConfigOption);

benchmarkCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(benchmarkFileOption);
    var modelsString = parseResult.GetValue(benchmarkModelsOption);
    var mode = parseResult.GetValue(benchmarkModeOption);
    var configPath = parseResult.GetValue(benchmarkConfigOption);
    
    if (file == null)
    {
        AnsiConsole.MarkupLine("[red]Error: --file is required[/]");
        return 1;
    }
    
    if (string.IsNullOrEmpty(modelsString))
    {
        AnsiConsole.MarkupLine("[red]Error: --models is required[/]");
        return 1;
    }
    
    var models = modelsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    
    if (models.Length == 0)
    {
        AnsiConsole.MarkupLine("[red]Error: No models specified[/]");
        return 1;
    }
    
    if (!file.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Error: File not found: {file.FullName}[/]");
        return 1;
    }
    
    SpectreProgressService.WriteHeader("DocSummarizer", "Model Benchmark");
    
    // Display benchmark info
    var infoTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Blue);
    
    infoTable.AddColumn("[blue]Property[/]");
    infoTable.AddColumn("[blue]Value[/]");
    infoTable.AddRow("[cyan]Document[/]", Markup.Escape(file.Name));
    infoTable.AddRow("[cyan]Mode[/]", $"[yellow]{mode}[/]");
    infoTable.AddRow("[cyan]Models[/]", $"[green]{models.Length}[/]");
    
    AnsiConsole.Write(infoTable);
    AnsiConsole.WriteLine();
    
    // Load config
    var config = ConfigurationLoader.Load(configPath);
    config.Output.Verbose = false; // Keep output clean
    
    // Store results
    var results = new List<(string Model, TimeSpan Duration, int Words, string Summary)>();
    
    // First, convert the document once and reuse chunks
    AnsiConsole.MarkupLine("[cyan]Converting document...[/]");
    
    var baseSummarizer = new DocumentSummarizer(
        config.Ollama.Model,
        config.Docling.BaseUrl,
        config.Qdrant.Host,
        verbose: false,
        config.Docling,
        config.Processing,
        config.Qdrant,
        ollamaConfig: config.Ollama,
        onnxConfig: config.Onnx,
        embeddingBackend: config.EmbeddingBackend,
        bertRagConfig: config.BertRag);
    
    var docId = Path.GetFileNameWithoutExtension(file.Name);
    var chunks = await SpectreProgressService.WithSpinnerAsync(
        "Parsing document...",
        () => baseSummarizer.ConvertToChunksAsync(file.FullName));
    
    AnsiConsole.MarkupLine($"[green]Document parsed: {chunks.Count} chunks[/]");
    AnsiConsole.WriteLine();
    
    // Benchmark each model
    foreach (var model in models)
    {
        AnsiConsole.MarkupLine($"[cyan]Testing model:[/] [yellow]{Markup.Escape(model)}[/]");
        
        try
        {
            config.Ollama.Model = model;
            
            var summarizer = new DocumentSummarizer(
                model,
                config.Docling.BaseUrl,
                config.Qdrant.Host,
                verbose: false,
                config.Docling,
                config.Processing,
                config.Qdrant,
                ollamaConfig: config.Ollama,
                onnxConfig: config.Onnx,
                embeddingBackend: config.EmbeddingBackend,
                bertRagConfig: config.BertRag);
            
            var sw = Stopwatch.StartNew();
            var summary = await SpectreProgressService.WithSpinnerAsync(
                $"Summarizing with {model}...",
                () => summarizer.SummarizeFromChunksAsync(docId, chunks, mode));
            sw.Stop();
            
            var wordCount = summary.ExecutiveSummary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            results.Add((model, sw.Elapsed, wordCount, summary.ExecutiveSummary));
            
            AnsiConsole.MarkupLine($"  [green]Completed in {sw.Elapsed.TotalSeconds:F1}s ({wordCount} words)[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]Failed: {Markup.Escape(ex.Message)}[/]");
            results.Add((model, TimeSpan.Zero, 0, $"Error: {ex.Message}"));
        }
        
        AnsiConsole.WriteLine();
    }
    
    // Display results table
    var resultsTable = new Table()
        .Border(TableBorder.Double)
        .BorderColor(Color.Green)
        .Title("[green]Benchmark Results[/]");
    
    resultsTable.AddColumn(new TableColumn("[green]Model[/]").LeftAligned());
    resultsTable.AddColumn(new TableColumn("[green]Time[/]").RightAligned());
    resultsTable.AddColumn(new TableColumn("[green]Words[/]").RightAligned());
    resultsTable.AddColumn(new TableColumn("[green]Speed[/]").RightAligned());
    
    foreach (var (model, duration, words, _) in results.OrderBy(r => r.Duration))
    {
        var speed = duration.TotalSeconds > 0 ? words / duration.TotalSeconds : 0;
        var timeColor = duration.TotalSeconds < 10 ? "green" : duration.TotalSeconds < 30 ? "yellow" : "red";
        
        resultsTable.AddRow(
            Markup.Escape(model),
            $"[{timeColor}]{duration.TotalSeconds:F1}s[/]",
            $"{words}",
            $"{speed:F1} w/s");
    }
    
    AnsiConsole.Write(resultsTable);
    AnsiConsole.WriteLine();
    
    // Show fastest model
    var fastest = results.Where(r => r.Duration > TimeSpan.Zero).OrderBy(r => r.Duration).FirstOrDefault();
    if (fastest.Model != null)
    {
        AnsiConsole.MarkupLine($"[green]Fastest:[/] [yellow]{Markup.Escape(fastest.Model)}[/] ({fastest.Duration.TotalSeconds:F1}s)");
    }
    
    // Optionally show summaries (only in interactive mode)
    AnsiConsole.WriteLine();
    var showSummaries = false;
    if (Environment.UserInteractive && !Console.IsInputRedirected)
    {
        try
        {
            showSummaries = AnsiConsole.Confirm("Show summary comparisons?", false);
        }
        catch
        {
            // Non-interactive mode, skip the prompt
        }
    }
    
    if (showSummaries)
    {
        foreach (var (model, _, _, summary) in results.Where(r => !r.Summary.StartsWith("Error:")))
        {
            var panel = new Panel(Markup.Escape(summary.Length > 500 ? summary[..500] + "..." : summary))
            {
                Header = new PanelHeader($" {model} ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 0)
            };
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }
    
    return 0;
});
rootCommand.Subcommands.Add(benchmarkCommand);

// Templates command - list available templates
var templatesCommand = new Command("templates", "List available summary templates");
templatesCommand.SetAction((parseResult, cancellationToken) =>
{
    SpectreProgressService.WriteHeader("DocSummarizer", "Templates");
    
    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .Title("[cyan]Available Templates[/]");
    
    table.AddColumn(new TableColumn("[cyan]Name[/]").LeftAligned());
    table.AddColumn(new TableColumn("[cyan]Words[/]").RightAligned());
    table.AddColumn(new TableColumn("[cyan]Description[/]"));
    
    var presets = new[]
    {
        ("default", "~500", "General purpose summary"),
        ("brief", "~50", "Quick scanning"),
        ("oneliner", "~25", "Single sentence"),
        ("bullets", "auto", "Key takeaways as bullet points"),
        ("executive", "~150", "C-suite reports"),
        ("detailed", "~1000", "Comprehensive analysis"),
        ("technical", "~300", "Tech documentation"),
        ("academic", "~250", "Research papers"),
        ("citations", "auto", "Key quotes with sources"),
        ("bookreport", "~800", "Book report style"),
        ("meeting", "~200", "Meeting notes with actions")
    };
    
    foreach (var (name, words, desc) in presets)
    {
        table.AddRow($"[yellow]{name}[/]", $"[dim]{words}[/]", desc);
    }
    
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
    
    AnsiConsole.MarkupLine("[dim]Usage: docsummarizer -f doc.pdf -t executive[/]");
    AnsiConsole.MarkupLine("[dim]       docsummarizer -f doc.pdf -t bookreport:500[/]");
    
    return Task.FromResult(0);
});
rootCommand.Subcommands.Add(templatesCommand);

// Tool command - for LLM tool integration, outputs JSON
var toolCommand = new Command("tool", "Summarize or query documents and output JSON for LLM tool integration");

var toolUrlOption = new Option<string?>("--url", "-u") { Description = "URL to fetch and process" };
var toolFileOption = new Option<FileInfo?>("--file", "-f") { Description = "File to process" };
var toolAskOption = new Option<string?>("--ask", "-a") { Description = "Ask a question about the document (Q&A mode using RAG)" };
var toolQueryOption = new Option<string?>("--query", "-q") { Description = "Focus query for summarization (filters summary to specific topic)" };
var toolModeOption = new Option<SummarizationMode>("--mode", "-m") { Description = "Summarization mode: Auto, BertRag, Bert, BertHybrid, Iterative, MapReduce, Rag (ignored if --ask is used)", DefaultValueFactory = _ => SummarizationMode.Auto };
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
    DocSummarizerConfig config,
    IUIService ui)
{
    ui.Info($"Fetching URL: {url}");
    ui.Info($"Mode: {mode}");
    ui.Info($"Model: {config.Ollama.Model}");
    if (!string.IsNullOrEmpty(focus)) ui.Info($"Focus: {focus}");
    Console.WriteLine();

    // Use WebFetcher with configured mode (Simple or Playwright)
    var fetcher = new WebFetcher(config.WebFetch);
    using var result = await fetcher.FetchAsync(url, config.WebFetch.Mode);
    
    if (string.IsNullOrWhiteSpace(result.TempFilePath) || !File.Exists(result.TempFilePath))
    {
        ui.Error("Failed to fetch content from URL");
        return;
    }

    // Process the file - cleanup happens automatically via IDisposable
    await ProcessFileAsync(summarizer, result.TempFilePath, mode, focus, query, config, ui, url);
}

static async Task ProcessFileAsync(
    DocumentSummarizer summarizer,
    string filePath,
    SummarizationMode mode,
    string? focus,
    string? query,
    DocSummarizerConfig config,
    IUIService ui,
    string? sourceUrl = null)
{
    var fileName = sourceUrl ?? Path.GetFileName(filePath);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    
    if (!string.IsNullOrEmpty(query))
    {
        // Query mode
        ui.WriteHeader("DocSummarizer", "Query Mode");
        ui.WriteDocumentInfo(fileName, "Query", config.Ollama.Model);
        
        var answer = await ui.WithSpinnerAsync("Querying document...", 
            () => summarizer.QueryAsync(filePath, query));
        
        ui.WriteSummary(answer, "Answer");
        ui.WriteCompletion(sw.Elapsed);
    }
    else
    {
        // Summarize mode
        ui.WriteHeader("DocSummarizer");
        ui.WriteDocumentInfo(fileName, mode.ToString(), config.Ollama.Model, focus);
        
        // Use SummarizeAsync directly - it now has built-in progress for conversion
        var summary = await summarizer.SummarizeAsync(filePath, mode, focus);
        
        sw.Stop();
        ui.WriteCompletion(sw.Elapsed);
        
        // Format and output
        var output = OutputFormatter.Format(summary, config.Output, fileName);
        
        if (config.Output.Format == OutputFormat.Console)
        {
            Console.WriteLine();
            ui.WriteSummary(summary.ExecutiveSummary, "Summary");
            
            // Show extracted entities (characters, locations, etc.) if available
            if (summary.Entities != null && summary.Entities.HasAny)
            {
                Console.WriteLine();
                ui.WriteEntities(summary.Entities);
            }
            
            // Show topics if available
            if (summary.TopicSummaries?.Count > 0)
            {
                Console.WriteLine();
                ui.WriteTopics(summary.TopicSummaries.Select(t => (t.Topic, t.Summary)));
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
            ui.Success($"Saved to: {summaryPath}");
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
    DocSummarizerConfig config,
    IUIService ui)
{
    ui.WriteHeader("DocSummarizer", "Batch Mode");
    ui.WriteDocumentInfo(directoryPath, mode.ToString(), config.Ollama.Model, focus);

    var batchProcessor = new BatchProcessor(summarizer, config.Batch, config.Output.Verbose);
    var totalFiles = 0;
    var processed = 0;
    
    // Enter batch context to prevent nested progress bars
    using (ui.EnterBatchContext())
    {
        // Callback to save each file IMMEDIATELY after processing - avoids OOM
        async Task OnFileCompleted(BatchResult result)
        {
            processed++;
            var fileName = Path.GetFileName(result.FilePath);
            ui.WriteBatchProgress(processed, totalFiles, fileName, result.Success);
            
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
        ui.WriteCompletion(sw.Elapsed, batchSummary.FailureCount == 0);
        ui.Success($"Processed: {batchSummary.SuccessCount} succeeded, {batchSummary.FailureCount} failed");
        
        if (config.Output.Format != OutputFormat.Console)
        {
            var batchOutput = OutputFormatter.FormatBatch(batchSummary, config.Output);
            await OutputFormatter.WriteOutputAsync(batchOutput, config.Output, "_batch_summary", config.Output.OutputDirectory);
        }
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
            embeddingBackend: config.EmbeddingBackend,
            bertRagConfig: config.BertRag);
        
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
