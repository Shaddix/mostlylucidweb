using Spectre.Console;
using Spectre.Console.Rendering;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
///     Provides progress feedback using Spectre.Console
/// </summary>
public class ProgressService
{
    /// <summary>
    ///     Default timeout for LLM operations (10 minutes for large documents)
    /// </summary>
    public static readonly TimeSpan DefaultLlmTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Default timeout for document conversion (5 minutes for large PDFs)
    /// </summary>
    public static readonly TimeSpan DefaultDoclingTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Track if we're already inside an interactive display (prevents nesting conflicts)
    /// </summary>
    [ThreadStatic] private static bool _isInInteractiveDisplay;

    private readonly bool _verbose;

    public ProgressService(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    ///     Check if we're already inside an interactive display
    /// </summary>
    public static bool IsInInteractiveContext => _isInInteractiveDisplay;

    /// <summary>
    ///     Enter an interactive display context (prevents nested displays)
    /// </summary>
    public static IDisposable EnterInteractiveContext()
    {
        _isInInteractiveDisplay = true;
        return new InteractiveContextGuard();
    }

    /// <summary>
    ///     Display a status spinner while executing an async operation
    /// </summary>
    public async Task<T> WithStatusAsync<T>(string status, Func<Task<T>> operation)
    {
        if (!_verbose) return await operation();

        // Always output status immediately for non-interactive terminals
        // Spectre's Status spinner may not show in captured/piped output
        Console.WriteLine(status);
        Console.Out.Flush();

        // Check if we're in an interactive terminal that supports ANSI
        // AND we're not already inside another interactive display (prevents nesting conflicts)
        if (AnsiConsole.Profile.Capabilities.Interactive && !_isInInteractiveDisplay)
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync(status, async ctx => await operation());

        // Non-interactive or nested: just run the operation
        return await operation();
    }

    /// <summary>
    ///     Display a progress bar for multiple items with live updates
    /// </summary>
    public async Task<List<T>> WithProgressAsync<TInput, T>(
        string description,
        IEnumerable<TInput> items,
        Func<TInput, ProgressTask, Task<T>> operation)
    {
        var itemList = items.ToList();
        var results = new List<T>();

        if (!_verbose)
        {
            // Non-verbose: just run operations without progress display
            var tasks = itemList.Select(async item => { return await operation(item, null!); });
            results = (await Task.WhenAll(tasks)).ToList();
            return results;
        }

        // Skip interactive display if already inside one
        if (_isInInteractiveDisplay)
        {
            var tasks = itemList.Select(async item => { return await operation(item, null!); });
            results = (await Task.WhenAll(tasks)).ToList();
            return results;
        }

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var mainTask = ctx.AddTask($"[blue]{description}[/]", maxValue: itemList.Count);
                var completedCount = 0;
                var lockObj = new object();

                var tasks = itemList.Select(async item =>
                {
                    var result = await operation(item, mainTask);
                    lock (lockObj)
                    {
                        completedCount++;
                        mainTask.Value = completedCount;
                    }

                    return result;
                });

                var taskResults = await Task.WhenAll(tasks);
                results = taskResults.ToList();
            });

        return results;
    }

    /// <summary>
    ///     Display a live table that updates as chunks are processed
    /// </summary>
    /// <typeparam name="TInput">The type of input items</typeparam>
    /// <typeparam name="T">The type of result items</typeparam>
    /// <param name="title">Title for the progress display</param>
    /// <param name="items">Items to process</param>
    /// <param name="getIndex">Function to get index from an item</param>
    /// <param name="getName">Function to get display name from an item</param>
    /// <param name="operation">Async operation to perform on each item</param>
    /// <param name="maxParallelism">Maximum parallel operations (defaults to Environment.ProcessorCount)</param>
    /// <returns>List of results from processing all items</returns>
    public async Task<List<T>> WithLiveTableAsync<TInput, T>(
        string title,
        IEnumerable<TInput> items,
        Func<TInput, int> getIndex,
        Func<TInput, string> getName,
        Func<TInput, Task<T>> operation,
        int? maxParallelism = null)
    {
        var itemList = items.ToList();
        var results = new T[itemList.Count];
        var statuses = new string[itemList.Count];
        // -1 or 0 means unlimited parallelism
        var isUnlimited = !maxParallelism.HasValue || maxParallelism.Value <= 0;
        var maxDegree = isUnlimited ? -1 : maxParallelism.GetValueOrDefault();

        // Initialize statuses
        for (var i = 0; i < itemList.Count; i++) statuses[i] = "[grey]Pending[/]";

        if (!_verbose)
        {
            if (isUnlimited)
            {
                // Unlimited: use Task.WhenAll for maximum parallelism
                var tasks = itemList.Select(async (item, idx) => { results[idx] = await operation(item); });
                await Task.WhenAll(tasks);
            }
            else
            {
                // Use controlled parallelism
                var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };
                await Parallel.ForEachAsync(
                    itemList.Select((item, idx) => (item, idx)),
                    options,
                    async (pair, ct) => { results[pair.idx] = await operation(pair.item); });
            }

            return results.ToList();
        }

        // Skip interactive display if already inside one
        if (_isInInteractiveDisplay)
        {
            if (isUnlimited)
            {
                var tasks = itemList.Select(async (item, idx) => { results[idx] = await operation(item); });
                await Task.WhenAll(tasks);
            }
            else
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };
                await Parallel.ForEachAsync(
                    itemList.Select((item, idx) => (item, idx)),
                    options,
                    async (pair, ct) => { results[pair.idx] = await operation(pair.item); });
            }

            return results.ToList();
        }

        // Verbose mode with live table display
        await AnsiConsole.Live(CreateStatusTable(title, itemList, getName, statuses))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                var lockObj = new object();

                if (isUnlimited)
                {
                    // Unlimited parallelism - no semaphore needed
                    var tasks = itemList.Select(async (item, idx) =>
                    {
                        try
                        {
                            lock (lockObj)
                            {
                                statuses[idx] = "[yellow]Processing...[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }

                            results[idx] = await operation(item);

                            lock (lockObj)
                            {
                                statuses[idx] = "[green]Done[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                statuses[idx] =
                                    $"[red]Error: {Markup.Escape(ex.Message.Length > 30 ? ex.Message[..30] + "..." : ex.Message)}[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }

                            throw;
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                else
                {
                    // Limited parallelism with semaphore
                    using var semaphore = new SemaphoreSlim(maxDegree, maxDegree);

                    var tasks = itemList.Select(async (item, idx) =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            lock (lockObj)
                            {
                                statuses[idx] = "[yellow]Processing...[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }

                            results[idx] = await operation(item);

                            lock (lockObj)
                            {
                                statuses[idx] = "[green]Done[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                statuses[idx] =
                                    $"[red]Error: {Markup.Escape(ex.Message.Length > 30 ? ex.Message[..30] + "..." : ex.Message)}[/]";
                                ctx.UpdateTarget(CreateStatusTable(title, itemList, getName, statuses));
                            }

                            throw;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }
            });

        return results.ToList();
    }

    private static IRenderable CreateStatusTable<TInput>(
        string title,
        List<TInput> items,
        Func<TInput, string> getName,
        string[] statuses)
    {
        // Count status types for summary
        var pending = statuses.Count(s => s.Contains("Pending"));
        var processing = statuses.Count(s => s.Contains("Processing"));
        var done = statuses.Count(s => s.Contains("Done"));
        var errors = statuses.Count(s => s.Contains("Error"));
        var total = items.Count;
        
        // Create a visual chunk grid (like a progress bar made of blocks)
        var chunkGrid = CreateChunkGrid(statuses);
        
        // Create stats panel
        var statsGrid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap());
        
        statsGrid.AddRow(
            new Markup($"[grey]Pending:[/] [white]{pending}[/]"),
            new Markup($"[yellow]Active:[/] [yellow]{processing}[/]"),
            new Markup($"[green]Done:[/] [green]{done}[/]"),
            errors > 0 ? new Markup($"[red]Errors:[/] [red]{errors}[/]") : new Markup("[grey]Errors:[/] [grey]0[/]")
        );
        
        // Progress percentage
        var progressPercent = total > 0 ? (double)done / total * 100 : 0;
        var progressBar = new BreakdownChart()
            .Width(60)
            .AddItem("Done", done, Color.Green)
            .AddItem("Active", processing, Color.Yellow)
            .AddItem("Pending", pending, Color.Grey);
        
        // Build the layout
        var layout = new Grid()
            .AddColumn();
        
        layout.AddRow(new Rule($"[bold blue]{title}[/]").RuleStyle("blue"));
        layout.AddEmptyRow();
        layout.AddRow(new Panel(chunkGrid)
            .Header("[bold]Chunk Status[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey")));
        layout.AddEmptyRow();
        layout.AddRow(statsGrid);
        layout.AddRow(new Markup($"[bold]Progress:[/] [cyan]{progressPercent:F0}%[/] ({done}/{total} chunks)"));
        
        // Only show detailed table if there are items actively processing or with errors
        if (processing > 0 || errors > 0)
        {
            layout.AddEmptyRow();
            var activeTable = CreateActiveItemsTable(items, getName, statuses);
            layout.AddRow(activeTable);
        }
        
        return layout;
    }
    
    /// <summary>
    /// Create a visual grid of chunk statuses using colored blocks
    /// </summary>
    private static IRenderable CreateChunkGrid(string[] statuses)
    {
        // Create a visual representation with colored squares
        var blocks = new List<string>();
        
        for (var i = 0; i < statuses.Length; i++)
        {
            var status = statuses[i];
            string block;
            
            if (status.Contains("Done"))
                block = "[green]█[/]";
            else if (status.Contains("Processing"))
                block = "[yellow]█[/]";
            else if (status.Contains("Error"))
                block = "[red]█[/]";
            else
                block = "[grey]░[/]";
            
            blocks.Add(block);
        }
        
        // Group into rows of 20 for readability
        const int blocksPerRow = 20;
        var rows = new List<string>();
        
        for (var i = 0; i < blocks.Count; i += blocksPerRow)
        {
            var rowBlocks = blocks.Skip(i).Take(blocksPerRow);
            var rowStr = string.Join("", rowBlocks);
            var startIdx = i;
            var endIdx = Math.Min(i + blocksPerRow - 1, blocks.Count - 1);
            rows.Add($"[grey]{startIdx,3}-{endIdx,3}[/] {rowStr}");
        }
        
        return new Markup(string.Join("\n", rows));
    }
    
    /// <summary>
    /// Create a table showing only active and errored items
    /// </summary>
    private static Table CreateActiveItemsTable<TInput>(
        List<TInput> items,
        Func<TInput, string> getName,
        string[] statuses)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn(new TableColumn("[bold]#[/]").Centered().Width(4))
            .AddColumn(new TableColumn("[bold]Section[/]"))
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());
        
        for (var i = 0; i < items.Count; i++)
        {
            // Only show processing or error items
            if (!statuses[i].Contains("Processing") && !statuses[i].Contains("Error"))
                continue;
                
            var name = getName(items[i]);
            var displayName = name.Length > 35 ? name[..32] + "..." : name;
            table.AddRow(
                $"[cyan]{i}[/]",
                Markup.Escape(displayName),
                statuses[i]);
        }
        
        return table;
    }

    /// <summary>
    ///     Write a styled info message
    /// </summary>
    public void Info(string message)
    {
        if (_verbose) AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    /// <summary>
    ///     Write a styled success message
    /// </summary>
    public void Success(string message)
    {
        if (_verbose) AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    ///     Write a styled warning message
    /// </summary>
    public void Warning(string message)
    {
        if (_verbose) AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    /// <summary>
    ///     Write a styled error message
    /// </summary>
    public void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    /// <summary>
    ///     Display a rule/separator
    /// </summary>
    public void Rule(string? title = null)
    {
        if (_verbose)
        {
            if (title != null)
                AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(title)}[/]").RuleStyle("grey"));
            else
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
        }
    }

    /// <summary>
    ///     Display the final summary in a panel
    /// </summary>
    public void DisplaySummary(string summary, string title = "Summary")
    {
        var panel = new Panel(Markup.Escape(summary))
            .Header($"[bold blue]{Markup.Escape(title)}[/]")
            .Border(BoxBorder.Double)
            .BorderStyle(Style.Parse("blue"))
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    ///     Display trace information in a table
    /// </summary>
    public void DisplayTrace(
        string docId,
        int totalChunks,
        int processedChunks,
        int topicCount,
        TimeSpan duration,
        double coverage,
        double citationRate)
    {
        if (!_verbose) return;

        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Processing Trace[/]")
            .AddColumn(new TableColumn("[bold]Metric[/]"))
            .AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

        table.AddRow("Document", Markup.Escape(docId));
        table.AddRow("Total Chunks", totalChunks.ToString());
        table.AddRow("Processed", processedChunks.ToString());
        table.AddRow("Topics", topicCount.ToString());
        table.AddRow("Duration", $"{duration.TotalSeconds:F1}s");
        table.AddRow("Coverage", FormatPercentage(coverage));
        table.AddRow("Citation Rate", $"{citationRate:F2}");

        AnsiConsole.Write(table);
    }

    private static string FormatPercentage(double value)
    {
        var percent = value * 100;
        var color = percent >= 80 ? "green" : percent >= 50 ? "yellow" : "red";
        return $"[{color}]{percent:F0}%[/]";
    }

    private class InteractiveContextGuard : IDisposable
    {
        public void Dispose()
        {
            _isInInteractiveDisplay = false;
        }
    }
}