using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Provides progress feedback using Spectre.Console
/// </summary>
public class ProgressService
{
    private readonly bool _verbose;
    
    /// <summary>
    /// Default timeout for LLM operations (10 minutes for large documents)
    /// </summary>
    public static readonly TimeSpan DefaultLlmTimeout = TimeSpan.FromMinutes(10);
    
    /// <summary>
    /// Default timeout for document conversion (5 minutes for large PDFs)
    /// </summary>
    public static readonly TimeSpan DefaultDoclingTimeout = TimeSpan.FromMinutes(5);

    public ProgressService(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Display a status spinner while executing an async operation
    /// </summary>
    public async Task<T> WithStatusAsync<T>(string status, Func<Task<T>> operation)
    {
        if (!_verbose)
        {
            return await operation();
        }

        // Always output status immediately for non-interactive terminals
        // Spectre's Status spinner may not show in captured/piped output
        Console.WriteLine(status);
        Console.Out.Flush();

        // Check if we're in an interactive terminal that supports ANSI
        if (AnsiConsole.Profile.Capabilities.Interactive)
        {
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync(status, async ctx => await operation());
        }
        else
        {
            // Non-interactive: just run the operation
            return await operation();
        }
    }

    /// <summary>
    /// Display a progress bar for multiple items with live updates
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
            var tasks = itemList.Select(async item =>
            {
                return await operation(item, null!);
            });
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
    /// Display a live table that updates as chunks are processed
    /// </summary>
    /// <param name="maxParallelism">Maximum parallel operations (defaults to Environment.ProcessorCount)</param>
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
        var maxDegree = isUnlimited ? -1 : maxParallelism.Value;
        
        // Initialize statuses
        for (int i = 0; i < itemList.Count; i++)
        {
            statuses[i] = "[grey]Pending[/]";
        }

        if (!_verbose)
        {
            if (isUnlimited)
            {
                // Unlimited: use Task.WhenAll for maximum parallelism
                var tasks = itemList.Select(async (item, idx) =>
                {
                    results[idx] = await operation(item);
                });
                await Task.WhenAll(tasks);
            }
            else
            {
                // Use controlled parallelism
                var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };
                await Parallel.ForEachAsync(
                    itemList.Select((item, idx) => (item, idx)),
                    options,
                    async (pair, ct) =>
                    {
                        results[pair.idx] = await operation(pair.item);
                    });
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
                                statuses[idx] = $"[red]Error: {Markup.Escape(ex.Message.Length > 30 ? ex.Message[..30] + "..." : ex.Message)}[/]";
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
                                statuses[idx] = $"[red]Error: {Markup.Escape(ex.Message.Length > 30 ? ex.Message[..30] + "..." : ex.Message)}[/]";
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

    private static Table CreateStatusTable<TInput>(
        string title,
        List<TInput> items,
        Func<TInput, string> getName,
        string[] statuses)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold blue]{title}[/]")
            .AddColumn(new TableColumn("[bold]Chunk[/]").Centered())
            .AddColumn(new TableColumn("[bold]Section[/]"))
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        for (int i = 0; i < items.Count; i++)
        {
            var name = getName(items[i]);
            var displayName = name.Length > 40 ? name[..37] + "..." : name;
            table.AddRow(
                $"[cyan]{i}[/]",
                Markup.Escape(displayName),
                statuses[i]);
        }

        return table;
    }

    /// <summary>
    /// Write a styled info message
    /// </summary>
    public void Info(string message)
    {
        if (_verbose)
        {
            AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
        }
    }

    /// <summary>
    /// Write a styled success message
    /// </summary>
    public void Success(string message)
    {
        if (_verbose)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
        }
    }

    /// <summary>
    /// Write a styled warning message
    /// </summary>
    public void Warning(string message)
    {
        if (_verbose)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
        }
    }

    /// <summary>
    /// Write a styled error message
    /// </summary>
    public void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display a rule/separator
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
    /// Display the final summary in a panel
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
    /// Display trace information in a table
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
}
