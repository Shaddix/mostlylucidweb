using Microsoft.Extensions.Configuration;
using Mostlylucid.BlogLLM.Models;
using Mostlylucid.BlogLLM.Services;
using Spectre.Console;
using System.Diagnostics;

namespace Mostlylucid.BlogLLM;

class Program
{
    private static IConfiguration? _config;
    private static string _modelPath = string.Empty;
    private static string _tokenizerPath = string.Empty;
    private static int _dimensions = 384;
    private static bool _useGpu = false;
    private static string _qdrantHost = "localhost";
    private static int _qdrantPort = 6334;
    private static string _collectionName = "blog_knowledge_base";
    private static int _maxChunkTokens = 512;
    private static int _minChunkTokens = 100;
    private static int _overlapTokens = 50;

    static async Task Main(string[] args)
    {
        ShowBanner();
        LoadConfiguration();

        while (true)
        {
            var choice = ShowMainMenu();

            switch (choice)
            {
                case "ingest":
                    await IngestDocuments();
                    break;
                case "search":
                    await SearchKnowledgeBase();
                    break;
                case "config":
                    ConfigureSettings();
                    break;
                case "stats":
                    await ShowStatistics();
                    break;
                case "delete":
                    await DeleteCollection();
                    break;
                case "exit":
                    return;
            }
        }
    }

    static void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("mostlylucid.blogllm")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]RAG Knowledge Base Builder - CPU Optimized[/]");
        AnsiConsole.WriteLine();
    }

    static void LoadConfiguration()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var section = _config.GetSection("BlogRag");

        _modelPath = section["EmbeddingModel:ModelPath"] ?? "./models/bge-small-en-v1.5-onnx/model.onnx";
        _tokenizerPath = section["EmbeddingModel:TokenizerPath"] ?? "./models/bge-small-en-v1.5-onnx/tokenizer.json";
        _dimensions = int.Parse(section["EmbeddingModel:Dimensions"] ?? "384");
        _useGpu = bool.Parse(section["EmbeddingModel:UseGpu"] ?? "false");

        _qdrantHost = section["VectorStore:Host"] ?? "localhost";
        _qdrantPort = int.Parse(section["VectorStore:Port"] ?? "6334");
        _collectionName = section["VectorStore:CollectionName"] ?? "blog_knowledge_base";

        _maxChunkTokens = int.Parse(section["Chunking:MaxChunkTokens"] ?? "512");
        _minChunkTokens = int.Parse(section["Chunking:MinChunkTokens"] ?? "100");
        _overlapTokens = int.Parse(section["Chunking:OverlapTokens"] ?? "50");
    }

    static string ShowMainMenu()
    {
        AnsiConsole.WriteLine();
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to do?[/]")
                .PageSize(10)
                .AddChoices(new[]
                {
                    "ingest", "search", "config", "stats", "delete", "exit"
                })
                .UseConverter(choice => choice switch
                {
                    "ingest" => "📄 Ingest Markdown Documents",
                    "search" => "🔍 Search Knowledge Base",
                    "config" => "⚙️  Configure Settings",
                    "stats" => "📊 Show Statistics",
                    "delete" => "🗑️  Delete Collection",
                    "exit" => "👋 Exit",
                    _ => choice
                }));

        return choice;
    }

    static async Task IngestDocuments()
    {
        var path = AnsiConsole.Ask<string>("[yellow]Enter path to markdown file or directory:[/]");

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            AnsiConsole.MarkupLine("[red]Path does not exist![/]");
            return;
        }

        var files = File.Exists(path)
            ? new[] { path }
            : Directory.GetFiles(path, "*.md", SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No markdown files found![/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found {files.Length} markdown file(s)[/]");

        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var sw = Stopwatch.StartNew();

                // Initialize services
                AnsiConsole.MarkupLine("[dim]Initializing services...[/]");
                var parser = new MarkdownParserService();
                var chunker = new ChunkingService(_tokenizerPath, _maxChunkTokens, _minChunkTokens, _overlapTokens);

                using var embedder = new EmbeddingService(_modelPath, _tokenizerPath, _dimensions, _useGpu);
                var vectorStore = new VectorStoreService(_qdrantHost, _qdrantPort, _collectionName);

                // Ensure collection exists
                if (!await vectorStore.CollectionExistsAsync())
                {
                    await vectorStore.CreateCollectionAsync((ulong)_dimensions);
                }

                // Parse documents
                var parseTask = ctx.AddTask("[green]Parsing documents...[/]", maxValue: files.Length);
                var documents = new List<BlogDocument>();

                foreach (var file in files)
                {
                    try
                    {
                        var doc = parser.ParseFile(file);
                        documents.Add(doc);
                        parseTask.Increment(1);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error parsing {file}: {ex.Message}[/]");
                    }
                }

                // Chunk documents
                var chunkTask = ctx.AddTask("[green]Chunking documents...[/]", maxValue: documents.Count);
                var allChunks = new List<ContentChunk>();

                foreach (var doc in documents)
                {
                    var chunks = chunker.ChunkDocument(doc);
                    allChunks.AddRange(chunks);
                    chunkTask.Increment(1);
                }

                AnsiConsole.MarkupLine($"[green]Created {allChunks.Count} chunks[/]");

                // Generate embeddings
                var embedTask = ctx.AddTask("[green]Generating embeddings...[/]", maxValue: allChunks.Count);
                var progress = new Progress<(int current, int total)>(p =>
                {
                    embedTask.Value = p.current;
                });

                await embedder.GenerateEmbeddingsAsync(allChunks, progress);

                // Upload to Qdrant
                var uploadTask = ctx.AddTask("[green]Uploading to Qdrant...[/]", maxValue: allChunks.Count);
                var uploadProgress = new Progress<(int current, int total)>(p =>
                {
                    uploadTask.Value = p.current;
                });

                await vectorStore.UpsertChunksAsync(allChunks, uploadProgress);

                sw.Stop();

                var table = new Table()
                    .AddColumn("Metric")
                    .AddColumn("Value")
                    .AddRow("Files processed", files.Length.ToString())
                    .AddRow("Documents parsed", documents.Count.ToString())
                    .AddRow("Chunks created", allChunks.Count.ToString())
                    .AddRow("Total tokens", allChunks.Sum(c => c.TokenCount).ToString("N0"))
                    .AddRow("Time taken", $"{sw.Elapsed.TotalSeconds:F2}s")
                    .AddRow("Chunks/second", $"{allChunks.Count / sw.Elapsed.TotalSeconds:F1}");

                AnsiConsole.WriteLine();
                AnsiConsole.Write(table);
            });
    }

    static async Task SearchKnowledgeBase()
    {
        using var embedder = new EmbeddingService(_modelPath, _tokenizerPath, _dimensions, _useGpu);
        var vectorStore = new VectorStoreService(_qdrantHost, _qdrantPort, _collectionName);

        if (!await vectorStore.CollectionExistsAsync())
        {
            AnsiConsole.MarkupLine("[red]Collection does not exist! Ingest documents first.[/]");
            return;
        }

        while (true)
        {
            var query = AnsiConsole.Ask<string>("\n[yellow]Enter search query (or 'back' to return):[/]");

            if (query.ToLower() == "back") break;

            var limit = AnsiConsole.Ask("[dim]Number of results (default 10):[/]", 10);
            var threshold = AnsiConsole.Ask("[dim]Score threshold (0.0-1.0, default 0.7):[/]", 0.7f);

            var sw = Stopwatch.StartNew();
            var embedding = embedder.GenerateEmbedding(query);
            var results = await vectorStore.SearchAsync(embedding, limit, threshold);
            sw.Stop();

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found![/]");
                continue;
            }

            AnsiConsole.MarkupLine($"\n[green]Found {results.Count} results in {sw.ElapsedMilliseconds}ms[/]\n");

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];

                var panel = new Panel(new Markup(
                    $"[bold]{result.DocumentTitle}[/] → [dim]{result.SectionHeading}[/]\n\n" +
                    $"{result.Text[..Math.Min(300, result.Text.Length)]}...\n\n" +
                    $"[dim]Categories: {string.Join(", ", result.Categories)}[/]"))
                    .Header($"[blue]#{i + 1} - Score: {result.Score:F3}[/]")
                    .BorderColor(Color.Blue)
                    .Padding(1, 1);

                AnsiConsole.Write(panel);
            }
        }
    }

    static void ConfigureSettings()
    {
        var setting = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Which setting would you like to modify?[/]")
                .AddChoices("embedding_model", "qdrant_host", "qdrant_port", "collection_name",
                           "chunk_size", "use_gpu", "back"));

        switch (setting)
        {
            case "embedding_model":
                _modelPath = AnsiConsole.Ask("[yellow]Model path:[/]", _modelPath);
                _tokenizerPath = AnsiConsole.Ask("[yellow]Tokenizer path:[/]", _tokenizerPath);
                break;
            case "qdrant_host":
                _qdrantHost = AnsiConsole.Ask("[yellow]Qdrant host:[/]", _qdrantHost);
                break;
            case "qdrant_port":
                _qdrantPort = AnsiConsole.Ask("[yellow]Qdrant port:[/]", _qdrantPort);
                break;
            case "collection_name":
                _collectionName = AnsiConsole.Ask("[yellow]Collection name:[/]", _collectionName);
                break;
            case "chunk_size":
                _maxChunkTokens = AnsiConsole.Ask("[yellow]Max chunk tokens:[/]", _maxChunkTokens);
                _minChunkTokens = AnsiConsole.Ask("[yellow]Min chunk tokens:[/]", _minChunkTokens);
                _overlapTokens = AnsiConsole.Ask("[yellow]Overlap tokens:[/]", _overlapTokens);
                break;
            case "use_gpu":
                _useGpu = AnsiConsole.Confirm("Use GPU for embeddings?", _useGpu);
                break;
        }

        ShowCurrentConfig();
    }

    static void ShowCurrentConfig()
    {
        var table = new Table()
            .Title("[bold]Current Configuration[/]")
            .AddColumn("Setting")
            .AddColumn("Value")
            .AddRow("Model Path", _modelPath)
            .AddRow("Tokenizer Path", _tokenizerPath)
            .AddRow("Dimensions", _dimensions.ToString())
            .AddRow("Use GPU", _useGpu.ToString())
            .AddRow("Qdrant Host", _qdrantHost)
            .AddRow("Qdrant Port", _qdrantPort.ToString())
            .AddRow("Collection Name", _collectionName)
            .AddRow("Max Chunk Tokens", _maxChunkTokens.ToString())
            .AddRow("Min Chunk Tokens", _minChunkTokens.ToString())
            .AddRow("Overlap Tokens", _overlapTokens.ToString());

        AnsiConsole.Write(table);
    }

    static async Task ShowStatistics()
    {
        var vectorStore = new VectorStoreService(_qdrantHost, _qdrantPort, _collectionName);

        if (!await vectorStore.CollectionExistsAsync())
        {
            AnsiConsole.MarkupLine("[red]Collection does not exist![/]");
            return;
        }

        var count = await vectorStore.GetDocumentCountAsync();

        var chart = new BarChart()
            .Width(60)
            .Label("[bold underline]Knowledge Base Statistics[/]")
            .AddItem("Total Chunks", (int)count, Color.Blue);

        AnsiConsole.Write(chart);

        var table = new Table()
            .AddColumn("Metric")
            .AddColumn("Value")
            .AddRow("Collection Name", _collectionName)
            .AddRow("Total Chunks", count.ToString("N0"))
            .AddRow("Embedding Dimensions", _dimensions.ToString())
            .AddRow("Qdrant Host", $"{_qdrantHost}:{_qdrantPort}");

        AnsiConsole.Write(table);
    }

    static async Task DeleteCollection()
    {
        var confirm = AnsiConsole.Confirm(
            $"[red]Are you sure you want to delete collection '{_collectionName}'?[/]");

        if (!confirm) return;

        var vectorStore = new VectorStoreService(_qdrantHost, _qdrantPort, _collectionName);
        await vectorStore.DeleteCollectionAsync();

        AnsiConsole.MarkupLine("[green]Collection deleted successfully![/]");
    }
}
