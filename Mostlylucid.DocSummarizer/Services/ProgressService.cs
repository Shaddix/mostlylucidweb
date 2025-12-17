namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Provides simple console-based progress feedback.
/// Replaces Spectre.Console with plain text output for AOT compatibility.
/// </summary>
public class ProgressService
{
    /// <summary>
    /// Default timeout for LLM operations (20 minutes for large documents)
    /// </summary>
    public static readonly TimeSpan DefaultLlmTimeout = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Default timeout for document conversion (5 minutes for large PDFs)
    /// </summary>
    public static readonly TimeSpan DefaultDoclingTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Track if we're already inside an interactive display
    /// </summary>
    [ThreadStatic] private static bool _isInInteractiveDisplay;

    private readonly bool _verbose;

    public ProgressService(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Check if we're already inside an interactive display
    /// </summary>
    public static bool IsInInteractiveContext => _isInInteractiveDisplay;

    /// <summary>
    /// Enter an interactive display context
    /// </summary>
    public static IDisposable EnterInteractiveContext()
    {
        _isInInteractiveDisplay = true;
        return new InteractiveContextGuard();
    }

    /// <summary>
    /// Display a status while executing an async operation
    /// </summary>
    public async Task<T> WithStatusAsync<T>(string status, Func<Task<T>> operation)
    {
        if (_verbose)
        {
            Console.WriteLine(status);
            Console.Out.Flush();
        }
        return await operation();
    }

    /// <summary>
    /// Execute multiple operations with progress tracking
    /// </summary>
    public async Task WithProgressAsync<T>(
        string title,
        IList<T> items,
        Func<T, string> getName,
        Func<T, int, Task> processItem,
        int maxParallel = 1)
    {
        if (_verbose)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
            Console.WriteLine($"Processing {items.Count} items (max {maxParallel} parallel)");
            Console.Out.Flush();
        }

        var completed = 0;
        var total = items.Count;
        var semaphore = new SemaphoreSlim(maxParallel);
        var tasks = new List<Task>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i;

            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var name = getName(item);
                    if (_verbose)
                    {
                        Console.WriteLine($"  [{index + 1}/{total}] Starting: {name}");
                        Console.Out.Flush();
                    }

                    await processItem(item, index);

                    Interlocked.Increment(ref completed);
                    if (_verbose)
                    {
                        Console.WriteLine($"  [{completed}/{total}] Completed: {name}");
                        Console.Out.Flush();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        if (_verbose)
        {
            Console.WriteLine($"Completed {completed}/{total} items");
            Console.Out.Flush();
        }
    }

    /// <summary>
    /// Execute operations with live status updates
    /// </summary>
    public async Task WithLiveStatusAsync<T>(
        string title,
        IList<T> items,
        Func<T, string> getName,
        Func<T, int, Dictionary<T, string>, Task> processItem,
        int maxParallel = 1)
    {
        var statuses = items.ToDictionary(x => x, _ => "Pending");

        if (_verbose)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
            Console.WriteLine($"Processing {items.Count} items");
            Console.Out.Flush();
        }

        var completed = 0;
        var semaphore = new SemaphoreSlim(maxParallel);
        var tasks = new List<Task>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i;

            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    statuses[item] = "Processing...";
                    await processItem(item, index, statuses);
                    Interlocked.Increment(ref completed);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Display info message
    /// </summary>
    public void Info(string message)
    {
        if (_verbose) Console.WriteLine($"[INFO] {message}");
    }

    /// <summary>
    /// Display success message
    /// </summary>
    public void Success(string message)
    {
        if (_verbose) Console.WriteLine($"[OK] {message}");
    }

    /// <summary>
    /// Display warning message
    /// </summary>
    public void Warning(string message)
    {
        if (_verbose) Console.WriteLine($"[WARN] {message}");
    }

    /// <summary>
    /// Display error message
    /// </summary>
    public void Error(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }

    /// <summary>
    /// Display a section header
    /// </summary>
    public void WriteHeader(string title)
    {
        if (!_verbose) return;
        Console.WriteLine();
        Console.WriteLine(new string('=', Math.Min(80, title.Length + 4)));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', Math.Min(80, title.Length + 4)));
    }

    /// <summary>
    /// Display a divider with optional title
    /// </summary>
    public void WriteDivider(string? title = null)
    {
        if (!_verbose) return;
        Console.WriteLine();
        if (title != null)
            Console.WriteLine($"--- {title} ---");
        else
            Console.WriteLine(new string('-', 40));
    }

    /// <summary>
    /// Display a summary box
    /// </summary>
    public void WriteSummary(string content)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(content);
        Console.WriteLine(new string('=', 60));
        Console.WriteLine();
    }

    private class InteractiveContextGuard : IDisposable
    {
        public void Dispose()
        {
            _isInInteractiveDisplay = false;
        }
    }
}
