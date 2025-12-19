using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Mostlylucid.DataSummarizer.Configuration;
using Mostlylucid.DataSummarizer.Models;
using Mostlylucid.DataSummarizer.Services;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build();

var settings = new DataSummarizerSettings();
configuration.GetSection("DataSummarizer").Bind(settings);

// Options
var fileOption = new Option<string?>("-f", "Path to data file (CSV, Excel, Parquet, JSON)") { IsRequired = false };
fileOption.AddAlias("--file");

var sheetOption = new Option<string?>("-s", "Sheet name for Excel files");
sheetOption.AddAlias("--sheet");

var modelOption = new Option<string?>("--model", () => "qwen2.5-coder:7b", "Ollama model for LLM insights");
modelOption.AddAlias("-m");

var noLlmOption = new Option<bool>("--no-llm", "Skip LLM insights (stats only)");

var verboseOption = new Option<bool>("-v", "Verbose output");
verboseOption.AddAlias("--verbose");

var outputOption = new Option<string?>("-o", "Output file path (default: console)");
outputOption.AddAlias("--output");

var queryOption = new Option<string?>("-q", "Ask a specific question about the data");
queryOption.AddAlias("--query");

var onnxOption = new Option<string?>("--onnx", "Optional ONNX sentinel model path for column scoring");
onnxOption.AddAlias("--onnx-sentinel");

var ingestDirOption = new Option<string?>("--ingest-dir", "Ingest all supported files in a directory into the registry");
var ingestFilesOption = new Option<string[]>("--ingest-files", "Ingest a comma-separated list of files into the registry")
{
    AllowMultipleArgumentsPerToken = true
};
var registryQueryOption = new Option<string?>("--registry-query", "Ask a question across all ingested data (vector search)");
var vectorDbOption = new Option<string?>("--vector-db", () => ".datasummarizer.vss.duckdb", "Path to persistent DuckDB vector store");
var sessionIdOption = new Option<string?>("--session-id", "Conversation/session id for context memory (auto-generates if omitted)");
var synthPathOption = new Option<string?>("--synthesize-to", "Write a synthetic CSV that matches the profiled shape");
var synthRowsOption = new Option<int>("--synthesize-rows", () => 1000, "Rows to generate when synthesizing");

// Profile options for wide tables
var columnsOption = new Option<string[]>("--columns", "Specific columns to analyze (comma-separated)")
{
    AllowMultipleArgumentsPerToken = true
};
var excludeColumnsOption = new Option<string[]>("--exclude-columns", "Columns to exclude from analysis")
{
    AllowMultipleArgumentsPerToken = true
};
var maxColumnsOption = new Option<int?>("--max-columns", "Maximum columns to analyze (0=unlimited). Selects most interesting for wide tables.");
var fastModeOption = new Option<bool>("--fast", "Fast mode: skip expensive pattern detection");
var skipCorrelationsOption = new Option<bool>("--skip-correlations", "Skip correlation analysis (faster for wide tables)");
var ignoreErrorsOption = new Option<bool>("--ignore-errors", "Ignore CSV parsing errors (malformed rows)");
var targetOption = new Option<string?>("--target", "Target column for supervised analysis (e.g. churn flag)");
var markdownOutputOption = new Option<string?>("--markdown-output", "Write markdown report to this path (overrides defaults)");
var noReportOption = new Option<bool>("--no-report", "Skip markdown report generation");
var focusQuestionOption = new Option<string[]>("--focus-question", "Focus question(s) for the LLM-grounded report")
{
    AllowMultipleArgumentsPerToken = true
};

// Subcommands
var profileCmd = new Command("profile", "Profile one or more files and write profile JSON")
{
    fileOption,
    ingestFilesOption,
    ingestDirOption,
    outputOption,
    verboseOption,
    noLlmOption,
    modelOption,
    onnxOption,
    vectorDbOption,
    sessionIdOption
};

var synthCmd = new Command("synth", "Synthesize data from a saved profile (JSON)")
{
    new Option<string>("--profile", "Profile JSON produced by 'profile' command") { IsRequired = true },
    synthPathOption,
    synthRowsOption,
    verboseOption
};

var validateCmd = new Command("validate", "Compare two datasets (or dataset vs synth) and report deltas")
{
    new Option<string>("--source", "Source file or glob") { IsRequired = true },
    new Option<string>("--target", "Target file or glob") { IsRequired = true },
    outputOption,
    verboseOption,
    modelOption,
    noLlmOption,
    vectorDbOption,
    sessionIdOption
};

var rootCommand = new RootCommand("Data summarization tool - profile CSV, Excel, Parquet files")
{
    fileOption,
    sheetOption,
    modelOption,
    noLlmOption,
    verboseOption,
    outputOption,
    queryOption,
    onnxOption,
    ingestDirOption,
    ingestFilesOption,
    registryQueryOption,
    vectorDbOption,
    sessionIdOption,
    synthPathOption,
    synthRowsOption,
    columnsOption,
    excludeColumnsOption,
    maxColumnsOption,
    fastModeOption,
    skipCorrelationsOption,
    ignoreErrorsOption,
    targetOption,
    markdownOutputOption,
    noReportOption,
    focusQuestionOption,
    profileCmd,
    synthCmd,
    validateCmd
};

profileCmd.SetHandler(async ctx =>
{
    var file = ctx.ParseResult.GetValueForOption(fileOption);
    var ingestFiles = ctx.ParseResult.GetValueForOption(ingestFilesOption) ?? Array.Empty<string>();
    var ingestDir = ctx.ParseResult.GetValueForOption(ingestDirOption);
    var output = ctx.ParseResult.GetValueForOption(outputOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var noLlm = ctx.ParseResult.GetValueForOption(noLlmOption);
    var model = ctx.ParseResult.GetValueForOption(modelOption);
    var onnx = ctx.ParseResult.GetValueForOption(onnxOption);
    var vectorDb = ctx.ParseResult.GetValueForOption(vectorDbOption);
    var sessionId = ctx.ParseResult.GetValueForOption(sessionIdOption);

    var sources = CliHelpers.ExpandPatternsHelper(new[] { file }.Concat(ingestFiles), ingestDir);
    if (!sources.Any()) { Console.WriteLine("No sources found."); return; }
    var sid = sessionId ?? Guid.NewGuid().ToString("N");
    var profiles = new List<DataProfile>();
    using var svc = new DataSummarizerService(verbose, noLlm ? null : model, "http://localhost:11434", onnx, vectorDb, sid);
    foreach (var src in sources)
    {
        var report = await svc.SummarizeAsync(src, useLlm: false);
        profiles.Add(report.Profile);
    }
    var outPath = output ?? "profile.json";
    ProfileIo.SaveProfiles(profiles, outPath);
    Console.WriteLine($"Profile saved to {outPath}");
});

var synthProfileOpt = synthCmd.Options.OfType<Option<string>>().First(o => o.Name == "profile");

synthCmd.SetHandler((string profilePath, string? synthOut, int rows, bool verbose) =>
{
    var profiles = ProfileIo.LoadProfiles(profilePath);
    if (profiles.Count == 0) { Console.WriteLine("No profiles found in JSON"); return; }
    var outPath = synthOut ?? "synthetic.csv";
    DataSynthesizer.GenerateCsv(profiles[0], rows, outPath);
    Console.WriteLine($"Synthetic data written to {outPath}");
}, synthProfileOpt, synthPathOption, synthRowsOption, verboseOption);

validateCmd.SetHandler(async ctx =>
{
    var source = ctx.ParseResult.GetValueForOption(validateCmd.Options.OfType<Option<string>>().First(o => o.Name == "source"))!;
    var target = ctx.ParseResult.GetValueForOption(validateCmd.Options.OfType<Option<string>>().First(o => o.Name == "target"))!;
    var output = ctx.ParseResult.GetValueForOption(outputOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var model = ctx.ParseResult.GetValueForOption(modelOption);
    var noLlm = ctx.ParseResult.GetValueForOption(noLlmOption);
    var vectorDb = ctx.ParseResult.GetValueForOption(vectorDbOption);
    var sessionId = ctx.ParseResult.GetValueForOption(sessionIdOption);

    var sid = sessionId ?? Guid.NewGuid().ToString("N");
    var srcFiles = CliHelpers.ExpandPatternsHelper(new[] { source }, null).ToList();
    var tgtFiles = CliHelpers.ExpandPatternsHelper(new[] { target }, null).ToList();
    if (srcFiles.Count == 0 || tgtFiles.Count == 0) { Console.WriteLine("Missing source/target files"); return; }

    using var svc = new DataSummarizerService(verbose, noLlm ? null : model, "http://localhost:11434", null, vectorDb, sid);
    var srcReport = await svc.SummarizeAsync(srcFiles[0], useLlm: false);
    var tgtReport = await svc.SummarizeAsync(tgtFiles[0], useLlm: false);

    var validation = ValidationService.Compare(srcReport.Profile, tgtReport.Profile);
    var json = System.Text.Json.JsonSerializer.Serialize(validation, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    if (!string.IsNullOrEmpty(output))
    {
        File.WriteAllText(output, json);
        Console.WriteLine($"Validation report saved to {output}");
    }
    Console.WriteLine(json);
});

// Tool command - for LLM tool integration, outputs JSON
var toolCmd = new Command("tool", "Profile data and output JSON for LLM tool integration")
{
    fileOption,
    sheetOption,
    targetOption,
    columnsOption,
    excludeColumnsOption,
    maxColumnsOption,
    fastModeOption,
    skipCorrelationsOption,
    ignoreErrorsOption
};

toolCmd.SetHandler(async ctx =>
{
    var file = ctx.ParseResult.GetValueForOption(fileOption);
    var sheet = ctx.ParseResult.GetValueForOption(sheetOption);
    var targetColumn = ctx.ParseResult.GetValueForOption(targetOption);
    var columns = ctx.ParseResult.GetValueForOption(columnsOption);
    var excludeColumns = ctx.ParseResult.GetValueForOption(excludeColumnsOption);
    var maxColumns = ctx.ParseResult.GetValueForOption(maxColumnsOption);
    var fastMode = ctx.ParseResult.GetValueForOption(fastModeOption);
    var skipCorrelations = ctx.ParseResult.GetValueForOption(skipCorrelationsOption);
    var ignoreErrors = ctx.ParseResult.GetValueForOption(ignoreErrorsOption);

    var startTime = DateTime.UtcNow;
    
    try
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            var errorOutput = new ToolOutput
            {
                Success = false,
                Source = file ?? "none",
                Error = file == null ? "File path is required" : $"File not found: {file}"
            };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorOutput, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        var profileOptions = new ProfileOptions
        {
            Columns = columns?.Length > 0 ? columns.ToList() : null,
            ExcludeColumns = excludeColumns?.Length > 0 ? excludeColumns.ToList() : null,
            MaxColumns = maxColumns ?? 50,
            FastMode = fastMode,
            SkipCorrelations = skipCorrelations,
            IgnoreErrors = ignoreErrors,
            TargetColumn = targetColumn
        };

        using var svc = new DataSummarizerService(
            verbose: false,
            ollamaModel: null,
            ollamaUrl: null,
            onnxSentinelPath: null,
            vectorStorePath: null,
            sessionId: null,
            profileOptions: profileOptions
        );

        var report = await svc.SummarizeAsync(file, sheet, useLlm: false);
        var processingTime = DateTime.UtcNow - startTime;

        // Convert to tool output format
        var toolProfile = new ToolProfile
        {
            SourcePath = report.Profile.SourcePath,
            RowCount = report.Profile.RowCount,
            ColumnCount = report.Profile.ColumnCount,
            ExecutiveSummary = report.ExecutiveSummary,
            Columns = report.Profile.Columns.Select(c => new ToolColumnProfile
            {
                Name = c.Name,
                Type = c.InferredType.ToString(),
                Role = c.SemanticRole != SemanticRole.Unknown ? c.SemanticRole.ToString() : null,
                NullPercent = c.NullPercent,
                UniqueCount = c.UniqueCount,
                UniquePercent = c.UniquePercent,
                Distribution = c.Distribution?.ToString(),
                Trend = c.Trend?.Direction.ToString(),
                Stats = new ToolColumnStats
                {
                    Min = c.Min,
                    Max = c.Max,
                    Mean = c.Mean,
                    Median = c.Median,
                    StdDev = c.StdDev,
                    Skewness = c.Skewness,
                    OutlierCount = c.OutlierCount > 0 ? c.OutlierCount : null,
                    TopValue = c.TopValues?.FirstOrDefault()?.Value,
                    TopValuePercent = c.TopValues?.FirstOrDefault()?.Percent,
                    ImbalanceRatio = c.ImbalanceRatio,
                    MinDate = c.MinDate?.ToString("yyyy-MM-dd"),
                    MaxDate = c.MaxDate?.ToString("yyyy-MM-dd"),
                    DateGapDays = c.DateGapDays,
                    AvgLength = c.AvgLength,
                    MaxLength = c.MaxLength
                }
            }).ToList(),
            Alerts = report.Profile.Alerts.Select(a => new ToolAlert
            {
                Severity = a.Severity.ToString(),
                Column = a.Column,
                Type = a.Type.ToString(),
                Message = a.Message
            }).ToList(),
            Insights = report.Profile.Insights.Take(10).Select(i => new ToolInsight
            {
                Title = i.Title,
                Description = i.Description,
                Score = i.Score,
                Source = i.Source.ToString(),
                RelatedColumns = i.RelatedColumns.Count > 0 ? i.RelatedColumns : null
            }).ToList(),
            Correlations = report.Profile.Correlations.Take(10).Select(c => new ToolCorrelation
            {
                Column1 = c.Column1,
                Column2 = c.Column2,
                Coefficient = c.Correlation,
                Strength = c.Strength
            }).ToList(),
            TargetAnalysis = report.Profile.Target != null ? new ToolTargetAnalysis
            {
                TargetColumn = report.Profile.Target.ColumnName,
                IsBinary = report.Profile.Target.IsBinary,
                ClassDistribution = report.Profile.Target.ClassDistribution.ToDictionary(
                    kv => kv.Key, 
                    kv => Math.Round(kv.Value * 100, 2)),
                TopDrivers = report.Profile.Target.FeatureEffects.Take(5).Select(e => new ToolFeatureDriver
                {
                    Feature = e.Feature,
                    Magnitude = Math.Round(e.Magnitude, 4),
                    Support = Math.Round(e.Support, 4),
                    Summary = e.Summary,
                    Metric = e.Metric
                }).ToList()
            } : null
        };

        var output = new ToolOutput
        {
            Success = true,
            Source = file,
            Profile = toolProfile,
            Metadata = new ToolMetadata
            {
                ProcessingSeconds = Math.Round(processingTime.TotalSeconds, 2),
                ColumnsAnalyzed = report.Profile.ColumnCount,
                RowsAnalyzed = report.Profile.RowCount,
                Model = null,
                UsedLlm = false,
                TargetColumn = targetColumn,
                ProfiledAt = startTime.ToString("o")
            }
        };

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    catch (Exception ex)
    {
        var errorOutput = new ToolOutput
        {
            Success = false,
            Source = file ?? "none",
            Error = ex.Message
        };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorOutput, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
});

rootCommand.Add(toolCmd);

rootCommand.SetHandler(async ctx =>
{
    var file = ctx.ParseResult.GetValueForOption(fileOption);
    var sheet = ctx.ParseResult.GetValueForOption(sheetOption);
    var model = ctx.ParseResult.GetValueForOption(modelOption);
    var noLlm = ctx.ParseResult.GetValueForOption(noLlmOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var output = ctx.ParseResult.GetValueForOption(outputOption);
    var query = ctx.ParseResult.GetValueForOption(queryOption);
    var onnx = ctx.ParseResult.GetValueForOption(onnxOption);
    var ingestDir = ctx.ParseResult.GetValueForOption(ingestDirOption);
    var ingestFiles = ctx.ParseResult.GetValueForOption(ingestFilesOption);
    var registryQuery = ctx.ParseResult.GetValueForOption(registryQueryOption);
    var vectorDb = ctx.ParseResult.GetValueForOption(vectorDbOption);
    var sessionId = ctx.ParseResult.GetValueForOption(sessionIdOption);
    var synthPath = ctx.ParseResult.GetValueForOption(synthPathOption);
    var synthRows = ctx.ParseResult.GetValueForOption(synthRowsOption);
    
    // Profile options
    var columns = ctx.ParseResult.GetValueForOption(columnsOption);
    var excludeColumns = ctx.ParseResult.GetValueForOption(excludeColumnsOption);
    var maxColumns = ctx.ParseResult.GetValueForOption(maxColumnsOption);
    var fastMode = ctx.ParseResult.GetValueForOption(fastModeOption);
    var skipCorrelations = ctx.ParseResult.GetValueForOption(skipCorrelationsOption);
    var ignoreErrors = ctx.ParseResult.GetValueForOption(ignoreErrorsOption);
    var targetColumn = ctx.ParseResult.GetValueForOption(targetOption);
    var markdownOutput = ctx.ParseResult.GetValueForOption(markdownOutputOption);
    var skipReport = ctx.ParseResult.GetValueForOption(noReportOption);
    var focusQuestions = ctx.ParseResult.GetValueForOption(focusQuestionOption);
    
    sessionId ??= Guid.NewGuid().ToString("N");
    
    var profileOptions = new ProfileOptions
    {
        Columns = columns?.Length > 0 ? columns.ToList() : settings.ProfileOptions.Columns,
        ExcludeColumns = excludeColumns?.Length > 0 ? excludeColumns.ToList() : settings.ProfileOptions.ExcludeColumns,
        MaxColumns = maxColumns ?? settings.ProfileOptions.MaxColumns,
        MaxCorrelationPairs = settings.ProfileOptions.MaxCorrelationPairs,
        FastMode = fastMode || settings.ProfileOptions.FastMode,
        SkipCorrelations = skipCorrelations || settings.ProfileOptions.SkipCorrelations,
        SampleSize = settings.ProfileOptions.SampleSize,
        IncludeDescriptions = settings.ProfileOptions.IncludeDescriptions,
        IgnoreErrors = ignoreErrors || settings.ProfileOptions.IgnoreErrors,
        TargetColumn = targetColumn ?? settings.ProfileOptions.TargetColumn
    };
    
    var reportOptions = new ReportOptions
    {
        GenerateMarkdown = settings.MarkdownReport.Enabled && !skipReport,
        UseLlm = settings.MarkdownReport.UseLlm,
        IncludeFocusQuestions = settings.MarkdownReport.IncludeFocusQuestions,
        FocusQuestions = settings.MarkdownReport.FocusQuestions?.ToList() ?? new()
    };
    
    if (focusQuestions is { Length: > 0 })
    {
        reportOptions.FocusQuestions = focusQuestions.ToList();
        reportOptions.IncludeFocusQuestions = true;
    }
    
    if (noLlm)
    {
        reportOptions.UseLlm = false;
    }

    var defaultReportDirectory = settings.MarkdownReport.OutputDirectory ?? Path.Combine(AppContext.BaseDirectory, "reports");
    var resolvedReportPath = markdownOutput;
    if (string.IsNullOrWhiteSpace(resolvedReportPath) && !string.IsNullOrWhiteSpace(output))
    {
        resolvedReportPath = output;
    }
    if (string.IsNullOrWhiteSpace(resolvedReportPath) && reportOptions.GenerateMarkdown)
    {
        var fileName = !string.IsNullOrWhiteSpace(file)
            ? Path.GetFileNameWithoutExtension(file)
            : $"report-{DateTime.UtcNow:yyyyMMddHHmmss}";
        resolvedReportPath = Path.Combine(defaultReportDirectory, $"{fileName}-report.md");
    }


    try
    {
        // Header
        AnsiConsole.Write(new FigletText("DataSummarizer").Color(Color.Cyan1));
        
        var supported = new[] { ".csv", ".xlsx", ".xls", ".parquet", ".json" };

        // Helper to validate a single file
        bool ValidateFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!File.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]Error: File not found: {path}[/]");
                return false;
            }
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!supported.Contains(ext))
            {
                AnsiConsole.MarkupLine($"[red]Error: Unsupported file type: {ext}[/]");
                AnsiConsole.MarkupLine($"[dim]Supported: {string.Join(", ", supported)}[/]");
                return false;
            }
            return true;
        }

        IEnumerable<string> ExpandPatterns(IEnumerable<string> patterns)
        {
            return CliHelpers.ExpandPatternsHelper(patterns, null, supported);
        }

        // Determine mode: ingestion, registry query, or single-file summarize/ask
        var ingestList = new List<string>();
        if (!string.IsNullOrWhiteSpace(ingestDir) && Directory.Exists(ingestDir))
        {
            ingestList.AddRange(ExpandPatterns([ingestDir]));
        }
        if (ingestFiles is { Length: > 0 })
        {
            ingestList.AddRange(ExpandPatterns(ingestFiles));
        }

        // If no ingest and no registry query, require a file
        if (!ingestList.Any() && string.IsNullOrEmpty(registryQuery))
        {
            if (!ValidateFile(file)) return;
        }

        using var summarizer = new DataSummarizerService(
            verbose: verbose,
            ollamaModel: noLlm ? null : model,
            ollamaUrl: "http://localhost:11434",
            onnxSentinelPath: onnx,
            vectorStorePath: vectorDb,
            sessionId: sessionId,
            profileOptions: profileOptions,
            reportOptions: reportOptions
        );

        // Ingest mode
        if (ingestList.Any())
        {
            AnsiConsole.MarkupLine($"[cyan]Ingesting {ingestList.Count} file(s) into registry...[/]");
            foreach (var path in ingestList)
            {
                AnsiConsole.MarkupLine($"[dim]- {Path.GetFileName(path)}[/]");
            }

            await summarizer.IngestAsync(ingestList, maxLlmInsights: 0); // no LLM during ingestion for speed
            AnsiConsole.MarkupLine("[green]Ingestion complete.[/]");
            return;
        }

        // Registry query mode (vector search over ingested data)
        if (!string.IsNullOrEmpty(registryQuery))
        {
            AnsiConsole.MarkupLine($"[yellow]Registry question:[/] {registryQuery}");
            AnsiConsole.MarkupLine($"[dim]Session:[/] {sessionId}");
            AnsiConsole.WriteLine();

            var answer = await summarizer.AskRegistryAsync(registryQuery, topK: 6);
            if (answer != null)
            {
                AnsiConsole.MarkupLine($"[green]Answer:[/] {answer.Description}");
                if (answer.RelatedColumns.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Context:[/] {string.Join(", ", answer.RelatedColumns)}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]No answer produced (registry empty or LLM unavailable).[/]");
            }
            return;
        }

        // Normal single-file modes below
        var ext = Path.GetExtension(file!).ToLowerInvariant();
        AnsiConsole.MarkupLine($"[cyan]File:[/] {Path.GetFileName(file)}");
        AnsiConsole.MarkupLine($"[cyan]Type:[/] {ext.TrimStart('.')}");
        if (!noLlm && !string.IsNullOrEmpty(model))
            AnsiConsole.MarkupLine($"[cyan]Model:[/] {model}");
        if (!string.IsNullOrWhiteSpace(onnx))
            AnsiConsole.MarkupLine($"[cyan]ONNX Sentinel:[/] {onnx}");
        AnsiConsole.WriteLine();

        // Query mode (single file)
        if (!string.IsNullOrEmpty(query))
        {
            AnsiConsole.MarkupLine($"[yellow]Question:[/] {query}");
            AnsiConsole.MarkupLine($"[dim]Session:[/] {sessionId}");
            AnsiConsole.WriteLine();

            var insight = await summarizer.AskAsync(file!, query, sheet);
            
            if (insight != null)
            {
                AnsiConsole.MarkupLine($"[green]Answer:[/] {insight.Description}");
                if (!string.IsNullOrEmpty(insight.Sql))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]SQL:[/]");
                    AnsiConsole.Write(new Panel(insight.Sql).BorderColor(Color.Grey));
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Could not generate answer[/]");
            }
            return;
        }

        // Full summarization
        var report = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Profiling data...", async ctx =>
            {
                ctx.Status("Running statistical analysis...");
                return await summarizer.SummarizeAsync(
                    file!, 
                    sheet, 
                    useLlm: !noLlm,
                    maxLlmInsights: 5
                );
            });

        // Persist markdown report if enabled
        if (reportOptions.GenerateMarkdown && !string.IsNullOrWhiteSpace(resolvedReportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedReportPath)!);
            await File.WriteAllTextAsync(resolvedReportPath, report.MarkdownReport);
            AnsiConsole.MarkupLine($"[green]Report saved to:[/] {resolvedReportPath}");
        }

        // Always show console summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Summary[/]").LeftJustified());
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(Markup.Escape(report.ExecutiveSummary));
        AnsiConsole.WriteLine();

        if (report.FocusFindings.Count > 0)
        {
            AnsiConsole.Write(new Rule("[cyan]Focus Findings[/]").LeftJustified());
            foreach (var kvp in report.FocusFindings)
            {
                AnsiConsole.MarkupLine($"[bold]? {kvp.Key}[/]");
                AnsiConsole.WriteLine(Markup.Escape(kvp.Value));
                AnsiConsole.WriteLine();
            }
        }

        // Column table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Column")
            .AddColumn("Type")
            .AddColumn("Nulls")
            .AddColumn("Unique")
            .AddColumn("Stats");

        foreach (var col in report.Profile.Columns)
        {
            var stats = col.InferredType switch
            {
                ColumnType.Numeric => $"μ={col.Mean:F1}, σ={col.StdDev:F1}, range={col.Min:F1}-{col.Max:F1}",
                ColumnType.Categorical when col.TopValues?.Count > 0 => $"top: {col.TopValues[0].Value}",
                ColumnType.DateTime => $"{col.MinDate:yyyy-MM-dd} → {col.MaxDate:yyyy-MM-dd}",
                _ => "-"
            };

            table.AddRow(
                Markup.Escape(col.Name),
                col.InferredType.ToString(),
                $"{col.NullPercent:F1}%",
                col.UniqueCount.ToString("N0"),
                stats);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (report.Profile.Alerts.Count > 0)
        {
            AnsiConsole.Write(new Rule("[yellow]Alerts[/]").LeftJustified());
            foreach (var alert in report.Profile.Alerts)
            {
                var color = alert.Severity switch
                {
                    AlertSeverity.Error => "red",
                    AlertSeverity.Warning => "yellow",
                    _ => "blue"
                };
                AnsiConsole.MarkupLine($"[{color}]- {Markup.Escape(alert.Column)}: {Markup.Escape(alert.Message)}[/]");
            }
            AnsiConsole.WriteLine();
        }

        if (report.Profile.Insights.Count > 0)
        {
            AnsiConsole.Write(new Rule("[green]Insights[/]").LeftJustified());
            foreach (var insight in report.Profile.Insights.OrderByDescending(i => i.Score).Take(5))
            {
                var scoreText = insight.Score > 0 ? $" (score {insight.Score:F2})" : string.Empty;
                AnsiConsole.MarkupLine($"[bold]{insight.Title}[/]{scoreText}");
                AnsiConsole.WriteLine(Markup.Escape(insight.Description));
                AnsiConsole.WriteLine();
            }
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex);
    }
});
 
 return await rootCommand.InvokeAsync(args);

