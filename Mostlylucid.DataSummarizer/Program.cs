using System.CommandLine;
using System.CommandLine.Parsing;
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

// Drag-and-drop support: if first arg is a file path (not a flag), inject -f
if (args.Length > 0 && !args[0].StartsWith("-") && File.Exists(args[0]))
{
    args = new[] { "-f", args[0] }.Concat(args.Skip(1)).ToArray();
}

// Options - using new System.CommandLine 2.0.1 API
var fileOption = new Option<string?>("--file", "-f") { Description = "Path to data file (CSV, Excel, Parquet, JSON)" };
var sheetOption = new Option<string?>("--sheet", "-s") { Description = "Sheet name for Excel files" };
var modelOption = new Option<string?>("--model", "-m") { Description = "Ollama model for LLM insights", DefaultValueFactory = _ => "qwen2.5-coder:7b" };
var noLlmOption = new Option<bool>("--no-llm") { Description = "Skip LLM insights (stats only)" };
var verboseOption = new Option<bool>("--verbose", "-v") { Description = "Verbose output" };
var outputOption = new Option<string?>("--output", "-o") { Description = "Output file path (default: console)" };
var queryOption = new Option<string?>("--query", "-q") { Description = "Ask a specific question about the data" };
var onnxOption = new Option<string?>("--onnx", "--onnx-sentinel") { Description = "Optional ONNX sentinel model path for column scoring" };
var ingestDirOption = new Option<string?>("--ingest-dir") { Description = "Ingest all supported files in a directory into the registry" };
var ingestFilesOption = new Option<string[]?>("--ingest-files") { Description = "Ingest a comma-separated list of files into the registry", AllowMultipleArgumentsPerToken = true };
var registryQueryOption = new Option<string?>("--registry-query") { Description = "Ask a question across all ingested data (vector search)" };
var vectorDbOption = new Option<string?>("--vector-db") { Description = "Path to persistent DuckDB vector store", DefaultValueFactory = _ => ".datasummarizer.vss.duckdb" };
var sessionIdOption = new Option<string?>("--session-id") { Description = "Conversation/session id for context memory (auto-generates if omitted)" };
var synthPathOption = new Option<string?>("--synthesize-to") { Description = "Write a synthetic CSV that matches the profiled shape" };
var synthRowsOption = new Option<int>("--synthesize-rows") { Description = "Rows to generate when synthesizing", DefaultValueFactory = _ => 1000 };
var columnsOption = new Option<string[]?>("--columns") { Description = "Specific columns to analyze (comma-separated)", AllowMultipleArgumentsPerToken = true };
var excludeColumnsOption = new Option<string[]?>("--exclude-columns") { Description = "Columns to exclude from analysis", AllowMultipleArgumentsPerToken = true };
var maxColumnsOption = new Option<int?>("--max-columns") { Description = "Maximum columns to analyze (0=unlimited). Selects most interesting for wide tables." };
var fastModeOption = new Option<bool>("--fast") { Description = "Fast mode: skip expensive pattern detection" };
var skipCorrelationsOption = new Option<bool>("--skip-correlations") { Description = "Skip correlation analysis (faster for wide tables)" };
var ignoreErrorsOption = new Option<bool>("--ignore-errors") { Description = "Ignore CSV parsing errors (malformed rows)" };
var targetOption = new Option<string?>("--target") { Description = "Target column for supervised analysis (e.g. churn flag)" };
var markdownOutputOption = new Option<string?>("--markdown-output") { Description = "Write markdown report to this path (overrides defaults)" };
var noReportOption = new Option<bool>("--no-report") { Description = "Skip markdown report generation" };
var focusQuestionOption = new Option<string[]?>("--focus-question") { Description = "Focus question(s) for the LLM-grounded report", AllowMultipleArgumentsPerToken = true };
var interactiveOption = new Option<bool>("--interactive", "-i") { Description = "Interactive conversation mode - ask multiple questions about your data" };
var outputProfileOption = new Option<string?>("--output-profile", "-p") { Description = "Output profile: Default, Tool, Brief, Detailed, Markdown" };

// Profile store options
var storeProfileOption = new Option<bool>("--store") { Description = "Store profile for drift tracking (auto-detect similar profiles)" };
var compareToOption = new Option<string?>("--compare-to") { Description = "Profile ID to compare against (for drift detection)" };
var autoDriftOption = new Option<bool>("--auto-drift") { Description = "Auto-detect baseline and show drift (default: true for tool mode)" };
var noStoreOption = new Option<bool>("--no-store") { Description = "Don't store profile or check for drift" };
var storePathOption = new Option<string?>("--store-path") { Description = "Custom profile store directory" };

// Synth command options
var synthProfileOption = new Option<string>("--profile") { Description = "Profile JSON produced by 'profile' command", Required = true };
var synthSourceOption = new Option<string>("--source") { Description = "Source file or glob", Required = true };
var synthTargetOption = new Option<string>("--target") { Description = "Target file or glob", Required = true };

// Export format options (for tool command)
var formatOption = new Option<string?>("--format") { Description = "Output format: json (default), markdown, html" };

// Constraint validation options
var constraintFileOption = new Option<string?>("--constraints") { Description = "Path to constraint suite JSON file" };
var generateConstraintsOption = new Option<bool>("--generate-constraints") { Description = "Auto-generate constraints from the profile" };
var strictValidationOption = new Option<bool>("--strict") { Description = "Fail if any constraint violations found" };

// Segment comparison options
var segmentAOption = new Option<string?>("--segment-a") { Description = "First profile ID or file path for segment comparison" };
var segmentBOption = new Option<string?>("--segment-b") { Description = "Second profile ID or file path for segment comparison" };
var segmentNameAOption = new Option<string?>("--name-a") { Description = "Display name for segment A" };
var segmentNameBOption = new Option<string?>("--name-b") { Description = "Display name for segment B" };

// Subcommands
var profileCmd = new Command("profile", "Profile one or more files and write profile JSON");
profileCmd.Options.Add(fileOption);
profileCmd.Options.Add(ingestFilesOption);
profileCmd.Options.Add(ingestDirOption);
profileCmd.Options.Add(outputOption);
profileCmd.Options.Add(verboseOption);
profileCmd.Options.Add(noLlmOption);
profileCmd.Options.Add(modelOption);
profileCmd.Options.Add(onnxOption);
profileCmd.Options.Add(vectorDbOption);
profileCmd.Options.Add(sessionIdOption);

var synthCmd = new Command("synth", "Synthesize data from a saved profile (JSON)");
synthCmd.Options.Add(synthProfileOption);
synthCmd.Options.Add(synthPathOption);
synthCmd.Options.Add(synthRowsOption);
synthCmd.Options.Add(verboseOption);

var validateCmd = new Command("validate", "Compare two datasets (or dataset vs synth) and report deltas, or validate against constraints");
validateCmd.Options.Add(synthSourceOption);
validateCmd.Options.Add(synthTargetOption);
validateCmd.Options.Add(outputOption);
validateCmd.Options.Add(verboseOption);
validateCmd.Options.Add(modelOption);
validateCmd.Options.Add(noLlmOption);
validateCmd.Options.Add(vectorDbOption);
validateCmd.Options.Add(sessionIdOption);
validateCmd.Options.Add(constraintFileOption);
validateCmd.Options.Add(generateConstraintsOption);
validateCmd.Options.Add(strictValidationOption);
validateCmd.Options.Add(formatOption);

// Segment comparison command
var segmentCmd = new Command("segment", "Compare two data segments or stored profiles");
segmentCmd.Options.Add(segmentAOption);
segmentCmd.Options.Add(segmentBOption);
segmentCmd.Options.Add(segmentNameAOption);
segmentCmd.Options.Add(segmentNameBOption);
segmentCmd.Options.Add(outputOption);
segmentCmd.Options.Add(formatOption);
segmentCmd.Options.Add(storePathOption);

var toolCmd = new Command("tool", "Profile data and output JSON for LLM tool integration");
toolCmd.Options.Add(fileOption);
toolCmd.Options.Add(sheetOption);
toolCmd.Options.Add(targetOption);
toolCmd.Options.Add(columnsOption);
toolCmd.Options.Add(excludeColumnsOption);
toolCmd.Options.Add(maxColumnsOption);
toolCmd.Options.Add(fastModeOption);
toolCmd.Options.Add(skipCorrelationsOption);
toolCmd.Options.Add(ignoreErrorsOption);

// Tool-specific options for fast/cached operation
var cacheOption = new Option<bool>("--cache") { Description = "Use cached profile if file unchanged (xxHash64 check)" };
var quickOption = new Option<bool>("--quick", "-q") { Description = "Quick mode: basic stats only, no patterns/correlations (fastest)" };
var compactOption = new Option<bool>("--compact") { Description = "Compact output: omit null fields and empty arrays" };
toolCmd.Options.Add(cacheOption);
toolCmd.Options.Add(storeProfileOption); // --store
toolCmd.Options.Add(compareToOption);    // --compare-to (defined above)
toolCmd.Options.Add(autoDriftOption);    // --auto-drift (defined above)
toolCmd.Options.Add(quickOption);
toolCmd.Options.Add(compactOption);
toolCmd.Options.Add(storePathOption);
toolCmd.Options.Add(formatOption);

// Store management commands
var storeCmd = new Command("store", "Manage the profile store (list, clear, prune stored profiles)");
var storeListCmd = new Command("list", "List all stored profiles");
var storeClearCmd = new Command("clear", "Clear all stored profiles");
var storePruneCmd = new Command("prune", "Remove old profiles, keeping N most recent per schema");
var storeStatsCmd = new Command("stats", "Show store statistics");
var storeDeleteOption = new Option<string?>("--id") { Description = "Profile ID to delete" };
var pruneKeepOption = new Option<int>("--keep", "-k") { Description = "Number of profiles to keep per schema", DefaultValueFactory = _ => 5 };

storePruneCmd.Options.Add(pruneKeepOption);
storeListCmd.Options.Add(storePathOption);
storeClearCmd.Options.Add(storePathOption);
storePruneCmd.Options.Add(storePathOption);
storeStatsCmd.Options.Add(storePathOption);
storeCmd.Subcommands.Add(storeListCmd);
storeCmd.Subcommands.Add(storeClearCmd);
storeCmd.Subcommands.Add(storePruneCmd);
storeCmd.Subcommands.Add(storeStatsCmd);
storeCmd.Options.Add(storePathOption);
storeCmd.Options.Add(storeDeleteOption);

var rootCommand = new RootCommand("Data summarization tool - profile CSV, Excel, Parquet files");
rootCommand.Options.Add(fileOption);
rootCommand.Options.Add(sheetOption);
rootCommand.Options.Add(modelOption);
rootCommand.Options.Add(noLlmOption);
rootCommand.Options.Add(verboseOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(queryOption);
rootCommand.Options.Add(onnxOption);
rootCommand.Options.Add(ingestDirOption);
rootCommand.Options.Add(ingestFilesOption);
rootCommand.Options.Add(registryQueryOption);
rootCommand.Options.Add(vectorDbOption);
rootCommand.Options.Add(sessionIdOption);
rootCommand.Options.Add(synthPathOption);
rootCommand.Options.Add(synthRowsOption);
rootCommand.Options.Add(columnsOption);
rootCommand.Options.Add(excludeColumnsOption);
rootCommand.Options.Add(maxColumnsOption);
rootCommand.Options.Add(fastModeOption);
rootCommand.Options.Add(skipCorrelationsOption);
rootCommand.Options.Add(ignoreErrorsOption);
rootCommand.Options.Add(targetOption);
rootCommand.Options.Add(markdownOutputOption);
rootCommand.Options.Add(noReportOption);
rootCommand.Options.Add(focusQuestionOption);
rootCommand.Options.Add(interactiveOption);
rootCommand.Options.Add(outputProfileOption);
rootCommand.Subcommands.Add(profileCmd);
rootCommand.Subcommands.Add(synthCmd);
rootCommand.Subcommands.Add(validateCmd);
rootCommand.Subcommands.Add(toolCmd);
rootCommand.Subcommands.Add(storeCmd);
rootCommand.Subcommands.Add(segmentCmd);

profileCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(fileOption);
    var ingestFiles = parseResult.GetValue(ingestFilesOption) ?? Array.Empty<string>();
    var ingestDir = parseResult.GetValue(ingestDirOption);
    var output = parseResult.GetValue(outputOption);
    var verbose = parseResult.GetValue(verboseOption);
    var noLlm = parseResult.GetValue(noLlmOption);
    var model = parseResult.GetValue(modelOption);
    var onnx = parseResult.GetValue(onnxOption);
    var vectorDb = parseResult.GetValue(vectorDbOption);
    var sessionId = parseResult.GetValue(sessionIdOption);

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

synthCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var profilePath = parseResult.GetValue(synthProfileOption);
    var synthOut = parseResult.GetValue(synthPathOption);
    var rows = parseResult.GetValue(synthRowsOption);
    var verbose = parseResult.GetValue(verboseOption);
    
    var profiles = ProfileIo.LoadProfiles(profilePath ?? "profile.json");
    if (profiles.Count == 0) { Console.WriteLine("No profiles found in JSON"); return; }
    var outPath = synthOut ?? "synthetic.csv";
    DataSynthesizer.GenerateCsv(profiles[0], rows, outPath);
    Console.WriteLine($"Synthetic data written to {outPath}");
    await Task.CompletedTask;
});

validateCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var source = parseResult.GetValue(synthSourceOption)!;
    var target = parseResult.GetValue(synthTargetOption)!;
    var output = parseResult.GetValue(outputOption);
    var verbose = parseResult.GetValue(verboseOption);
    var model = parseResult.GetValue(modelOption);
    var noLlm = parseResult.GetValue(noLlmOption);
    var vectorDb = parseResult.GetValue(vectorDbOption);
    var sessionId = parseResult.GetValue(sessionIdOption);
    var constraintFile = parseResult.GetValue(constraintFileOption);
    var generateConstraints = parseResult.GetValue(generateConstraintsOption);
    var strict = parseResult.GetValue(strictValidationOption);
    var format = parseResult.GetValue(formatOption)?.ToLowerInvariant() ?? "json";

    var sid = sessionId ?? Guid.NewGuid().ToString("N");
    var srcFiles = CliHelpers.ExpandPatternsHelper(new[] { source }, null).ToList();
    var tgtFiles = CliHelpers.ExpandPatternsHelper(new[] { target }, null).ToList();
    if (srcFiles.Count == 0 || tgtFiles.Count == 0) { Console.WriteLine("Missing source/target files"); return; }

    using var svc = new DataSummarizerService(verbose, noLlm ? null : model, "http://localhost:11434", null, vectorDb, sid);
    var srcReport = await svc.SummarizeAsync(srcFiles[0], useLlm: false);
    var tgtReport = await svc.SummarizeAsync(tgtFiles[0], useLlm: false);

    // Constraint validation mode
    if (!string.IsNullOrEmpty(constraintFile) || generateConstraints)
    {
        var validator = new ConstraintValidator(verbose);
        ConstraintSuite suite;
        
        if (!string.IsNullOrEmpty(constraintFile) && File.Exists(constraintFile))
        {
            var suiteJson = await File.ReadAllTextAsync(constraintFile);
            suite = System.Text.Json.JsonSerializer.Deserialize<ConstraintSuite>(suiteJson) 
                ?? throw new InvalidOperationException("Failed to parse constraint file");
        }
        else
        {
            // Auto-generate constraints from source profile
            suite = validator.GenerateFromProfile(srcReport.Profile);
            if (generateConstraints && string.IsNullOrEmpty(constraintFile))
            {
                // Output the generated constraints
                var generatedJson = System.Text.Json.JsonSerializer.Serialize(suite, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                var constraintOutPath = output ?? Path.ChangeExtension(srcFiles[0], ".constraints.json");
                await File.WriteAllTextAsync(constraintOutPath, generatedJson);
                AnsiConsole.MarkupLine($"[green]Generated {suite.Constraints.Count} constraints to:[/] {constraintOutPath}");
            }
        }

        // Validate target against constraints
        var validationResult = validator.Validate(tgtReport.Profile, suite);
        
        // Output based on format
        var outputContent = format switch
        {
            "markdown" => FormatConstraintValidationMarkdown(validationResult),
            "html" => FormatConstraintValidationHtml(validationResult),
            _ => System.Text.Json.JsonSerializer.Serialize(validationResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
        };
        
        if (!string.IsNullOrEmpty(output))
        {
            await File.WriteAllTextAsync(output, outputContent);
            AnsiConsole.MarkupLine($"[green]Validation report saved to:[/] {output}");
        }
        
        // Console output
        if (format == "json")
        {
            Console.WriteLine(outputContent);
        }
        else
        {
            AnsiConsole.Write(new Rule($"[cyan]Constraint Validation: {validationResult.SuiteName}[/]").LeftJustified());
            AnsiConsole.MarkupLine($"[bold]Pass Rate:[/] {validationResult.PassRate:P1} ({validationResult.PassedConstraints}/{validationResult.TotalConstraints})");
            
            if (validationResult.FailedConstraints > 0)
            {
                AnsiConsole.MarkupLine("\n[yellow]Failed Constraints:[/]");
                foreach (var failure in validationResult.GetFailures().Take(10))
                {
                    AnsiConsole.MarkupLine($"  [red]X[/] {Markup.Escape(failure.Constraint.Description)}");
                    if (failure.ActualValue != null)
                        AnsiConsole.MarkupLine($"    [dim]Actual: {failure.ActualValue}[/]");
                    if (!string.IsNullOrEmpty(failure.Details))
                        AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(failure.Details)}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]All constraints passed![/]");
            }
        }
        
        // Exit with error code if strict mode and failures exist
        if (strict && validationResult.FailedConstraints > 0)
        {
            Environment.Exit(1);
        }
        return;
    }

    // Standard drift comparison mode
    var validation = ValidationService.Compare(srcReport.Profile, tgtReport.Profile);
    
    // Also compute detailed drift with ProfileComparator
    var comparator = new ProfileComparator();
    var detailedDrift = comparator.Compare(srcReport.Profile, tgtReport.Profile);
    
    // Compute anomaly score
    var anomalyScore = AnomalyScorer.ComputeAnomalyScore(tgtReport.Profile);
    
    // If significant drift detected, suggest updated constraints
    if (detailedDrift.HasSignificantDrift && detailedDrift.OverallDriftScore > 0.3)
    {
        var validator = new ConstraintValidator(verbose);
        var suggestedConstraints = validator.GenerateFromProfile(tgtReport.Profile);
        
        var suggestedPath = output != null
            ? Path.Combine(Path.GetDirectoryName(output) ?? ".", "constraints.suggested.json")
            : "constraints.suggested.json";
            
        var suggestedJson = System.Text.Json.JsonSerializer.Serialize(suggestedConstraints, 
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(suggestedPath, suggestedJson);
        
        AnsiConsole.MarkupLine($"[yellow]⚠ Significant drift detected (score: {detailedDrift.OverallDriftScore:F2})[/]");
        AnsiConsole.MarkupLine($"[dim]Suggested constraints saved to: {suggestedPath}[/]");
        AnsiConsole.MarkupLine($"[dim]Review before applying in CI/CD pipeline.[/]");
    }
    
    var combinedResult = new
    {
        validation.Source,
        validation.Target,
        validation.DriftScore,
        AnomalyScore = anomalyScore,
        DetailedDrift = new
        {
            detailedDrift.Summary,
            detailedDrift.HasSignificantDrift,
            detailedDrift.OverallDriftScore,
            detailedDrift.RowCountChange,
            detailedDrift.SchemaChanges,
            detailedDrift.Recommendations
        },
        ColumnDeltas = validation.Columns
    };
    
    var outputContent2 = format switch
    {
        "markdown" => FormatValidationMarkdown(combinedResult, detailedDrift),
        "html" => FormatValidationHtml(combinedResult, detailedDrift),
        _ => System.Text.Json.JsonSerializer.Serialize(combinedResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
    };
    
    if (!string.IsNullOrEmpty(output))
    {
        await File.WriteAllTextAsync(output, outputContent2);
        AnsiConsole.MarkupLine($"[green]Validation report saved to:[/] {output}");
    }
    
    if (format == "json")
    {
        Console.WriteLine(outputContent2);
    }
    else
    {
        // Pretty console output
        AnsiConsole.Write(new Rule("[cyan]Data Drift Validation[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[bold]Source:[/] {Path.GetFileName(source)}");
        AnsiConsole.MarkupLine($"[bold]Target:[/] {Path.GetFileName(target)}");
        AnsiConsole.MarkupLine($"[bold]Drift Score:[/] {validation.DriftScore:F3}");
        AnsiConsole.MarkupLine($"[bold]Anomaly Score:[/] {anomalyScore.OverallScore:F3} ({anomalyScore.Interpretation})");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]{detailedDrift.Summary}[/]");
        
        if (detailedDrift.Recommendations.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Recommendations:[/]");
            foreach (var rec in detailedDrift.Recommendations.Take(5))
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(rec)}");
            }
        }
    }
});

toolCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(fileOption);
    var sheet = parseResult.GetValue(sheetOption);
    var targetColumn = parseResult.GetValue(targetOption);
    var columns = parseResult.GetValue(columnsOption);
    var excludeColumns = parseResult.GetValue(excludeColumnsOption);
    var maxColumns = parseResult.GetValue(maxColumnsOption);
    var fastMode = parseResult.GetValue(fastModeOption);
    var skipCorrelations = parseResult.GetValue(skipCorrelationsOption);
    var ignoreErrors = parseResult.GetValue(ignoreErrorsOption);
    
    // Tool-specific options
    var useCache = parseResult.GetValue(cacheOption);
    var storeResult = parseResult.GetValue(storeProfileOption);
    var compareToId = parseResult.GetValue(compareToOption);
    var autoDrift = parseResult.GetValue(autoDriftOption);
    var quickMode = parseResult.GetValue(quickOption);
    var compact = parseResult.GetValue(compactOption);
    var storePath = parseResult.GetValue(storePathOption);

    var startTime = DateTime.UtcNow;
    var jsonOptions = new System.Text.Json.JsonSerializerOptions 
    { 
        WriteIndented = !compact,
        DefaultIgnoreCondition = compact 
            ? System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull 
            : System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };
    
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
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorOutput, jsonOptions));
            return;
        }

        var store = new ProfileStore(storePath);
        DataProfile? profile = null;
        StoredProfileInfo? cachedInfo = null;
        bool usedCache = false;
        string? contentHash = null;
        
        // Fast path: check cache first (uses xxHash64 - very fast even for large files)
        if (useCache)
        {
            cachedInfo = store.QuickFindExisting(file);
            if (cachedInfo != null)
            {
                profile = store.LoadProfile(cachedInfo.Id);
                if (profile != null)
                {
                    usedCache = true;
                    contentHash = cachedInfo.ContentHash;
                }
            }
        }
        
        // Profile if not cached
        if (profile == null)
        {
            // Quick mode: minimal stats, no patterns/correlations
            var profileOptions = new ProfileOptions
            {
                Columns = columns?.Length > 0 ? columns.ToList() : null,
                ExcludeColumns = excludeColumns?.Length > 0 ? excludeColumns.ToList() : null,
                MaxColumns = quickMode ? 100 : (maxColumns ?? 50),
                FastMode = quickMode || fastMode,
                SkipCorrelations = quickMode || skipCorrelations,
                IgnoreErrors = ignoreErrors,
                TargetColumn = targetColumn
            };

            using var svc = new DataSummarizerService(
                verbose: false,
                ollamaModel: null,
                ollamaUrl: "http://localhost:11434",
                onnxSentinelPath: null,
                vectorStorePath: null,
                sessionId: null,
                profileOptions: profileOptions
            );

            var report = await svc.SummarizeAsync(file, sheet, useLlm: false);
            profile = report.Profile;
        }
        
        var processingTime = DateTime.UtcNow - startTime;
        
        // Store if requested
        StoredProfileInfo? storedInfo = null;
        if (storeResult && !usedCache)
        {
            storedInfo = store.Store(profile, contentHash);
        }
        
        // Drift detection
        ToolDriftSummary? drift = null;
        if (!string.IsNullOrEmpty(compareToId))
        {
            // Manual comparison to specific profile
            var baselineProfile = store.LoadProfile(compareToId);
            var baselineInfo = store.ListAll().FirstOrDefault(p => p.Id == compareToId);
            if (baselineProfile != null && baselineInfo != null)
            {
                drift = ComputeDrift(profile, baselineProfile, baselineInfo);
            }
        }
        else if (autoDrift)
        {
            // Auto-detect baseline (oldest profile with same schema)
            var baseline = store.LoadBaseline(profile);
            if (baseline != null && baseline.SourcePath != profile.SourcePath)
            {
                var schemaHash = ProfileStore.ComputeSchemaHash(profile);
                var baselineInfo = store.GetHistory(schemaHash).LastOrDefault(); // Oldest
                if (baselineInfo != null)
                {
                    drift = ComputeDrift(profile, baseline, baselineInfo);
                }
            }
        }
        
        // Build output
        var toolProfile = BuildToolProfile(profile, quickMode);
        
        var output = new ToolOutput
        {
            Success = true,
            Source = file,
            Profile = toolProfile,
            Metadata = new ToolMetadata
            {
                ProcessingSeconds = Math.Round(processingTime.TotalSeconds, 3),
                ColumnsAnalyzed = profile.ColumnCount,
                RowsAnalyzed = profile.RowCount,
                Model = null,
                UsedLlm = false,
                TargetColumn = targetColumn,
                ProfiledAt = startTime.ToString("o"),
                ProfileId = storedInfo?.Id ?? cachedInfo?.Id,
                SchemaHash = ProfileStore.ComputeSchemaHash(profile),
                ContentHash = contentHash ?? (usedCache ? cachedInfo?.ContentHash : ProfileStore.ComputeFileHash(file)),
                Drift = drift
            }
        };

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(output, jsonOptions));
    }
    catch (Exception ex)
    {
        var errorOutput = new ToolOutput
        {
            Success = false,
            Source = file ?? "none",
            Error = ex.Message
        };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorOutput, jsonOptions));
    }
});

// Store command handlers
storeListCmd.SetAction((parseResult, cancellationToken) =>
{
    var storePath = parseResult.GetValue(storePathOption);
    var store = new ProfileStore(storePath);
    var profiles = store.ListAll(100);
    
    if (profiles.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No stored profiles found.[/]");
        return Task.CompletedTask;
    }
    
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("ID")
        .AddColumn("File")
        .AddColumn("Rows")
        .AddColumn("Cols")
        .AddColumn("Schema")
        .AddColumn("Stored");
    
    foreach (var p in profiles)
    {
        table.AddRow(
            p.Id,
            Markup.Escape(p.FileName),
            p.RowCount.ToString("N0"),
            p.ColumnCount.ToString(),
            p.SchemaHash[..8],
            p.StoredAt.ToString("yyyy-MM-dd HH:mm"));
    }
    
    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"[dim]Total: {profiles.Count} profile(s)[/]");
    return Task.CompletedTask;
});

storeClearCmd.SetAction((parseResult, cancellationToken) =>
{
    var storePath = parseResult.GetValue(storePathOption);
    
    if (!AnsiConsole.Confirm("[red]Clear ALL stored profiles?[/]", defaultValue: false))
    {
        AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
        return Task.CompletedTask;
    }
    
    var store = new ProfileStore(storePath);
    var count = store.ClearAll();
    AnsiConsole.MarkupLine($"[green]Cleared {count} profile(s).[/]");
    return Task.CompletedTask;
});

storePruneCmd.SetAction((parseResult, cancellationToken) =>
{
    var storePath = parseResult.GetValue(storePathOption);
    var keep = parseResult.GetValue(pruneKeepOption);
    
    var store = new ProfileStore(storePath);
    var pruned = store.Prune(keep);
    AnsiConsole.MarkupLine($"[green]Pruned {pruned} old profile(s), keeping {keep} most recent per schema.[/]");
    return Task.CompletedTask;
});

storeStatsCmd.SetAction((parseResult, cancellationToken) =>
{
    var storePath = parseResult.GetValue(storePathOption);
    var store = new ProfileStore(storePath);
    var stats = store.GetStats();
    
    AnsiConsole.Write(new Rule("[cyan]Profile Store Statistics[/]").LeftJustified());
    AnsiConsole.MarkupLine($"[bold]Store path:[/] {Markup.Escape(stats.StorePath)}");
    AnsiConsole.MarkupLine($"[bold]Total profiles:[/] {stats.TotalProfiles}");
    AnsiConsole.MarkupLine($"[bold]Total size:[/] {stats.TotalSizeFormatted}");
    AnsiConsole.MarkupLine($"[bold]Unique schemas:[/] {stats.UniqueSchemas}");
    AnsiConsole.MarkupLine($"[bold]Segment groups:[/] {stats.SegmentGroups}");
    if (stats.OldestProfile.HasValue)
        AnsiConsole.MarkupLine($"[bold]Oldest:[/] {stats.OldestProfile:yyyy-MM-dd HH:mm}");
    if (stats.NewestProfile.HasValue)
        AnsiConsole.MarkupLine($"[bold]Newest:[/] {stats.NewestProfile:yyyy-MM-dd HH:mm}");
    return Task.CompletedTask;
});

storeCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var storePath = parseResult.GetValue(storePathOption);
    var deleteId = parseResult.GetValue(storeDeleteOption);
    
    if (!string.IsNullOrEmpty(deleteId))
    {
        var store = new ProfileStore(storePath);
        if (store.Delete(deleteId))
        {
            AnsiConsole.MarkupLine($"[green]Deleted profile {deleteId}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Profile {deleteId} not found[/]");
        }
        return;
    }
    
    // Interactive menu mode
    await ShowProfileManagementMenu(storePath);
});

// Segment comparison command handler
segmentCmd.SetAction(async (parseResult, cancellationToken) =>
{
    var segmentA = parseResult.GetValue(segmentAOption);
    var segmentB = parseResult.GetValue(segmentBOption);
    var nameA = parseResult.GetValue(segmentNameAOption);
    var nameB = parseResult.GetValue(segmentNameBOption);
    var output = parseResult.GetValue(outputOption);
    var format = parseResult.GetValue(formatOption)?.ToLowerInvariant() ?? "json";
    var storePath = parseResult.GetValue(storePathOption);

    if (string.IsNullOrEmpty(segmentA) || string.IsNullOrEmpty(segmentB))
    {
        AnsiConsole.MarkupLine("[red]Both --segment-a and --segment-b are required[/]");
        AnsiConsole.MarkupLine("[dim]Usage: datasummarizer segment --segment-a <path-or-id> --segment-b <path-or-id>[/]");
        return;
    }

    var store = new ProfileStore(storePath);
    
    // Load profiles - can be file paths or profile IDs
    DataProfile? profileA = null;
    DataProfile? profileB = null;

    // Try to load segment A
    if (File.Exists(segmentA))
    {
        using var svc = new DataSummarizerService(verbose: false, ollamaModel: null);
        var report = await svc.SummarizeAsync(segmentA, useLlm: false);
        profileA = report.Profile;
        nameA ??= Path.GetFileName(segmentA);
    }
    else
    {
        profileA = store.LoadProfile(segmentA);
        var info = store.ListAll().FirstOrDefault(p => p.Id == segmentA);
        nameA ??= info?.FileName ?? segmentA;
    }

    // Try to load segment B
    if (File.Exists(segmentB))
    {
        using var svc = new DataSummarizerService(verbose: false, ollamaModel: null);
        var report = await svc.SummarizeAsync(segmentB, useLlm: false);
        profileB = report.Profile;
        nameB ??= Path.GetFileName(segmentB);
    }
    else
    {
        profileB = store.LoadProfile(segmentB);
        var info = store.ListAll().FirstOrDefault(p => p.Id == segmentB);
        nameB ??= info?.FileName ?? segmentB;
    }

    if (profileA == null || profileB == null)
    {
        AnsiConsole.MarkupLine("[red]Could not load one or both profiles[/]");
        if (profileA == null) AnsiConsole.MarkupLine($"[dim]Segment A not found: {segmentA}[/]");
        if (profileB == null) AnsiConsole.MarkupLine($"[dim]Segment B not found: {segmentB}[/]");
        return;
    }

    // Perform comparison
    var segmentProfiler = new SegmentProfiler();
    var comparison = segmentProfiler.CompareSegments(profileA, profileB, nameA, nameB);
    
    // Also compute anomaly scores for context
    var anomalyA = AnomalyScorer.ComputeAnomalyScore(profileA);
    var anomalyB = AnomalyScorer.ComputeAnomalyScore(profileB);

    var result = new
    {
        comparison.SegmentAName,
        comparison.SegmentBName,
        comparison.SegmentARowCount,
        comparison.SegmentBRowCount,
        comparison.Similarity,
        comparison.OverallDistance,
        AnomalyScoreA = anomalyA.OverallScore,
        AnomalyScoreB = anomalyB.OverallScore,
        comparison.Insights,
        comparison.ColumnComparisons,
        comparison.ComparedAt
    };

    // Format output
    var outputContent = format switch
    {
        "markdown" => FormatSegmentComparisonMarkdown(comparison, anomalyA, anomalyB),
        "html" => FormatSegmentComparisonHtml(comparison, anomalyA, anomalyB),
        _ => System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
    };

    if (!string.IsNullOrEmpty(output))
    {
        await File.WriteAllTextAsync(output, outputContent);
        AnsiConsole.MarkupLine($"[green]Segment comparison saved to:[/] {output}");
    }

    if (format == "json")
    {
        Console.WriteLine(outputContent);
    }
    else
    {
        // Pretty console output
        AnsiConsole.Write(new Rule("[cyan]Segment Comparison[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[bold]Segment A:[/] {Markup.Escape(comparison.SegmentAName)} ({comparison.SegmentARowCount:N0} rows)");
        AnsiConsole.MarkupLine($"[bold]Segment B:[/] {Markup.Escape(comparison.SegmentBName)} ({comparison.SegmentBRowCount:N0} rows)");
        AnsiConsole.WriteLine();
        
        var similarityColor = comparison.Similarity >= 0.8 ? "green" : comparison.Similarity >= 0.5 ? "yellow" : "red";
        AnsiConsole.MarkupLine($"[bold]Similarity:[/] [{similarityColor}]{comparison.Similarity:P1}[/]");
        AnsiConsole.MarkupLine($"[bold]Anomaly Scores:[/] A={anomalyA.OverallScore:F3} ({anomalyA.Interpretation}), B={anomalyB.OverallScore:F3} ({anomalyB.Interpretation})");
        AnsiConsole.WriteLine();

        // Insights
        AnsiConsole.MarkupLine("[bold]Insights:[/]");
        foreach (var insight in comparison.Insights)
        {
            AnsiConsole.MarkupLine($"  - {Markup.Escape(insight)}");
        }
        AnsiConsole.WriteLine();

        // Top differing columns
        var topDiffs = comparison.ColumnComparisons.Take(5).ToList();
        if (topDiffs.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Top Differences:[/]");
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Column");
            table.AddColumn("Type");
            table.AddColumn("Distance");
            table.AddColumn("A");
            table.AddColumn("B");
            table.AddColumn("Delta");

            foreach (var col in topDiffs)
            {
                var valueA = col.ColumnType == ColumnType.Numeric ? col.MeanA?.ToString("F2") ?? "-" : col.ModeA ?? "-";
                var valueB = col.ColumnType == ColumnType.Numeric ? col.MeanB?.ToString("F2") ?? "-" : col.ModeB ?? "-";
                var delta = col.MeanDelta?.ToString("+0.0;-0.0") ?? (col.NullRateDelta != 0 ? $"{col.NullRateDelta * 100:+0.0;-0.0}pp nulls" : "-");
                
                table.AddRow(
                    col.ColumnName,
                    col.ColumnType.ToString(),
                    $"{col.Distance:F3}",
                    valueA,
                    valueB,
                    delta
                );
            }
            AnsiConsole.Write(table);
        }
    }
});

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var file = parseResult.GetValue(fileOption);
    var sheet = parseResult.GetValue(sheetOption);
    var model = parseResult.GetValue(modelOption);
    var noLlm = parseResult.GetValue(noLlmOption);
    var verbose = parseResult.GetValue(verboseOption);
    var output = parseResult.GetValue(outputOption);
    var query = parseResult.GetValue(queryOption);
    var onnx = parseResult.GetValue(onnxOption);
    var ingestDir = parseResult.GetValue(ingestDirOption);
    var ingestFiles = parseResult.GetValue(ingestFilesOption);
    var registryQuery = parseResult.GetValue(registryQueryOption);
    var vectorDb = parseResult.GetValue(vectorDbOption);
    var sessionId = parseResult.GetValue(sessionIdOption);
    var synthPath = parseResult.GetValue(synthPathOption);
    var synthRows = parseResult.GetValue(synthRowsOption);
    var interactive = parseResult.GetValue(interactiveOption);
    
    // Profile options
    var columns = parseResult.GetValue(columnsOption);
    var excludeColumns = parseResult.GetValue(excludeColumnsOption);
    var maxColumns = parseResult.GetValue(maxColumnsOption);
    var fastMode = parseResult.GetValue(fastModeOption);
    var skipCorrelations = parseResult.GetValue(skipCorrelationsOption);
    var ignoreErrors = parseResult.GetValue(ignoreErrorsOption);
    var targetColumn = parseResult.GetValue(targetOption);
    var markdownOutput = parseResult.GetValue(markdownOutputOption);
    var skipReport = parseResult.GetValue(noReportOption);
    var focusQuestions = parseResult.GetValue(focusQuestionOption);
    var outputProfileName = parseResult.GetValue(outputProfileOption);
    
    // Resolve output profile
    var activeProfile = ResolveOutputProfile(settings, outputProfileName);
    
    sessionId ??= Guid.NewGuid().ToString("N");
    
    // Double-click mode: no file specified, prompt for one
    if (string.IsNullOrWhiteSpace(file) && 
        string.IsNullOrWhiteSpace(ingestDir) && 
        (ingestFiles == null || ingestFiles.Length == 0) &&
        string.IsNullOrWhiteSpace(registryQuery))
    {
        // Check if running in non-interactive mode (CI, piped, etc.)
        if (Console.IsInputRedirected || Console.IsOutputRedirected || !Environment.UserInteractive)
        {
            Console.WriteLine("Usage: datasummarizer [options] <file>");
            Console.WriteLine("Try 'datasummarizer --help' for more information.");
            return;
        }
        
        ShowBanner();
        AnsiConsole.MarkupLine("[cyan]Welcome to DataSummarizer![/]");
        AnsiConsole.MarkupLine("[dim]DuckDB-powered data profiling - analyze CSV, Excel, Parquet, JSON files[/]\n");
        
        file = AnsiConsole.Ask<string>("[green]Enter path to data file:[/] ");
        
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            AnsiConsole.MarkupLine("[red]File not found. Exiting.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }
        
        // In double-click mode, default to interactive
        interactive = AnsiConsole.Confirm("[yellow]Enter interactive mode?[/]", defaultValue: true);
    }
    
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
                AnsiConsole.MarkupLine($"[green]Answer:[/] {Markup.Escape(insight.Description)}");
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

        // Full summarization with status updates
        var report = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Profiling data...", async ctx =>
            {
                // Wire up status callback to update the spinner text
                profileOptions.OnStatusUpdate = status => ctx.Status(status);
                
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

        // Console output - all sections configurable
        var consoleSettings = settings.ConsoleOutput;
        
        // Summary section
        if (consoleSettings.ShowSummary)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[cyan]Summary[/]").LeftJustified());
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(Markup.Escape(report.ExecutiveSummary));
            AnsiConsole.WriteLine();
        }

        // Focus findings section (off by default)
        if (consoleSettings.ShowFocusFindings && report.FocusFindings.Count > 0)
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
        if (consoleSettings.ShowColumnTable)
        {
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
        }

        // Alerts section
        if (consoleSettings.ShowAlerts && report.Profile.Alerts.Count > 0)
        {
            AnsiConsole.Write(new Rule("[yellow]Alerts[/]").LeftJustified());
            foreach (var alert in report.Profile.Alerts.Take(consoleSettings.MaxAlerts))
            {
                var color = alert.Severity switch
                {
                    AlertSeverity.Error => "red",
                    AlertSeverity.Warning => "yellow",
                    _ => "blue"
                };
                AnsiConsole.MarkupLine($"[{color}]- {Markup.Escape(alert.Column)}: {Markup.Escape(alert.Message)}[/]");
            }
            if (report.Profile.Alerts.Count > consoleSettings.MaxAlerts)
            {
                AnsiConsole.MarkupLine($"[dim]... and {report.Profile.Alerts.Count - consoleSettings.MaxAlerts} more alerts[/]");
            }
            AnsiConsole.WriteLine();
        }

        // Insights section
        if (consoleSettings.ShowInsights && report.Profile.Insights.Count > 0)
        {
            AnsiConsole.Write(new Rule("[green]Insights[/]").LeftJustified());
            foreach (var insight in report.Profile.Insights.OrderByDescending(i => i.Score).Take(consoleSettings.MaxInsights))
            {
                var scoreText = insight.Score > 0 ? $" (score {insight.Score:F2})" : string.Empty;
                AnsiConsole.MarkupLine($"[bold]{insight.Title}[/]{scoreText}");
                AnsiConsole.WriteLine(Markup.Escape(insight.Description));
                AnsiConsole.WriteLine();
            }
        }
        
        // Interactive mode - continue asking questions
        if (interactive && !noLlm)
        {
            await RunInteractiveMode(summarizer, report, file!, sheet, verbose, sessionId);
        }
        else if (interactive && noLlm)
        {
            AnsiConsole.MarkupLine("[yellow]Interactive mode requires LLM. Run without --no-llm flag.[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.WriteException(ex);
    }
});
 
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

// Helper function to show the ASCII banner
static void ShowBanner()
{
    AnsiConsole.Write(new FigletText("DataSumma").Color(Color.Cyan1));
    AnsiConsole.Write(new FigletText("    rizer").Color(Color.Cyan1));
}

// Interactive conversation mode
static async Task RunInteractiveMode(
    DataSummarizerService summarizer, 
    DataSummaryReport report,
    string file,
    string? sheet,
    bool verbose,
    string sessionId)
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[green]Interactive Mode[/]").LeftJustified());
    AnsiConsole.MarkupLine("[dim]Ask questions about your data. Type 'exit' or 'quit' to leave.[/]");
    AnsiConsole.MarkupLine($"[dim]Session: {sessionId}[/]\n");
    
    while (true)
    {
        var question = AnsiConsole.Ask<string>("[cyan]>[/] ");
        
        if (string.IsNullOrWhiteSpace(question))
            continue;
            
        var lower = question.ToLowerInvariant().Trim();
        if (lower is "exit" or "quit" or "q" or "bye")
        {
            AnsiConsole.MarkupLine("[dim]Goodbye![/]");
            break;
        }
        
        if (lower is "help" or "?")
        {
            AnsiConsole.MarkupLine("[dim]Example questions:[/]");
            AnsiConsole.MarkupLine("  [cyan]tell me about this data[/]");
            AnsiConsole.MarkupLine("  [cyan]what columns have missing values?[/]");
            AnsiConsole.MarkupLine("  [cyan]are there any outliers?[/]");
            AnsiConsole.MarkupLine("  [cyan]what patterns do you see?[/]");
            AnsiConsole.MarkupLine("  [cyan]what should I fix before modeling?[/]");
            continue;
        }
        
        try
        {
            var answer = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Thinking...", async ctx =>
                {
                    return await summarizer.AskAsync(file, question, sheet);
                });
            
            if (answer != null)
            {
                AnsiConsole.MarkupLine($"\n[green]Answer:[/] {Markup.Escape(answer.Description)}\n");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Could not generate an answer for that question.[/]\n");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
                AnsiConsole.WriteException(ex);
            else
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]\n");
        }
    }
}

// Helper to resolve output profile from settings or CLI override
static OutputProfileConfig ResolveOutputProfile(DataSummarizerSettings settings, string? profileName)
{
    if (string.IsNullOrEmpty(profileName))
        return settings.GetActiveProfile();
    
    // Try to find in configured profiles
    if (settings.OutputProfiles.TryGetValue(profileName, out var profile))
        return profile;
    
    // Try built-in profiles by name (case-insensitive)
    return profileName.ToLowerInvariant() switch
    {
        "tool" => OutputProfileConfig.Tool,
        "brief" => OutputProfileConfig.Brief,
        "detailed" => OutputProfileConfig.Detailed,
        "markdown" => OutputProfileConfig.MarkdownFocus,
        _ => settings.GetActiveProfile()
    };
}

// Build ToolProfile from DataProfile (with optional quick mode for minimal output)
static ToolProfile BuildToolProfile(DataProfile profile, bool quickMode)
{
    return new ToolProfile
    {
        SourcePath = profile.SourcePath,
        RowCount = profile.RowCount,
        ColumnCount = profile.ColumnCount,
        ExecutiveSummary = quickMode 
            ? $"{profile.RowCount:N0} rows, {profile.ColumnCount} columns"
            : $"{profile.RowCount:N0} rows, {profile.ColumnCount} columns. " +
              $"{profile.Columns.Count(c => c.NullPercent > 0)} columns have nulls. " +
              $"{profile.Alerts.Count} alerts.",
        Columns = profile.Columns.Select(c => new ToolColumnProfile
        {
            Name = c.Name,
            Type = c.InferredType.ToString(),
            Role = c.SemanticRole != SemanticRole.Unknown ? c.SemanticRole.ToString() : null,
            NullPercent = Math.Round(c.NullPercent, 2),
            UniqueCount = c.UniqueCount,
            UniquePercent = Math.Round(c.UniquePercent, 2),
            Distribution = quickMode ? null : c.Distribution?.ToString(),
            Trend = quickMode ? null : c.Trend?.Direction.ToString(),
            Periodicity = quickMode || c.Periodicity == null ? null : new ToolPeriodicityInfo
            {
                Period = c.Periodicity.DominantPeriod,
                Confidence = Math.Round(c.Periodicity.Confidence, 3),
                Interpretation = c.Periodicity.SuggestedInterpretation
            },
            Stats = quickMode ? new ToolColumnStats
            {
                // Quick mode: only essential stats
                Min = c.Min,
                Max = c.Max,
                Mean = c.Mean != null ? Math.Round(c.Mean.Value, 4) : null,
                TopValue = c.TopValues?.FirstOrDefault()?.Value,
                TopValuePercent = c.TopValues?.FirstOrDefault()?.Percent
            } : new ToolColumnStats
            {
                Min = c.Min,
                Max = c.Max,
                Mean = c.Mean,
                Median = c.Median,
                StdDev = c.StdDev,
                Skewness = c.Skewness,
                Kurtosis = c.Kurtosis,
                OutlierCount = c.OutlierCount > 0 ? c.OutlierCount : null,
                ZeroCount = c.ZeroCount > 0 ? c.ZeroCount : null,
                CoefficientOfVariation = c.CoefficientOfVariation,
                Iqr = c.Iqr,
                TopValue = c.TopValues?.FirstOrDefault()?.Value,
                TopValuePercent = c.TopValues?.FirstOrDefault()?.Percent,
                ImbalanceRatio = c.ImbalanceRatio,
                Entropy = c.Entropy,
                MinDate = c.MinDate?.ToString("yyyy-MM-dd"),
                MaxDate = c.MaxDate?.ToString("yyyy-MM-dd"),
                DateGapDays = c.DateGapDays,
                DateSpanDays = c.DateSpanDays,
                AvgLength = c.AvgLength,
                MaxLength = c.MaxLength,
                MinLength = c.MinLength,
                EmptyStringCount = c.EmptyStringCount > 0 ? c.EmptyStringCount : null
            }
        }).ToList(),
        Alerts = quickMode 
            ? profile.Alerts.Where(a => a.Severity >= AlertSeverity.Warning).Take(5).Select(a => new ToolAlert
            {
                Severity = a.Severity.ToString(),
                Column = a.Column,
                Type = a.Type.ToString(),
                Message = a.Message
            }).ToList()
            : profile.Alerts.Select(a => new ToolAlert
            {
                Severity = a.Severity.ToString(),
                Column = a.Column,
                Type = a.Type.ToString(),
                Message = a.Message
            }).ToList(),
        Insights = quickMode 
            ? [] 
            : profile.Insights.Take(10).Select(i => new ToolInsight
            {
                Title = i.Title,
                Description = i.Description,
                Score = i.Score,
                Source = i.Source.ToString(),
                RelatedColumns = i.RelatedColumns.Count > 0 ? i.RelatedColumns : null
            }).ToList(),
        Correlations = quickMode 
            ? null 
            : profile.Correlations.Take(10).Select(c => new ToolCorrelation
            {
                Column1 = c.Column1,
                Column2 = c.Column2,
                Coefficient = c.Correlation,
                Strength = c.Strength
            }).ToList(),
        TargetAnalysis = profile.Target != null ? new ToolTargetAnalysis
        {
            TargetColumn = profile.Target.ColumnName,
            IsBinary = profile.Target.IsBinary,
            ClassDistribution = profile.Target.ClassDistribution.ToDictionary(
                kv => kv.Key, 
                kv => Math.Round(kv.Value * 100, 2)),
            TopDrivers = profile.Target.FeatureEffects.Take(5).Select(e => new ToolFeatureDriver
            {
                Feature = e.Feature,
                Magnitude = Math.Round(e.Magnitude, 4),
                Support = Math.Round(e.Support, 4),
                Summary = e.Summary,
                Metric = e.Metric
            }).ToList()
        } : null
    };
}

// Compute drift summary between current and baseline profiles
static ToolDriftSummary ComputeDrift(DataProfile current, DataProfile baseline, StoredProfileInfo baselineInfo)
{
    var comparator = new ProfileComparator();
    var diff = comparator.Compare(baseline, current);
    
    return new ToolDriftSummary
    {
        BaselineProfileId = baselineInfo.Id,
        BaselineDate = baselineInfo.StoredAt.ToString("o"),
        DriftScore = Math.Round(diff.OverallDriftScore, 4),
        HasSignificantDrift = diff.HasSignificantDrift,
        RowCountChangePercent = Math.Round(diff.RowCountChange.PercentChange, 2),
        DriftedColumnCount = diff.ColumnDiffs.Count(c => c.Psi >= 0.1),
        RemovedColumns = diff.SchemaChanges.RemovedColumns.Count > 0 ? diff.SchemaChanges.RemovedColumns : null,
        AddedColumns = diff.SchemaChanges.AddedColumns.Count > 0 ? diff.SchemaChanges.AddedColumns : null,
        Summary = diff.Summary,
        Recommendations = diff.Recommendations.Count > 0 ? diff.Recommendations : null
    };
}

// Format constraint validation as markdown
static string FormatConstraintValidationMarkdown(ConstraintValidationResult result)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"# Constraint Validation Report");
    sb.AppendLine();
    sb.AppendLine($"**Suite:** {result.SuiteName}");
    sb.AppendLine($"**Source:** {result.ProfileSource}");
    sb.AppendLine($"**Validated:** {result.ValidatedAt:yyyy-MM-dd HH:mm:ss}");
    sb.AppendLine();
    sb.AppendLine($"## Summary");
    sb.AppendLine();
    sb.AppendLine($"| Metric | Value |");
    sb.AppendLine($"|--------|-------|");
    sb.AppendLine($"| Pass Rate | {result.PassRate:P1} |");
    sb.AppendLine($"| Passed | {result.PassedConstraints} |");
    sb.AppendLine($"| Failed | {result.FailedConstraints} |");
    sb.AppendLine($"| Total | {result.TotalConstraints} |");
    sb.AppendLine();
    
    if (result.FailedConstraints > 0)
    {
        sb.AppendLine($"## Failed Constraints");
        sb.AppendLine();
        foreach (var failure in result.GetFailures())
        {
            sb.AppendLine($"- **{failure.Constraint.Type}**: {failure.Constraint.Description}");
            if (failure.ActualValue != null)
                sb.AppendLine($"  - Actual: {failure.ActualValue}");
            if (!string.IsNullOrEmpty(failure.Details))
                sb.AppendLine($"  - Details: {failure.Details}");
        }
    }
    else
    {
        sb.AppendLine("All constraints passed!");
    }
    
    return sb.ToString();
}

// Format constraint validation as HTML
static string FormatConstraintValidationHtml(ConstraintValidationResult result)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html><head><style>");
    sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }");
    sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
    sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
    sb.AppendLine("th { background: #f5f5f5; }");
    sb.AppendLine(".pass { color: green; } .fail { color: red; }");
    sb.AppendLine("</style></head><body>");
    sb.AppendLine($"<h1>Constraint Validation Report</h1>");
    sb.AppendLine($"<p><strong>Suite:</strong> {System.Net.WebUtility.HtmlEncode(result.SuiteName)}</p>");
    sb.AppendLine($"<p><strong>Pass Rate:</strong> <span class='{(result.AllPassed ? "pass" : "fail")}'>{result.PassRate:P1}</span></p>");
    
    sb.AppendLine("<table><tr><th>Status</th><th>Constraint</th><th>Actual</th><th>Details</th></tr>");
    foreach (var r in result.Results)
    {
        var status = r.Passed ? "<span class='pass'>PASS</span>" : "<span class='fail'>FAIL</span>";
        sb.AppendLine($"<tr><td>{status}</td><td>{System.Net.WebUtility.HtmlEncode(r.Constraint.Description)}</td><td>{r.ActualValue}</td><td>{System.Net.WebUtility.HtmlEncode(r.Details ?? "")}</td></tr>");
    }
    sb.AppendLine("</table></body></html>");
    
    return sb.ToString();
}

// Format validation/drift as markdown
static string FormatValidationMarkdown(dynamic result, ProfileDiffResult drift)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"# Data Drift Validation Report");
    sb.AppendLine();
    sb.AppendLine($"**Source:** {result.Source}");
    sb.AppendLine($"**Target:** {result.Target}");
    sb.AppendLine();
    sb.AppendLine($"## Summary");
    sb.AppendLine();
    sb.AppendLine($"| Metric | Value |");
    sb.AppendLine($"|--------|-------|");
    sb.AppendLine($"| Drift Score | {result.DriftScore:F3} |");
    sb.AppendLine($"| Anomaly Score | {result.AnomalyScore.OverallScore:F3} ({result.AnomalyScore.Interpretation}) |");
    sb.AppendLine($"| Significant Drift | {(drift.HasSignificantDrift ? "Yes" : "No")} |");
    sb.AppendLine();
    sb.AppendLine(drift.Summary);
    sb.AppendLine();
    
    if (drift.Recommendations.Count > 0)
    {
        sb.AppendLine("## Recommendations");
        sb.AppendLine();
        foreach (var rec in drift.Recommendations)
        {
            sb.AppendLine($"- {rec}");
        }
    }
    
    return sb.ToString();
}

// Format validation/drift as HTML
static string FormatValidationHtml(dynamic result, ProfileDiffResult drift)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html><head><style>");
    sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }");
    sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
    sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
    sb.AppendLine("th { background: #f5f5f5; }");
    sb.AppendLine(".good { color: green; } .warning { color: orange; } .bad { color: red; }");
    sb.AppendLine("</style></head><body>");
    sb.AppendLine($"<h1>Data Drift Validation Report</h1>");
    sb.AppendLine($"<p><strong>Source:</strong> {System.Net.WebUtility.HtmlEncode((string)result.Source)}</p>");
    sb.AppendLine($"<p><strong>Target:</strong> {System.Net.WebUtility.HtmlEncode((string)result.Target)}</p>");
    
    var driftClass = result.DriftScore < 0.3 ? "good" : result.DriftScore < 0.6 ? "warning" : "bad";
    sb.AppendLine($"<p><strong>Drift Score:</strong> <span class='{driftClass}'>{result.DriftScore:F3}</span></p>");
    sb.AppendLine($"<p><strong>Summary:</strong> {System.Net.WebUtility.HtmlEncode(drift.Summary)}</p>");
    
    if (drift.Recommendations.Count > 0)
    {
        sb.AppendLine("<h2>Recommendations</h2><ul>");
        foreach (var rec in drift.Recommendations)
        {
            sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(rec)}</li>");
        }
        sb.AppendLine("</ul>");
    }
    
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

// Format segment comparison as markdown
static string FormatSegmentComparisonMarkdown(SegmentComparison comparison, AnomalyScoreResult anomalyA, AnomalyScoreResult anomalyB)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"# Segment Comparison Report");
    sb.AppendLine();
    sb.AppendLine($"| Segment | Rows | Anomaly Score |");
    sb.AppendLine($"|---------|------|---------------|");
    sb.AppendLine($"| {comparison.SegmentAName} | {comparison.SegmentARowCount:N0} | {anomalyA.OverallScore:F3} ({anomalyA.Interpretation}) |");
    sb.AppendLine($"| {comparison.SegmentBName} | {comparison.SegmentBRowCount:N0} | {anomalyB.OverallScore:F3} ({anomalyB.Interpretation}) |");
    sb.AppendLine();
    sb.AppendLine($"**Similarity:** {comparison.Similarity:P1}");
    sb.AppendLine();
    sb.AppendLine("## Insights");
    sb.AppendLine();
    foreach (var insight in comparison.Insights)
    {
        sb.AppendLine($"- {insight}");
    }
    sb.AppendLine();
    
    if (comparison.ColumnComparisons.Count > 0)
    {
        sb.AppendLine("## Top Column Differences");
        sb.AppendLine();
        sb.AppendLine("| Column | Type | Distance | A | B |");
        sb.AppendLine("|--------|------|----------|---|---|");
        foreach (var col in comparison.ColumnComparisons.Take(10))
        {
            var valA = col.ColumnType == ColumnType.Numeric ? col.MeanA?.ToString("F2") ?? "-" : col.ModeA ?? "-";
            var valB = col.ColumnType == ColumnType.Numeric ? col.MeanB?.ToString("F2") ?? "-" : col.ModeB ?? "-";
            sb.AppendLine($"| {col.ColumnName} | {col.ColumnType} | {col.Distance:F3} | {valA} | {valB} |");
        }
    }
    
    return sb.ToString();
}

// Format segment comparison as HTML
static string FormatSegmentComparisonHtml(SegmentComparison comparison, AnomalyScoreResult anomalyA, AnomalyScoreResult anomalyB)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<!DOCTYPE html><html><head><style>");
    sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }");
    sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
    sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
    sb.AppendLine("th { background: #f5f5f5; }");
    sb.AppendLine(".high { color: green; } .medium { color: orange; } .low { color: red; }");
    sb.AppendLine("</style></head><body>");
    sb.AppendLine($"<h1>Segment Comparison Report</h1>");
    
    var simClass = comparison.Similarity >= 0.8 ? "high" : comparison.Similarity >= 0.5 ? "medium" : "low";
    sb.AppendLine($"<p><strong>Similarity:</strong> <span class='{simClass}'>{comparison.Similarity:P1}</span></p>");
    
    sb.AppendLine("<table><tr><th>Segment</th><th>Rows</th><th>Anomaly Score</th></tr>");
    sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(comparison.SegmentAName)}</td><td>{comparison.SegmentARowCount:N0}</td><td>{anomalyA.OverallScore:F3}</td></tr>");
    sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(comparison.SegmentBName)}</td><td>{comparison.SegmentBRowCount:N0}</td><td>{anomalyB.OverallScore:F3}</td></tr>");
    sb.AppendLine("</table>");
    
    sb.AppendLine("<h2>Insights</h2><ul>");
    foreach (var insight in comparison.Insights)
    {
        sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(insight)}</li>");
    }
    sb.AppendLine("</ul>");
    
    if (comparison.ColumnComparisons.Count > 0)
    {
        sb.AppendLine("<h2>Top Column Differences</h2>");
        sb.AppendLine("<table><tr><th>Column</th><th>Type</th><th>Distance</th><th>A</th><th>B</th></tr>");
        foreach (var col in comparison.ColumnComparisons.Take(10))
        {
            var valA = col.ColumnType == ColumnType.Numeric ? col.MeanA?.ToString("F2") ?? "-" : col.ModeA ?? "-";
            var valB = col.ColumnType == ColumnType.Numeric ? col.MeanB?.ToString("F2") ?? "-" : col.ModeB ?? "-";
            sb.AppendLine($"<tr><td>{col.ColumnName}</td><td>{col.ColumnType}</td><td>{col.Distance:F3}</td><td>{valA}</td><td>{valB}</td></tr>");
        }
        sb.AppendLine("</table>");
    }
    
    sb.AppendLine("</body></html>");
    return sb.ToString();
}

// Interactive profile management menu
static async Task ShowProfileManagementMenu(string? storePath)
{
    var store = new ProfileStore(storePath);
    
    while (true)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[cyan]Profile Store Management[/]").LeftJustified());
        AnsiConsole.WriteLine();
        
        var profiles = store.ListAll(100);
        if (profiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No stored profiles found.[/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]What would you like to do?[/]")
                .PageSize(10)
                .AddChoices(new[] {
                    "📋 List all profiles",
                    "🔍 View profile details",
                    "📊 Compare two profiles", 
                    "🗑️  Delete profile",
                    "🚫 Exclude from baseline",
                    "📌 Pin as baseline",
                    "🏷️  Add tags/notes",
                    "🧹 Prune old profiles",
                    "📈 Show statistics",
                    "❌ Exit"
                }));
        
        try
        {
            switch (choice)
            {
                case "📋 List all profiles":
                    await ListProfiles(store);
                    break;
                    
                case "🔍 View profile details":
                    await ViewProfileDetails(store, profiles);
                    break;
                    
                case "📊 Compare two profiles":
                    await CompareProfiles(store, profiles);
                    break;
                    
                case "🗑️  Delete profile":
                    await DeleteProfile(store, profiles);
                    break;
                    
                case "🚫 Exclude from baseline":
                    await ExcludeFromBaseline(store, profiles);
                    break;
                    
                case "📌 Pin as baseline":
                    await PinAsBaseline(store, profiles);
                    break;
                    
                case "🏷️  Add tags/notes":
                    await AddTagsNotes(store, profiles);
                    break;
                    
                case "🧹 Prune old profiles":
                    await PruneProfiles(store);
                    break;
                    
                case "📈 Show statistics":
                    await ShowStoreStatistics(store);
                    break;
                    
                case "❌ Exit":
                    return;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }
}

static async Task ListProfiles(ProfileStore store)
{
    var profiles = store.ListAll(100);
    
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("ID")
        .AddColumn("File")
        .AddColumn("Rows")
        .AddColumn("Cols")
        .AddColumn("Schema")
        .AddColumn("Stored")
        .AddColumn("Flags");
    
    foreach (var p in profiles)
    {
        var flags = new List<string>();
        if (p.IsPinnedBaseline) flags.Add("📌");
        if (p.ExcludeFromBaseline) flags.Add("🚫");
        if (!string.IsNullOrEmpty(p.Tags)) flags.Add("🏷️");
        
        table.AddRow(
            p.Id,
            Markup.Escape(Path.GetFileName(p.FileName)),
            p.RowCount.ToString("N0"),
            p.ColumnCount.ToString(),
            p.SchemaHash[..8],
            p.StoredAt.ToString("yyyy-MM-dd"),
            string.Join(" ", flags));
    }
    
    AnsiConsole.WriteLine();
    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"\n[dim]Total: {profiles.Count} profile(s)  |  📌 = pinned baseline  |  🚫 = excluded  |  🏷️ = has tags[/]");
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task ViewProfileDetails(ProfileStore store, List<StoredProfileInfo> profiles)
{
    var selected = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .Title("[yellow]Select profile to view:[/]")
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.RowCount:N0} rows)")
            .AddChoices(profiles));
    
    var profile = store.LoadProfile(selected.Id);
    if (profile == null)
    {
        AnsiConsole.MarkupLine("[red]Profile not found[/]");
        return;
    }
    
    AnsiConsole.Clear();
    AnsiConsole.Write(new Rule($"[cyan]Profile: {selected.FileName}[/]").LeftJustified());
    AnsiConsole.WriteLine();
    
    var grid = new Grid()
        .AddColumn()
        .AddColumn()
        .AddRow("[bold]ID:[/]", selected.Id)
        .AddRow("[bold]File:[/]", Markup.Escape(selected.SourcePath))
        .AddRow("[bold]Rows:[/]", selected.RowCount.ToString("N0"))
        .AddRow("[bold]Columns:[/]", selected.ColumnCount.ToString())
        .AddRow("[bold]Schema Hash:[/]", selected.SchemaHash)
        .AddRow("[bold]Stored:[/]", selected.StoredAt.ToString("yyyy-MM-dd HH:mm:ss"))
        .AddRow("[bold]Tags:[/]", selected.Tags ?? "[dim]none[/]")
        .AddRow("[bold]Notes:[/]", selected.Notes ?? "[dim]none[/]");
    
    if (selected.IsPinnedBaseline)
    {
        grid.AddRow("[bold]Baseline:[/]", "[green]📌 Pinned as baseline[/]");
    }
    if (selected.ExcludeFromBaseline)
    {
        grid.AddRow("[bold]Excluded:[/]", "[red]🚫 Excluded from baseline[/]");
    }
    
    AnsiConsole.Write(grid);
    AnsiConsole.WriteLine();
    
    AnsiConsole.MarkupLine($"\n[bold]Columns ({profile.ColumnCount}):[/]");
    foreach (var col in profile.Columns.Take(10))
    {
        AnsiConsole.MarkupLine($"  [cyan]{col.Name}[/]: {col.InferredType} ({col.NullPercent:F1}% null, {col.UniquePercent:F1}% unique)");
    }
    if (profile.ColumnCount > 10)
    {
        AnsiConsole.MarkupLine($"  [dim]... and {profile.ColumnCount - 10} more[/]");
    }
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task CompareProfiles(ProfileStore store, List<StoredProfileInfo> profiles)
{
    AnsiConsole.MarkupLine("[yellow]Select baseline profile:[/]");
    var baseline = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles));
    
    AnsiConsole.MarkupLine("\n[yellow]Select current profile to compare:[/]");
    var current = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles.Where(p => p.Id != baseline.Id)));
    
    var baselineProfile = store.LoadProfile(baseline.Id);
    var currentProfile = store.LoadProfile(current.Id);
    
    if (baselineProfile == null || currentProfile == null)
    {
        AnsiConsole.MarkupLine("[red]Failed to load profiles[/]");
        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
        Console.ReadKey(true);
        return;
    }
    
    AnsiConsole.Status()
        .Start("Comparing profiles...", ctx =>
        {
            var comparator = new ProfileComparator();
            var diff = comparator.Compare(baselineProfile, currentProfile);
            
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[cyan]Profile Comparison[/]").LeftJustified());
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine($"[bold]Baseline:[/] {baseline.FileName} ({baseline.StoredAt:yyyy-MM-dd})");
            AnsiConsole.MarkupLine($"[bold]Current:[/] {current.FileName} ({current.StoredAt:yyyy-MM-dd})");
            AnsiConsole.WriteLine();
            
            var driftColor = diff.OverallDriftScore > 0.3 ? "red" : (diff.OverallDriftScore > 0.1 ? "yellow" : "green");
            AnsiConsole.MarkupLine($"[bold]Drift Score:[/] [{driftColor}]{diff.OverallDriftScore:F3}[/]");
            AnsiConsole.MarkupLine($"[bold]Row Count Change:[/] {diff.RowCountChange.PercentChange:+0.0;-0.0}%");
            AnsiConsole.WriteLine();
            
            if (diff.SchemaChanges.HasChanges)
            {
                AnsiConsole.MarkupLine("[red]⚠ Schema Changes Detected[/]");
                if (diff.SchemaChanges.AddedColumns.Count > 0)
                    AnsiConsole.MarkupLine($"  [green]+[/] Added: {string.Join(", ", diff.SchemaChanges.AddedColumns)}");
                if (diff.SchemaChanges.RemovedColumns.Count > 0)
                    AnsiConsole.MarkupLine($"  [red]-[/] Removed: {string.Join(", ", diff.SchemaChanges.RemovedColumns)}");
                AnsiConsole.WriteLine();
            }
            
            if (diff.ColumnDiffs.Count > 0)
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Column")
                    .AddColumn("Type")
                    .AddColumn("PSI")
                    .AddColumn("KS/JS")
                    .AddColumn("Null Δ");
                
                foreach (var col in diff.ColumnDiffs.OrderByDescending(c => c.Psi ?? c.KsDistance ?? c.JsDivergence ?? 0).Take(10))
                {
                    var metric = col.KsDistance?.ToString("F3") ?? col.JsDivergence?.ToString("F3") ?? "-";
                    var psi = col.Psi?.ToString("F3") ?? "-";
                    var nullDelta = col.NullPercentChange?.AbsoluteChange.ToString("+0.0;-0.0") ?? "-";
                    
                    table.AddRow(
                        col.ColumnName,
                        col.ColumnType.ToString(),
                        psi,
                        metric,
                        nullDelta);
                }
                
                AnsiConsole.MarkupLine("[bold]Top Drifted Columns:[/]");
                AnsiConsole.Write(table);
            }
            
            if (diff.Summary != null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(diff.Summary)}[/]");
            }
        });
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task DeleteProfile(ProfileStore store, List<StoredProfileInfo> profiles)
{
    var selected = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .Title("[yellow]Select profile to delete:[/]")
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles));
    
    if (!AnsiConsole.Confirm($"[red]Delete profile {selected.Id}?[/]", defaultValue: false))
    {
        return;
    }
    
    if (store.Delete(selected.Id))
    {
        AnsiConsole.MarkupLine($"[green]✓ Deleted profile {selected.Id}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine($"[red]✗ Failed to delete profile {selected.Id}[/]");
    }
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task ExcludeFromBaseline(ProfileStore store, List<StoredProfileInfo> profiles)
{
    var selected = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .Title("[yellow]Select profile to exclude from baseline:[/]")
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles));
    
    selected.ExcludeFromBaseline = !selected.ExcludeFromBaseline;
    store.UpdateMetadata(selected);
    
    var status = selected.ExcludeFromBaseline ? "[red]excluded from[/]" : "[green]included in[/]";
    AnsiConsole.MarkupLine($"[green]✓[/] Profile {selected.Id} is now {status} baseline selection");
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task PinAsBaseline(ProfileStore store, List<StoredProfileInfo> profiles)
{
    var selected = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .Title("[yellow]Select profile to pin as baseline:[/]")
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles));
    
    // Unpin others with same schema
    var schemaHash = selected.SchemaHash;
    foreach (var p in profiles.Where(p => p.SchemaHash == schemaHash && p.Id != selected.Id))
    {
        if (p.IsPinnedBaseline)
        {
            p.IsPinnedBaseline = false;
            store.UpdateMetadata(p);
        }
    }
    
    selected.IsPinnedBaseline = !selected.IsPinnedBaseline;
    store.UpdateMetadata(selected);
    
    var status = selected.IsPinnedBaseline ? "[green]📌 pinned as baseline[/]" : "[yellow]unpinned[/]";
    AnsiConsole.MarkupLine($"[green]✓[/] Profile {selected.Id} is now {status}");
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task AddTagsNotes(ProfileStore store, List<StoredProfileInfo> profiles)
{
    var selected = AnsiConsole.Prompt(
        new SelectionPrompt<StoredProfileInfo>()
            .Title("[yellow]Select profile to edit:[/]")
            .PageSize(15)
            .UseConverter(p => $"{p.Id} - {Path.GetFileName(p.FileName)} ({p.StoredAt:yyyy-MM-dd})")
            .AddChoices(profiles));
    
    var tags = AnsiConsole.Ask("[yellow]Tags (comma-separated):[/]", selected.Tags ?? "");
    var notes = AnsiConsole.Ask("[yellow]Notes:[/]", selected.Notes ?? "");
    
    selected.Tags = string.IsNullOrWhiteSpace(tags) ? null : tags;
    selected.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
    
    store.UpdateMetadata(selected);
    
    AnsiConsole.MarkupLine($"[green]✓ Updated metadata for profile {selected.Id}[/]");
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task PruneProfiles(ProfileStore store)
{
    var keep = AnsiConsole.Ask("[yellow]How many profiles to keep per schema?[/]", 3);
    
    if (!AnsiConsole.Confirm($"[yellow]Keep {keep} most recent profiles per schema and delete the rest?[/]", defaultValue: false))
    {
        return;
    }
    
    var pruned = store.PruneOldProfiles(keep);
    AnsiConsole.MarkupLine($"[green]✓ Pruned {pruned} old profile(s)[/]");
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}

static async Task ShowStoreStatistics(ProfileStore store)
{
    var stats = store.GetStatistics();
    
    AnsiConsole.Clear();
    AnsiConsole.Write(new Rule("[cyan]Store Statistics[/]").LeftJustified());
    AnsiConsole.WriteLine();
    
    var grid = new Grid()
        .AddColumn()
        .AddColumn()
        .AddRow("[bold]Total Profiles:[/]", stats.TotalProfiles.ToString())
        .AddRow("[bold]Unique Schemas:[/]", stats.UniqueSchemas.ToString())
        .AddRow("[bold]Total Rows Profiled:[/]", stats.TotalRowsProfiled.ToString("N0"))
        .AddRow("[bold]Disk Usage:[/]", $"{stats.TotalDiskUsageMB:F2} MB")
        .AddRow("[bold]Oldest Profile:[/]", stats.OldestProfile?.ToString("yyyy-MM-dd HH:mm") ?? "-")
        .AddRow("[bold]Newest Profile:[/]", stats.NewestProfile?.ToString("yyyy-MM-dd HH:mm") ?? "-");
    
    AnsiConsole.Write(grid);
    
    AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
    Console.ReadKey(true);
}
