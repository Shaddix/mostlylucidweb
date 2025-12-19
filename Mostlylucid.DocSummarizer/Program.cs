using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;
using Mostlylucid.DocSummarizer.Services.Onnx;
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

// Docling options for performance tuning
var doclingUrlOption = new Option<string?>("--docling-url") { Description = "Docling service URL (default: http://localhost:5001)" };
var doclingPagesPerChunkOption = new Option<int?>("--pages-per-chunk") { Description = "Pages per chunk for PDF split processing (default: 50, use higher for GPU)" };
var doclingMaxConcurrentOption = new Option<int?>("--max-concurrent-chunks") { Description = "Max concurrent chunks to process (default: 2, use 1 for GPU)" };
var doclingDisableSplitOption = new Option<bool?>("--no-split") { Description = "Disable split processing - process entire PDF at once (best for GPU)" };
var doclingMinPagesForSplitOption = new Option<int?>("--min-pages-split") { Description = "Minimum pages before enabling split processing (default: 60)" };
var doclingPdfBackendOption = new Option<string?>("--pdf-backend") { Description = "PDF backend: pypdfium2 (fast) or docling (accurate)" };
var doclingGpuOption = new Option<bool?>("--docling-gpu") { Description = "Force GPU mode for Docling (auto-detected if not set). Use --docling-gpu=true or --docling-gpu=false" };
var onnxGpuOption = new Option<string?>("--onnx-gpu") { Description = "ONNX execution provider: cpu, cuda, directml, auto. Use cuda for NVIDIA, directml for AMD/Intel" };
var gpuDeviceIdOption = new Option<int?>("--gpu-device") { Description = "GPU device ID for ONNX (default: 0). Use nvidia-smi -L to list devices. Set to 1 if GPU 0 is integrated graphics" };

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
rootCommand.Options.Add(doclingUrlOption);
rootCommand.Options.Add(doclingPagesPerChunkOption);
rootCommand.Options.Add(doclingMaxConcurrentOption);
rootCommand.Options.Add(doclingDisableSplitOption);
rootCommand.Options.Add(doclingMinPagesForSplitOption);
rootCommand.Options.Add(doclingPdfBackendOption);
rootCommand.Options.Add(doclingGpuOption);
rootCommand.Options.Add(onnxGpuOption);
rootCommand.Options.Add(gpuDeviceIdOption);

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
        
        // Parse ONNX GPU options
        var onnxGpu = parseResult.GetValue(onnxGpuOption);
        var gpuDeviceId = parseResult.GetValue(gpuDeviceIdOption);
        
        if (!string.IsNullOrEmpty(onnxGpu))
            config.Onnx.ExecutionProvider = Enum.Parse<OnnxExecutionProvider>(onnxGpu, ignoreCase: true);
        if (gpuDeviceId.HasValue)
            config.Onnx.GpuDeviceId = gpuDeviceId.Value;
        
        // Parse web fetch mode
        var webMode = parseResult.GetValue(webModeOption);
        if (webMode.HasValue) config.WebFetch.Mode = webMode.Value;
        
        // Parse Docling options for performance tuning
        var doclingUrl = parseResult.GetValue(doclingUrlOption);
        var pagesPerChunk = parseResult.GetValue(doclingPagesPerChunkOption);
        var maxConcurrent = parseResult.GetValue(doclingMaxConcurrentOption);
        var noSplit = parseResult.GetValue(doclingDisableSplitOption);
        var minPagesForSplit = parseResult.GetValue(doclingMinPagesForSplitOption);
        var pdfBackend = parseResult.GetValue(doclingPdfBackendOption);
        
        if (!string.IsNullOrEmpty(doclingUrl)) config.Docling.BaseUrl = doclingUrl;
        if (pagesPerChunk.HasValue) config.Docling.PagesPerChunk = pagesPerChunk.Value;
        if (maxConcurrent.HasValue) config.Docling.MaxConcurrentChunks = maxConcurrent.Value;
        if (noSplit == true) config.Docling.EnableSplitProcessing = false;
        if (minPagesForSplit.HasValue) config.Docling.MinPagesForSplit = minPagesForSplit.Value;
        if (!string.IsNullOrEmpty(pdfBackend)) config.Docling.PdfBackend = pdfBackend;
        
        // Get explicit GPU setting from CLI
        var doclingGpu = parseResult.GetValue(doclingGpuOption);
        
        // Detect available services and auto-adapt config
        // Pass GPU override to detection so it displays correctly
        var detectedServices = await ServiceDetector.DetectAndDisplayAsync(config, verbose, doclingGpu);
        
        // Auto-optimize Docling config based on GPU detection (unless user specified explicit values)
        if (config.Docling.AutoDetectGpu && !pagesPerChunk.HasValue && !maxConcurrent.HasValue)
        {
            var optimizedDocling = detectedServices.GetOptimizedDoclingConfig(config.Docling);
            config.Docling.PagesPerChunk = optimizedDocling.PagesPerChunk;
            config.Docling.MaxConcurrentChunks = optimizedDocling.MaxConcurrentChunks;
            config.Docling.MinPagesForSplit = optimizedDocling.MinPagesForSplit;
            
            if (verbose && detectedServices.DoclingAvailable)
            {
                var gpuStatus = detectedServices.DoclingHasGpu ? "GPU" : "CPU";
                AnsiConsole.MarkupLine($"[dim]Docling ({gpuStatus}): pages/chunk={config.Docling.PagesPerChunk}, concurrent={config.Docling.MaxConcurrentChunks}, min-split={config.Docling.MinPagesForSplit}[/]");
            }
        }
        
        // Get template - supports "template:wordcount" syntax (e.g., "bookreport:500")
        var template = ParseTemplate(templateName ?? "default", targetWords);
        
        // Show template info if verbose
        if (verbose && !string.IsNullOrEmpty(templateName))
        {
            var wordInfo = template.TargetWords > 0 ? $" (~{template.TargetWords} words)" : "";
            AnsiConsole.MarkupLine($"[dim]Template: {Markup.Escape(template.Name)} ({Markup.Escape(template.Description)}){Markup.Escape(wordInfo)}[/]");
        }
        
        // Show what mode will be used (but don't override Auto - let summarizer decide based on document size)
        // Note: The detailed configuration reasoning is now shown in DetectAndDisplayAsync
        if (mode != SummarizationMode.Auto)
        {
            AnsiConsole.MarkupLine($"[cyan]Mode override:[/] {mode} [dim](user specified)[/]");
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
var checkConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
checkCommand.Options.Add(checkVerboseOption);
checkCommand.Options.Add(checkConfigOption);
checkCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var verbose = parseResult.GetValue(checkVerboseOption);
    var configPath = parseResult.GetValue(checkConfigOption);
    
    SpectreProgressService.WriteHeader("DocSummarizer", "Dependency Check");

    // Load config for detection
    var config = ConfigurationLoader.Load(configPath);
    
    // Use unified service detection
    var detected = await ServiceDetector.DetectAsync(config, verbose);
    
    ModelInfo? modelInfo = null;
    if (detected.OllamaAvailable && verbose)
    {
        var ollama = new OllamaService();
        modelInfo = await ollama.GetModelInfoAsync();
    }

    // Display status table
    var statusTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Blue)
        .Title("[cyan]Dependency Status[/]");
    
    statusTable.AddColumn(new TableColumn("[blue]Service[/]").LeftAligned());
    statusTable.AddColumn(new TableColumn("[blue]Status[/]").Centered());
    statusTable.AddColumn(new TableColumn("[blue]Details[/]").LeftAligned());
    
    statusTable.AddRow(
        "[cyan]Ollama[/]",
        detected.OllamaAvailable ? "[green]OK[/]" : "[red]FAIL[/]",
        detected.OllamaAvailable ? $"{detected.AvailableModels.Count} models" : "Run: ollama serve");
    statusTable.AddRow(
        "[cyan]Docling[/]",
        detected.DoclingAvailable ? "[green]OK[/]" : "[yellow]Optional[/]",
        detected.DoclingAvailable 
            ? (detected.DoclingHasGpu ? "[cyan]GPU accelerated[/]" : "CPU mode") 
            : "PDF/DOCX disabled");
    statusTable.AddRow(
        "[cyan]Qdrant[/]",
        detected.QdrantAvailable ? "[green]OK[/]" : "[yellow]Optional[/]",
        detected.QdrantAvailable ? "Vector persistence enabled" : "Using in-memory vectors");
    statusTable.AddRow(
        "[cyan]ONNX[/]",
        "[green]OK[/]",
        "Embedded (always available)");
    
    AnsiConsole.Write(statusTable);
    AnsiConsole.WriteLine();
    
    // Display features table
    var featuresTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .Title("[cyan]Available Features[/]");
    
    featuresTable.AddColumn(new TableColumn("[cyan]Feature[/]").LeftAligned());
    featuresTable.AddColumn(new TableColumn("[cyan]Status[/]").Centered());
    featuresTable.AddColumn(new TableColumn("[cyan]Requires[/]").LeftAligned());
    
    var f = detected.Features;
    featuresTable.AddRow(
        "PDF/DOCX conversion",
        f.PdfConversion ? "[green]✓[/]" : "[red]✗[/]",
        "Docling");
    featuresTable.AddRow(
        "Fast GPU conversion",
        f.FastPdfConversion ? "[green]✓[/]" : "[yellow]○[/]",
        "Docling + CUDA");
    featuresTable.AddRow(
        "BERT summarization",
        f.BertSummarization ? "[green]✓[/]" : "[red]✗[/]",
        "ONNX (embedded)");
    featuresTable.AddRow(
        "LLM summarization",
        f.LlmSummarization ? "[green]✓[/]" : "[yellow]○[/]",
        "Ollama");
    featuresTable.AddRow(
        "Document Q&A",
        f.DocumentQA ? "[green]✓[/]" : "[yellow]○[/]",
        "Ollama + ONNX");
    featuresTable.AddRow(
        "Vector persistence",
        f.VectorPersistence ? "[green]✓[/]" : "[yellow]○[/]",
        "Qdrant");
    featuresTable.AddRow(
        "Cross-session cache",
        f.CrossSessionCache ? "[green]✓[/]" : "[yellow]○[/]",
        "Qdrant");
    
    AnsiConsole.Write(featuresTable);
    AnsiConsole.WriteLine();
    
    // Best mode recommendation
    AnsiConsole.MarkupLine($"[cyan]Recommended mode:[/] {f.BestModeDescription}");
    AnsiConsole.WriteLine();

    // Show verbose model info
    if (verbose && detected.OllamaAvailable)
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
        
        if (detected.AvailableModels.Count > 0)
        {
            AnsiConsole.MarkupLine("[cyan]Available Models:[/]");
            var modelList = new Tree("[blue]Models[/]");
            foreach (var m in detected.AvailableModels.Take(10))
            {
                modelList.AddNode(Markup.Escape(m));
            }
            if (detected.AvailableModels.Count > 10)
            {
                modelList.AddNode($"[dim]... and {detected.AvailableModels.Count - 10} more[/]");
            }
            AnsiConsole.Write(modelList);
            AnsiConsole.WriteLine();
        }
    }

    // Show help for missing dependencies
    if (!detected.OllamaAvailable || (!detected.DoclingAvailable && verbose) || (!detected.QdrantAvailable && verbose))
    {
        var helpPanel = new Panel(
            new Rows(
                detected.OllamaAvailable ? Text.Empty : new Text("ollama serve", new Style(Color.Yellow)),
                detected.DoclingAvailable ? Text.Empty : new Text("docker run -p 5001:5001 quay.io/docling-project/docling-serve", new Style(Color.Yellow)),
                detected.QdrantAvailable ? Text.Empty : new Text("docker run -p 6333:6333 qdrant/qdrant", new Style(Color.Yellow)),
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
    if (detected.OllamaAvailable || detected.Features.BertSummarization)
    {
        AnsiConsole.MarkupLine("[green]Ready to summarize![/]");
        if (!detected.OllamaAvailable) AnsiConsole.MarkupLine("[dim]Note: Ollama unavailable - using BERT-only mode[/]");
        if (!detected.DoclingAvailable) AnsiConsole.MarkupLine("[dim]Note: Docling unavailable - PDF/DOCX conversion disabled[/]");
        if (!detected.QdrantAvailable) AnsiConsole.MarkupLine("[dim]Note: Qdrant unavailable - using in-memory vectors (no persistence)[/]");
        return 0;
    }
    else
    {
        AnsiConsole.MarkupLine("[red]No summarization backend available.[/]");
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

// Benchmark Templates command - compare templates on the same document
var benchmarkTemplatesCommand = new Command("benchmark-templates", "Compare multiple summary templates on the same document (reuses extraction)");
var btFileOption = new Option<FileInfo?>("--file", "-f") { Description = "Document to summarize (required)" };
var btTemplatesOption = new Option<string?>("--templates", "-t") { Description = "Comma-separated list of templates to compare (e.g., 'default,brief,executive,technical'). Use 'all' for all templates." };
var btFocusOption = new Option<string?>("--focus", "-q") { Description = "Focus query for retrieval (optional)" };
var btOutputDirOption = new Option<string?>("--output-dir", "-o") { Description = "Output directory for summary files (defaults to document directory)" };
var btConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
var btVerboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed progress", DefaultValueFactory = _ => false };

benchmarkTemplatesCommand.Options.Add(btFileOption);
benchmarkTemplatesCommand.Options.Add(btTemplatesOption);
benchmarkTemplatesCommand.Options.Add(btFocusOption);
benchmarkTemplatesCommand.Options.Add(btOutputDirOption);
benchmarkTemplatesCommand.Options.Add(btConfigOption);
benchmarkTemplatesCommand.Options.Add(btVerboseOption);

benchmarkTemplatesCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(btFileOption);
    var templatesString = parseResult.GetValue(btTemplatesOption);
    var focus = parseResult.GetValue(btFocusOption);
    var outputDir = parseResult.GetValue(btOutputDirOption);
    var configPath = parseResult.GetValue(btConfigOption);
    var verbose = parseResult.GetValue(btVerboseOption);
    
    if (file == null)
    {
        AnsiConsole.MarkupLine("[red]Error: --file is required[/]");
        return 1;
    }
    
    if (!file.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Error: File not found: {file.FullName}[/]");
        return 1;
    }
    
    // Parse templates - "all" means all available templates
    var templateNames = new List<string>();
    if (string.IsNullOrWhiteSpace(templatesString) || templatesString.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        templateNames.AddRange(SummaryTemplate.Presets.AvailableTemplates);
    }
    else
    {
        templateNames.AddRange(templatesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
    
    if (templateNames.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]Error: No templates specified[/]");
        return 1;
    }
    
    SpectreProgressService.WriteHeader("DocSummarizer", "Template Benchmark");
    
    // Display benchmark info
    var infoTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Blue);
    
    infoTable.AddColumn("[blue]Property[/]");
    infoTable.AddColumn("[blue]Value[/]");
    infoTable.AddRow("[cyan]Document[/]", Markup.Escape(file.Name));
    infoTable.AddRow("[cyan]Templates[/]", $"[green]{templateNames.Count}[/] ({string.Join(", ", templateNames.Take(5))}{(templateNames.Count > 5 ? "..." : "")})");
    if (!string.IsNullOrEmpty(focus)) infoTable.AddRow("[cyan]Focus[/]", Markup.Escape(focus));
    
    AnsiConsole.Write(infoTable);
    AnsiConsole.WriteLine();
    
    // Load config
    var config = ConfigurationLoader.Load(configPath);
    config.Output.Verbose = verbose;
    
    // Read the document content
    var extension = file.Extension.ToLowerInvariant();
    string markdown;
    
    if (extension is ".md" or ".txt" or ".text")
    {
        markdown = await File.ReadAllTextAsync(file.FullName, cancellationToken);
    }
    else if (extension is ".zip")
    {
        // Extract text from ZIP (e.g., Project Gutenberg archives)
        AnsiConsole.MarkupLine("[cyan]Extracting text from ZIP archive...[/]");
        var archiveInfo = ArchiveHandler.InspectArchive(file.FullName);
        if (archiveInfo == null || !archiveInfo.IsValid)
        {
            AnsiConsole.MarkupLine($"[red]Error: {archiveInfo?.Error ?? "Invalid archive"}[/]");
            return 1;
        }
        if (verbose)
        {
            var gutenbergTag = archiveInfo.IsGutenberg ? " (Gutenberg)" : "";
            AnsiConsole.MarkupLine($"[dim]Archive: {Markup.Escape(archiveInfo.MainFileName ?? "unknown")} ({archiveInfo.MainFileSize / 1024:N0} KB){gutenbergTag}[/]");
        }
        markdown = await ArchiveHandler.ExtractTextAsync(file.FullName, ct: cancellationToken);
        AnsiConsole.MarkupLine($"[green]Extracted {markdown.Length / 1024:N0} KB of text[/]");
    }
    else
    {
        // Use Docling for PDF/DOCX
        AnsiConsole.MarkupLine("[cyan]Converting document with Docling...[/]");
        var docling = new DoclingClient(config.Docling);
        markdown = await SpectreProgressService.RunConversionWithProgressAsync(
            docling,
            file.FullName,
            $"Converting {file.Name}");
        AnsiConsole.MarkupLine("[green]Document converted[/]");
    }
    
    var docId = Path.GetFileNameWithoutExtension(file.Name);
    
    // Create extraction and retrieval configs from loaded config
    var extractionConfig = config.Extraction.ToExtractionConfig();
    var retrievalConfig = config.Retrieval.ToRetrievalConfig();
    
    // Apply adaptive retrieval settings
    config.AdaptiveRetrieval.ApplyTo(retrievalConfig);
    
    if (verbose)
    {
        AnsiConsole.MarkupLine($"[dim]Extraction: ratio={extractionConfig.ExtractionRatio}, MMR={extractionConfig.MmrLambda}[/]");
        AnsiConsole.MarkupLine($"[dim]Retrieval: TopK={retrievalConfig.TopK}, Adaptive={retrievalConfig.AdaptiveTopK}, MaxTopK={retrievalConfig.MaxTopK}[/]");
    }
    
    // Create benchmark service with config-loaded settings
    await using var benchmarkService = new TemplateBenchmarkService(
        config.Onnx,
        new OllamaService(config.Ollama.Model, 
            timeout: TimeSpan.FromSeconds(config.Ollama.TimeoutSeconds),
            classifierModel: config.Ollama.ClassifierModel),
        config.BertRag,
        extractionConfig: extractionConfig,
        retrievalConfig: retrievalConfig,
        verbose: verbose);
    
    AnsiConsole.WriteLine();
    
    // Run benchmark
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var results = await benchmarkService.BenchmarkTemplatesAsync(
        docId,
        markdown,
        templateNames,
        focus,
        ct: cancellationToken);
    sw.Stop();
    
    AnsiConsole.WriteLine();
    
    // Display results table
    var resultsTable = new Table()
        .Border(TableBorder.Double)
        .BorderColor(Color.Green)
        .Title("[green]Template Benchmark Results[/]");
    
    resultsTable.AddColumn(new TableColumn("[green]Template[/]").LeftAligned());
    resultsTable.AddColumn(new TableColumn("[green]Target[/]").RightAligned());
    resultsTable.AddColumn(new TableColumn("[green]Actual[/]").RightAligned());
    resultsTable.AddColumn(new TableColumn("[green]Diff[/]").RightAligned());
    resultsTable.AddColumn(new TableColumn("[green]Time[/]").RightAligned());
    
    foreach (var r in results.TemplateResults)
    {
        if (r.Success)
        {
            var diff = r.ActualWordCount - r.TargetWords;
            var diffStr = r.TargetWords > 0 ? $"{diff:+#;-#;0}" : "n/a";
            var diffColor = r.TargetWords > 0 
                ? (Math.Abs(diff) <= r.TargetWords * 0.2 ? "green" : Math.Abs(diff) <= r.TargetWords * 0.5 ? "yellow" : "red")
                : "dim";
            
            resultsTable.AddRow(
                $"[yellow]{Markup.Escape(r.TemplateName)}[/]",
                r.TargetWords > 0 ? $"{r.TargetWords}" : "[dim]auto[/]",
                $"{r.ActualWordCount}",
                $"[{diffColor}]{diffStr}[/]",
                $"{r.SynthesisTime.TotalSeconds:F2}s");
        }
        else
        {
            resultsTable.AddRow(
                $"[red]{Markup.Escape(r.TemplateName)}[/]",
                $"{r.TargetWords}",
                "[red]FAILED[/]",
                "-",
                "-");
        }
    }
    
    AnsiConsole.Write(resultsTable);
    AnsiConsole.WriteLine();
    
    // Summary stats
    var successCount = results.TemplateResults.Count(r => r.Success);
    var avgSynthesisTime = results.TemplateResults.Where(r => r.Success).Average(r => r.SynthesisTime.TotalSeconds);
    
    AnsiConsole.MarkupLine($"[cyan]Extraction:[/] {results.TotalSegments} segments, {results.RetrievedSegments} retrieved in {results.TotalExtractionTime.TotalSeconds:F2}s");
    AnsiConsole.MarkupLine($"[cyan]Synthesis:[/] {successCount}/{templateNames.Count} templates, avg {avgSynthesisTime:F2}s per template");
    AnsiConsole.MarkupLine($"[cyan]Total:[/] {sw.Elapsed.TotalSeconds:F1}s");
    AnsiConsole.WriteLine();
    
    // Save results
    var effectiveOutputDir = outputDir ?? Path.GetDirectoryName(file.FullName) ?? Environment.CurrentDirectory;
    await benchmarkService.SaveResultsAsync(results, effectiveOutputDir, docId);
    
    AnsiConsole.MarkupLine($"[green]Results saved to:[/] {effectiveOutputDir}");
    
    return 0;
});
rootCommand.Subcommands.Add(benchmarkTemplatesCommand);

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
        ("prose", "~400", "Clean multi-paragraph prose (no metadata)"),
        ("brief", "~50", "Quick scanning"),
        ("oneliner", "~25", "Single sentence"),
        ("strict", "~60", "Ultra-concise, no fluff"),
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

// Search command - RAG debugger showing top segments with scores (no LLM)
var searchCommand = new Command("search", "Search document(s) for relevant segments (no LLM, shows RAG debug info)");
var searchFileOption = new Option<FileInfo?>("--file", "-f") { Description = "Document to search" };
var searchDirOption = new Option<DirectoryInfo?>("--directory", "-d") { Description = "Directory of documents to search" };
var searchQueryOption = new Option<string?>("--query", "-q") { Description = "Search query (required)" };
var searchTopKOption = new Option<int>("--top", "-k") { Description = "Number of results to return", DefaultValueFactory = _ => 10 };
var searchConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
var searchShowContentOption = new Option<bool>("--content") { Description = "Show full segment content", DefaultValueFactory = _ => false };
var searchJsonOption = new Option<bool>("--json") { Description = "Output as JSON", DefaultValueFactory = _ => false };
var searchExtOption = new Option<string?>("--ext") { Description = "File extensions to include (e.g., '.md,.txt')", DefaultValueFactory = _ => ".md" };

searchCommand.Options.Add(searchFileOption);
searchCommand.Options.Add(searchDirOption);
searchCommand.Options.Add(searchQueryOption);
searchCommand.Options.Add(searchTopKOption);
searchCommand.Options.Add(searchConfigOption);
searchCommand.Options.Add(searchShowContentOption);
searchCommand.Options.Add(searchJsonOption);
searchCommand.Options.Add(searchExtOption);

searchCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(searchFileOption);
    var directory = parseResult.GetValue(searchDirOption);
    var query = parseResult.GetValue(searchQueryOption);
    var topK = parseResult.GetValue(searchTopKOption);
    var configPath = parseResult.GetValue(searchConfigOption);
    var showContent = parseResult.GetValue(searchShowContentOption);
    var outputJson = parseResult.GetValue(searchJsonOption);
    var extFilter = parseResult.GetValue(searchExtOption) ?? ".md";
    
    if (file == null && directory == null)
    {
        AnsiConsole.MarkupLine("[red]Error: --file or --directory is required[/]");
        return 1;
    }
    
    if (string.IsNullOrEmpty(query))
    {
        AnsiConsole.MarkupLine("[red]Error: --query is required[/]");
        return 1;
    }
    
    var config = ConfigurationLoader.Load(configPath);
    
    // Build list of files to search
    var filesToSearch = new List<FileInfo>();
    
    if (file != null)
    {
        if (!file.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error: File not found: {file.FullName}[/]");
            return 1;
        }
        filesToSearch.Add(file);
    }
    
    if (directory != null)
    {
        if (!directory.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error: Directory not found: {directory.FullName}[/]");
            return 1;
        }
        
        var extensions = extFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var ext in extensions)
        {
            var pattern = ext.StartsWith(".") ? $"*{ext}" : $"*.{ext}";
            filesToSearch.AddRange(directory.GetFiles(pattern, SearchOption.AllDirectories)
                .Where(f => !f.Name.Contains("_summary") && !f.Name.Contains(".summary")));
        }
    }
    
    if (filesToSearch.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No matching files found[/]");
        return 0;
    }
    
    // Check if Qdrant is available for fast search
    var detected = await ServiceDetector.DetectSilentAsync(config);
    // Use Qdrant if available, regardless of config (search benefits from persistence)
    var useQdrant = detected.QdrantAvailable;
    
    if (!outputJson)
    {
        SpectreProgressService.WriteHeader("DocSummarizer", "Semantic Search");
        AnsiConsole.MarkupLine($"[cyan]Query:[/] {Markup.Escape(query)}");
        AnsiConsole.MarkupLine($"[cyan]Documents:[/] {filesToSearch.Count} file(s)");
        AnsiConsole.MarkupLine($"[cyan]Backend:[/] {(useQdrant ? "Qdrant (persistent)" : "In-memory (extract on demand)")}");
        AnsiConsole.WriteLine();
    }
    
    // Create extraction and retrieval configs
    var extractionConfig = config.Extraction.ToExtractionConfig();
    var retrievalConfig = config.Retrieval.ToRetrievalConfig();
    retrievalConfig.TopK = topK;
    
    // Create the embedding service for query embedding
    using var embeddingService = new OnnxEmbeddingService(config.Onnx, verbose: false);
    
    // Embed query once
    if (!outputJson)
    {
        AnsiConsole.MarkupLine("[dim]Embedding query...[/]");
    }
    var queryEmbedding = await embeddingService.EmbedAsync(query, cancellationToken);
    
    // Collect all segments from all documents with source tracking
    var allScoredSegments = new List<(string FileName, Segment Segment, double QueryScore, double SalienceScore)>();
    
    var qdrantSucceeded = false;
    if (useQdrant)
    {
        try
        {
            // Use Qdrant for fast vector search across indexed documents
            var collectionName = config.BertRag.CollectionName;
            await using var vectorStore = new QdrantVectorStore(config.Qdrant, verbose: false, deleteOnDispose: false);
            await vectorStore.InitializeAsync(collectionName, 384, cancellationToken);
            
            if (!outputJson)
            {
                AnsiConsole.MarkupLine($"[dim]Searching Qdrant collection '{collectionName}'...[/]");
            }
            
            // Search across ALL indexed documents (docId: null = no filter)
            var results = await vectorStore.SearchAsync(collectionName, queryEmbedding, topK * 2, docId: null, cancellationToken);
            
            foreach (var segment in results)
            {
                // Extract filename from docId (format: filename_contenthash)
                var fileName = segment.Id.Contains('_') 
                    ? segment.Id.Substring(0, segment.Id.LastIndexOf('_')) + ".md"
                    : segment.Id;
                allScoredSegments.Add((fileName, segment, segment.QuerySimilarity, segment.SalienceScore));
            }
            
            if (!outputJson && results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No indexed documents found in Qdrant. Falling back to on-demand extraction...[/]");
            }
            else
            {
                qdrantSucceeded = true;
            }
        }
        catch (Exception ex)
        {
            if (!outputJson)
            {
                AnsiConsole.MarkupLine($"[yellow]Qdrant search failed ({ex.GetType().Name}). Falling back to on-demand extraction...[/]");
            }
        }
    }
    
    if (!qdrantSucceeded)
    {
        // Fall back to on-demand extraction (slower but works without Qdrant)
        using var extractor = new SegmentExtractor(config.Onnx, extractionConfig, verbose: false);
        
        var processedFiles = 0;
        foreach (var searchFile in filesToSearch)
        {
            processedFiles++;
            if (!outputJson && filesToSearch.Count > 1)
            {
                AnsiConsole.MarkupLine($"[dim]Processing ({processedFiles}/{filesToSearch.Count}): {searchFile.Name}[/]");
            }
            
            try
            {
                // Read document content
                var extension = searchFile.Extension.ToLowerInvariant();
                string markdown;
                
                if (extension is ".md" or ".txt" or ".text")
                {
                    markdown = await File.ReadAllTextAsync(searchFile.FullName, cancellationToken);
                }
                else
                {
                    // Use Docling for PDF/DOCX
                    var docling = new DoclingClient(config.Docling);
                    markdown = await docling.ConvertAsync(searchFile.FullName);
                }
                
                var docId = Path.GetFileNameWithoutExtension(searchFile.Name);
                
                // Extract segments
                var extractionResult = await extractor.ExtractAsync(docId, markdown, ct: cancellationToken);
                var segments = extractionResult.AllSegments;
                
                // Score segments against query
                foreach (var segment in segments.Where(s => s.Embedding != null))
                {
                    var queryScore = ComputeCosineSimilarity(queryEmbedding, segment.Embedding!);
                    allScoredSegments.Add((searchFile.Name, segment, queryScore, segment.SalienceScore));
                }
            }
            catch (Exception ex)
            {
                if (!outputJson)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: Failed to process {searchFile.Name}: {ex.Message}[/]");
                }
            }
        }
    }
    
    // Rank all segments across all documents
    var scoredSegments = allScoredSegments
        .OrderByDescending(x => x.QueryScore * retrievalConfig.Alpha + x.SalienceScore * (1 - retrievalConfig.Alpha))
        .Take(topK)
        .ToList();
    
    if (outputJson)
    {
        // Output as JSON for programmatic use
        var results = scoredSegments.Select(x => new {
            file = x.FileName,
            index = x.Segment.Index,
            section = x.Segment.SectionTitle,
            queryScore = Math.Round(x.QueryScore, 4),
            salienceScore = Math.Round(x.SalienceScore, 4),
            combinedScore = Math.Round(x.QueryScore * retrievalConfig.Alpha + x.SalienceScore * (1 - retrievalConfig.Alpha), 4),
            wordCount = x.Segment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
            preview = x.Segment.Text.Length > 200 ? x.Segment.Text[..200] + "..." : x.Segment.Text,
            content = showContent ? x.Segment.Text : null
        });
        var json = System.Text.Json.JsonSerializer.Serialize(new { 
            query, 
            documentsSearched = filesToSearch.Count,
            totalSegments = allScoredSegments.Count,
            results 
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
    else
    {
        // Display results table
        AnsiConsole.MarkupLine($"[dim]Found {allScoredSegments.Count} segments across {filesToSearch.Count} file(s), showing top {scoredSegments.Count}[/]");
        AnsiConsole.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title("[cyan]Search Results[/]");
        
        table.AddColumn(new TableColumn("[cyan]#[/]").RightAligned());
        if (filesToSearch.Count > 1)
            table.AddColumn(new TableColumn("[cyan]File[/]").LeftAligned());
        table.AddColumn(new TableColumn("[cyan]Section[/]").LeftAligned());
        table.AddColumn(new TableColumn("[cyan]Query[/]").RightAligned());
        table.AddColumn(new TableColumn("[cyan]Salience[/]").RightAligned());
        table.AddColumn(new TableColumn("[cyan]Combined[/]").RightAligned());
        table.AddColumn(new TableColumn("[cyan]Preview[/]").LeftAligned());
        
        var rank = 1;
        foreach (var result in scoredSegments)
        {
            var combined = result.QueryScore * retrievalConfig.Alpha + result.SalienceScore * (1 - retrievalConfig.Alpha);
            var queryColor = result.QueryScore > 0.7 ? "green" : result.QueryScore > 0.5 ? "yellow" : "white";
            var preview = result.Segment.Text.Length > 60 
                ? result.Segment.Text[..60].Replace("\n", " ") + "..." 
                : result.Segment.Text.Replace("\n", " ");
            
            if (filesToSearch.Count > 1)
            {
                table.AddRow(
                    $"{rank}",
                    $"[dim]{Markup.Escape(result.FileName)}[/]",
                    Markup.Escape(string.IsNullOrEmpty(result.Segment.SectionTitle) ? "[no section]" : result.Segment.SectionTitle),
                    $"[{queryColor}]{result.QueryScore:F3}[/]",
                    $"{result.SalienceScore:F3}",
                    $"[cyan]{combined:F3}[/]",
                    $"[dim]{Markup.Escape(preview)}[/]");
            }
            else
            {
                table.AddRow(
                    $"{rank}",
                    Markup.Escape(string.IsNullOrEmpty(result.Segment.SectionTitle) ? "[no section]" : result.Segment.SectionTitle),
                    $"[{queryColor}]{result.QueryScore:F3}[/]",
                    $"{result.SalienceScore:F3}",
                    $"[cyan]{combined:F3}[/]",
                    $"[dim]{Markup.Escape(preview)}[/]");
            }
            rank++;
        }
        
        AnsiConsole.Write(table);
        
        // Show full content if requested
        if (showContent)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[cyan]Full Content:[/]");
            foreach (var result in scoredSegments)
            {
                var headerText = filesToSearch.Count > 1 
                    ? $" [{result.FileName}] {(string.IsNullOrEmpty(result.Segment.SectionTitle) ? $"Segment {result.Segment.Index}" : result.Segment.SectionTitle)} "
                    : $" {(string.IsNullOrEmpty(result.Segment.SectionTitle) ? $"Segment {result.Segment.Index}" : result.Segment.SectionTitle)} ";
                var panel = new Panel(Markup.Escape(result.Segment.Text))
                {
                    Header = new PanelHeader(headerText, Justify.Left),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Grey),
                    Padding = new Padding(1, 0)
                };
                AnsiConsole.Write(panel);
            }
        }
    }
    
    return 0;
});
rootCommand.Subcommands.Add(searchCommand);

// Cache command - manage the vector cache
var cacheCommand = new Command("cache", "Manage the document vector cache");

// Cache stats subcommand
var cacheStatsCommand = new Command("stats", "Show cache statistics");
var cacheStatsConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
cacheStatsCommand.Options.Add(cacheStatsConfigOption);
cacheStatsCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var configPath = parseResult.GetValue(cacheStatsConfigOption);
    var config = ConfigurationLoader.Load(configPath);
    
    SpectreProgressService.WriteHeader("DocSummarizer", "Cache Statistics");
    
    // Check if Qdrant is available
    var detected = await ServiceDetector.DetectSilentAsync(config);
    if (!detected.QdrantAvailable)
    {
        AnsiConsole.MarkupLine("[yellow]Qdrant not available[/] - no persistent cache");
        return 0;
    }
    
    var qdrant = new QdrantHttpClient(config.Qdrant.Host, config.Qdrant.Port, config.Qdrant.ApiKey);
    var collections = (await qdrant.ListCollectionsAsync()).ToList();
    
    if (collections.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No cached documents found[/]");
        return 0;
    }
    
    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Cyan1)
        .Title("[cyan]Cached Documents[/]");
    
    table.AddColumn(new TableColumn("[cyan]Collection[/]").LeftAligned());
    table.AddColumn(new TableColumn("[cyan]Vectors[/]").RightAligned());
    table.AddColumn(new TableColumn("[cyan]Size[/]").RightAligned());
    
    long totalVectors = 0;
    foreach (var collection in collections.OrderBy(c => c))
    {
        try
        {
            var info = await qdrant.GetCollectionInfoAsync(collection);
            var vectors = info?.VectorsCount ?? 0;
            totalVectors += vectors;
            
            table.AddRow(
                Markup.Escape(collection),
                $"{vectors:N0}",
                "[dim]-[/]");
        }
        catch
        {
            table.AddRow(Markup.Escape(collection), "[red]error[/]", "-");
        }
    }
    
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[cyan]Total:[/] {collections.Count} documents, {totalVectors:N0} vectors");
    
    return 0;
});
cacheCommand.Subcommands.Add(cacheStatsCommand);

// Cache list subcommand
var cacheListCommand = new Command("list", "List cached documents");
var cacheListConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
cacheListCommand.Options.Add(cacheListConfigOption);
cacheListCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var configPath = parseResult.GetValue(cacheListConfigOption);
    var config = ConfigurationLoader.Load(configPath);
    
    var detected = await ServiceDetector.DetectSilentAsync(config);
    if (!detected.QdrantAvailable)
    {
        AnsiConsole.MarkupLine("[yellow]Qdrant not available[/]");
        return 0;
    }
    
    var qdrant = new QdrantHttpClient(config.Qdrant.Host, config.Qdrant.Port, config.Qdrant.ApiKey);
    var collections = await qdrant.ListCollectionsAsync();
    
    foreach (var collection in collections.OrderBy(c => c))
    {
        Console.WriteLine(collection);
    }
    
    return 0;
});
cacheCommand.Subcommands.Add(cacheListCommand);

// Cache rm subcommand
var cacheRmCommand = new Command("rm", "Remove cached document(s)");
var cacheRmDocOption = new Option<string?>("--doc", "-d") { Description = "Document name/pattern to remove (supports wildcards)" };
var cacheRmAllOption = new Option<bool>("--all") { Description = "Remove all cached documents", DefaultValueFactory = _ => false };
var cacheRmConfigOption = new Option<string?>("--config", "-c") { Description = "Configuration file path" };
cacheRmCommand.Options.Add(cacheRmDocOption);
cacheRmCommand.Options.Add(cacheRmAllOption);
cacheRmCommand.Options.Add(cacheRmConfigOption);
cacheRmCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var doc = parseResult.GetValue(cacheRmDocOption);
    var removeAll = parseResult.GetValue(cacheRmAllOption);
    var configPath = parseResult.GetValue(cacheRmConfigOption);
    
    if (string.IsNullOrEmpty(doc) && !removeAll)
    {
        AnsiConsole.MarkupLine("[red]Error: Specify --doc or --all[/]");
        return 1;
    }
    
    var config = ConfigurationLoader.Load(configPath);
    
    var detected = await ServiceDetector.DetectSilentAsync(config);
    if (!detected.QdrantAvailable)
    {
        AnsiConsole.MarkupLine("[yellow]Qdrant not available[/]");
        return 0;
    }
    
    var qdrant = new QdrantHttpClient(config.Qdrant.Host, config.Qdrant.Port, config.Qdrant.ApiKey);
    var collections = await qdrant.ListCollectionsAsync();
    
    var toRemove = new List<string>();
    
    if (removeAll)
    {
        toRemove.AddRange(collections);
    }
    else if (!string.IsNullOrEmpty(doc))
    {
        // Simple wildcard matching
        var pattern = doc.Replace("*", ".*").Replace("?", ".");
        var regex = new System.Text.RegularExpressions.Regex($"^{pattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        toRemove.AddRange(collections.Where(c => regex.IsMatch(c)));
    }
    
    if (toRemove.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No matching documents found[/]");
        return 0;
    }
    
    AnsiConsole.MarkupLine($"[yellow]Removing {toRemove.Count} document(s):[/]");
    foreach (var collection in toRemove)
    {
        AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(collection)}[/]");
    }
    
    if (!AnsiConsole.Confirm("Continue?", false))
    {
        return 0;
    }
    
    foreach (var collection in toRemove)
    {
        try
        {
            await qdrant.DeleteCollectionAsync(collection);
            AnsiConsole.MarkupLine($"[green]Removed:[/] {Markup.Escape(collection)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed:[/] {Markup.Escape(collection)} - {ex.Message}");
        }
    }
    
    return 0;
});
cacheCommand.Subcommands.Add(cacheRmCommand);

rootCommand.Subcommands.Add(cacheCommand);

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
            // Avoid double _summary suffix
            if (baseName.EndsWith("_summary", StringComparison.OrdinalIgnoreCase))
                baseName = baseName[..^8];
            var summaryPath = Path.Combine(fileDir, $"{baseName}_summary.md");
            
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
    
    // Determine effective output directory
    var effectiveOutputDir = config.Output.OutputDirectory ?? directoryPath;
    var outputInSourceDir = string.Equals(
        Path.GetFullPath(effectiveOutputDir), 
        Path.GetFullPath(directoryPath), 
        StringComparison.OrdinalIgnoreCase);
    
    // Warn if output is going to source directory (files with _summary suffix will be auto-skipped)
    if (outputInSourceDir && config.Output.Format != OutputFormat.Console)
    {
        AnsiConsole.MarkupLine("[yellow]Note:[/] Output files will be saved alongside source files.");
        AnsiConsole.MarkupLine("[dim]  Files ending in _summary will be automatically skipped to prevent loops.[/]");
        AnsiConsole.WriteLine();
    }

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
                    // Use --output-dir if specified, otherwise save next to source file
                    var outputDir = config.Output.OutputDirectory ?? Path.GetDirectoryName(result.FilePath);
                    await OutputFormatter.WriteOutputAsync(output, config.Output, fileName, outputDir);
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

// Helper: Compute cosine similarity between two vectors
static double ComputeCosineSimilarity(float[] a, float[] b)
{
    if (a.Length != b.Length) return 0;
    
    double dotProduct = 0, normA = 0, normB = 0;
    for (var i = 0; i < a.Length; i++)
    {
        dotProduct += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    
    var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
    return denominator > 0 ? dotProduct / denominator : 0;
}
