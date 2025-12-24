namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Simple progress service for library mode.
/// Replaces CLI-specific Spectre.Console progress functionality.
/// </summary>
public class ProgressService
{
    private readonly bool _verbose;

    /// <summary>
    /// Creates a new progress service.
    /// </summary>
    /// <param name="verbose">Whether to output verbose messages.</param>
    public ProgressService(bool verbose = false)
    {
        _verbose = verbose;
    }

    /// <summary>
    /// Whether we're in an interactive context (always false in library mode).
    /// </summary>
    public static bool IsInInteractiveContext => false;

    /// <summary>
    /// Check if verbose output should be shown.
    /// </summary>
    public static bool ShouldShowVerbose(bool verbose) => verbose;

    /// <summary>
    /// Write verbose output with markup (stripped in library mode).
    /// </summary>
    public static void WriteVerboseMarkup(bool verbose, string message)
    {
        if (verbose)
        {
            VerboseHelper.Log(message);
        }
    }

    /// <summary>
    /// Write verbose output.
    /// </summary>
    public static void WriteVerbose(bool verbose, string message)
    {
        if (verbose)
        {
            VerboseHelper.Log(message);
        }
    }

    /// <summary>
    /// Write a divider line to the console.
    /// </summary>
    /// <param name="label">Optional label for the divider.</param>
    public void WriteDivider(string? label = null)
    {
        if (_verbose)
        {
            if (!string.IsNullOrEmpty(label))
            {
                Console.WriteLine($"--- {label} ---");
            }
            else
            {
                Console.WriteLine("---");
            }
        }
    }

    /// <summary>
    /// Write an info message.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void Info(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[INFO] {VerboseHelper.Escape(message)}");
        }
    }

    /// <summary>
    /// Write a success message.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void Success(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[SUCCESS] {VerboseHelper.Escape(message)}");
        }
    }

    /// <summary>
    /// Write a warning message.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void Warning(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[WARNING] {VerboseHelper.Escape(message)}");
        }
    }

    /// <summary>
    /// Execute an action with a status message (no-op spinner in library mode).
    /// </summary>
    /// <param name="statusMessage">The status message to display.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task WithStatusAsync(string statusMessage, Func<Task> action)
    {
        if (_verbose)
        {
            Console.WriteLine($"[STATUS] {statusMessage}");
        }
        await action();
    }

    /// <summary>
    /// Execute a function with a status message and return the result.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="statusMessage">The status message to display.</param>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    public async Task<T> WithStatusAsync<T>(string statusMessage, Func<Task<T>> func)
    {
        if (_verbose)
        {
            Console.WriteLine($"[STATUS] {statusMessage}");
        }
        return await func();
    }
}
