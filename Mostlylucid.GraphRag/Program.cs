using Mostlylucid.GraphRag;
using Mostlylucid.GraphRag.Query;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("graphrag");

    config.AddCommand<IndexCommand>("index")
        .WithDescription("Index a directory of markdown files")
        .WithExample("index", "./Markdown");

    config.AddCommand<QueryCommand>("query")
        .WithDescription("Query the indexed corpus")
        .WithExample("query", "What are the main themes?", "--mode", "global");

    config.AddCommand<StatsCommand>("stats")
        .WithDescription("Show database statistics");
});

return await app.RunAsync(args);

// ============================================================================
// Commands
// ============================================================================

public class IndexCommand : AsyncCommand<IndexCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("Path to directory containing markdown files")]
        public string Path { get; set; } = "";

        [CommandOption("-d|--database")]
        [Description("Database file path")]
        [DefaultValue("graphrag.duckdb")]
        public string Database { get; set; } = "graphrag.duckdb";

        [CommandOption("-u|--ollama-url")]
        [Description("Ollama API URL")]
        [DefaultValue("http://localhost:11434")]
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        [CommandOption("-m|--model")]
        [Description("Ollama model for LLM tasks")]
        [DefaultValue("llama3.2:3b")]
        public string Model { get; set; } = "llama3.2:3b";

        [CommandOption("-e|--extraction-mode")]
        [Description("Entity extraction mode: heuristic (fast, no per-chunk LLM) or llm (MSFT-style, 2 LLM calls per chunk)")]
        [DefaultValue("heuristic")]
        public string ExtractionMode { get; set; } = "heuristic";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!Directory.Exists(settings.Path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {settings.Path}");
            return 1;
        }

        var extractionMode = settings.ExtractionMode.ToLowerInvariant() switch
        {
            "llm" => ExtractionMode.Llm,
            "msft" => ExtractionMode.Llm,
            _ => ExtractionMode.Heuristic
        };

        var config = new GraphRagConfig
        {
            DatabasePath = settings.Database,
            OllamaUrl = settings.OllamaUrl,
            Model = settings.Model,
            ExtractionMode = extractionMode
        };

        var modeColor = extractionMode == ExtractionMode.Llm ? "yellow" : "green";
        var modeLabel = extractionMode == ExtractionMode.Llm ? "LLM (MSFT-style)" : "Heuristic (IDF + signals)";
        
        AnsiConsole.MarkupLine($"[bold blue]GraphRAG Indexer[/]");
        AnsiConsole.MarkupLine($"  Source: [green]{settings.Path}[/]");
        AnsiConsole.MarkupLine($"  Database: [green]{settings.Database}[/]");
        AnsiConsole.MarkupLine($"  Model: [green]{settings.Model}[/]");
        AnsiConsole.MarkupLine($"  Extraction: [{modeColor}]{modeLabel}[/]");
        AnsiConsole.WriteLine();

        using var pipeline = new GraphRagPipeline(config);

        await AnsiConsole.Status()
            .StartAsync("Initializing...", async ctx =>
            {
                await pipeline.InitializeAsync();
            });

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var indexTask = ctx.AddTask("[green]Indexing documents[/]");
                var extractTask = ctx.AddTask("[yellow]Extracting entities[/]");
                var communityTask = ctx.AddTask("[blue]Detecting communities[/]");
                var summarizeTask = ctx.AddTask("[magenta]Generating summaries[/]");

                var progress = new Progress<PipelineProgress>(p =>
                {
                    switch (p.Phase)
                    {
                        case PipelinePhase.Indexing:
                            indexTask.Value = p.Percentage;
                            indexTask.Description = $"[green]{p.Message}[/]";
                            break;
                        case PipelinePhase.EntityExtraction:
                            indexTask.Value = 100;
                            extractTask.Value = p.Percentage;
                            extractTask.Description = $"[yellow]{p.Message}[/]";
                            break;
                        case PipelinePhase.CommunityDetection:
                            extractTask.Value = 100;
                            communityTask.Value = p.Percentage;
                            communityTask.Description = $"[blue]{p.Message}[/]";
                            break;
                        case PipelinePhase.Summarization:
                            communityTask.Value = 100;
                            summarizeTask.Value = p.Percentage;
                            summarizeTask.Description = $"[magenta]{p.Message}[/]";
                            break;
                        case PipelinePhase.Complete:
                            summarizeTask.Value = 100;
                            break;
                    }
                });

                await pipeline.IndexAsync(settings.Path, progress);
            });

        // Show final stats
        var stats = await pipeline.GetStatsAsync();
        
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold green]Indexing Complete[/]"));
        
        var table = new Table()
            .AddColumn("Metric")
            .AddColumn("Count");
        
        table.AddRow("Documents", stats.DocumentCount.ToString());
        table.AddRow("Chunks", stats.ChunkCount.ToString());
        table.AddRow("Entities", stats.EntityCount.ToString());
        table.AddRow("Relationships", stats.RelationshipCount.ToString());
        table.AddRow("Communities", stats.CommunityCount.ToString());
        
        AnsiConsole.Write(table);

        return 0;
    }
}

public class QueryCommand : AsyncCommand<QueryCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<QUERY>")]
        [Description("The query to execute")]
        public string Query { get; set; } = "";

        [CommandOption("-d|--database")]
        [Description("Database file path")]
        [DefaultValue("graphrag.duckdb")]
        public string Database { get; set; } = "graphrag.duckdb";

        [CommandOption("-m|--mode")]
        [Description("Query mode: local, global, or drift")]
        public string? Mode { get; set; }

        [CommandOption("-u|--ollama-url")]
        [Description("Ollama API URL")]
        [DefaultValue("http://localhost:11434")]
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        [CommandOption("--model")]
        [Description("Ollama model")]
        [DefaultValue("llama3.2:3b")]
        public string Model { get; set; } = "llama3.2:3b";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.Database))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Database not found: {settings.Database}");
            AnsiConsole.MarkupLine("Run [yellow]graphrag index[/] first to create the database.");
            return 1;
        }

        var config = new GraphRagConfig
        {
            DatabasePath = settings.Database,
            OllamaUrl = settings.OllamaUrl,
            Model = settings.Model
        };

        QueryMode? mode = settings.Mode?.ToLowerInvariant() switch
        {
            "local" => QueryMode.Local,
            "global" => QueryMode.Global,
            "drift" => QueryMode.Drift,
            _ => null
        };

        using var pipeline = new GraphRagPipeline(config);

        QueryResult result = null!;
        
        await AnsiConsole.Status()
            .StartAsync("Processing query...", async ctx =>
            {
                await pipeline.InitializeAsync();
                result = await pipeline.QueryAsync(settings.Query, mode);
            });

        // Display result
        AnsiConsole.Write(new Rule($"[bold blue]{result.Mode} Search[/]"));
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine($"[bold]Query:[/] {result.Query}");
        AnsiConsole.WriteLine();
        
        var panel = new Panel(result.Answer)
            .Header("[bold green]Answer[/]")
            .Border(BoxBorder.Rounded);
        AnsiConsole.Write(panel);

        if (result.Entities?.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Related Entities:[/] {string.Join(", ", result.Entities.Take(10))}");
        }

        if (result.Sources.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Sources:[/] {result.Sources.Count} chunks (top score: {result.Sources[0].Score:F3})");
        }

        if (result.CommunitiesUsed > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Communities used:[/] {result.CommunitiesUsed}");
        }

        return 0;
    }
}

public class StatsCommand : AsyncCommand<StatsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-d|--database")]
        [Description("Database file path")]
        [DefaultValue("graphrag.duckdb")]
        public string Database { get; set; } = "graphrag.duckdb";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.Database))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Database not found: {settings.Database}");
            return 1;
        }

        var config = new GraphRagConfig { DatabasePath = settings.Database };
        using var pipeline = new GraphRagPipeline(config);
        await pipeline.InitializeAsync();

        var stats = await pipeline.GetStatsAsync();

        AnsiConsole.Write(new Rule("[bold blue]GraphRAG Database Stats[/]"));
        
        var table = new Table()
            .AddColumn("Metric")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("Documents", stats.DocumentCount.ToString("N0"));
        table.AddRow("Chunks", stats.ChunkCount.ToString("N0"));
        table.AddRow("Entities", stats.EntityCount.ToString("N0"));
        table.AddRow("Relationships", stats.RelationshipCount.ToString("N0"));
        table.AddRow("Communities", stats.CommunityCount.ToString("N0"));

        AnsiConsole.Write(table);

        // Show file size
        var fileInfo = new FileInfo(settings.Database);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Database size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB[/]");

        return 0;
    }
}
