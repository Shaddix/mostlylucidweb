using Spectre.Console;

namespace Mostlylucid.Summarizer.Shared.Services;

/// <summary>
/// Shared progress/status reporting for CLI tools.
/// Wraps Spectre.Console with fallback for non-interactive terminals.
/// </summary>
public static class ProgressService
{
    private static bool? _isInteractive;

    public static bool IsInteractive
    {
        get
        {
            _isInteractive ??= !Console.IsOutputRedirected && 
                               !Console.IsErrorRedirected &&
                               Environment.GetEnvironmentVariable("CI") == null;
            return _isInteractive.Value;
        }
    }

    /// <summary>
    /// Run an async task with status spinner
    /// </summary>
    public static async Task<T> WithStatusAsync<T>(string status, Func<Task<T>> action)
    {
        if (IsInteractive)
        {
            return await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(status, async ctx => await action());
        }
        else
        {
            Console.WriteLine(status);
            return await action();
        }
    }

    /// <summary>
    /// Run with progress bar
    /// </summary>
    public static async Task WithProgressAsync(
        string description,
        int total,
        Func<Action<int>, Task> action)
    {
        if (IsInteractive)
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask(description, maxValue: total);
                    await action(increment => task.Increment(increment));
                });
        }
        else
        {
            var current = 0;
            var lastPct = -1;
            
            await action(increment =>
            {
                current += increment;
                var pct = (int)(current * 100.0 / total);
                if (pct != lastPct && pct % 10 == 0)
                {
                    Console.WriteLine($"{description}: {pct}%");
                    lastPct = pct;
                }
            });
        }
    }

    /// <summary>
    /// Print a success message
    /// </summary>
    public static void Success(string message)
    {
        if (IsInteractive)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] {message}");
        }
        else
        {
            Console.WriteLine($"✓ {message}");
        }
    }

    /// <summary>
    /// Print a warning message
    /// </summary>
    public static void Warning(string message)
    {
        if (IsInteractive)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");
        }
        else
        {
            Console.WriteLine($"⚠ {message}");
        }
    }

    /// <summary>
    /// Print an error message
    /// </summary>
    public static void Error(string message)
    {
        if (IsInteractive)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {message}");
        }
        else
        {
            Console.Error.WriteLine($"✗ {message}");
        }
    }

    /// <summary>
    /// Print info/dim message
    /// </summary>
    public static void Info(string message)
    {
        if (IsInteractive)
        {
            AnsiConsole.MarkupLine($"[dim]{message}[/]");
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Print a rule/divider
    /// </summary>
    public static void Rule(string title)
    {
        if (IsInteractive)
        {
            AnsiConsole.Write(new Rule($"[cyan]{title}[/]").LeftJustified());
        }
        else
        {
            Console.WriteLine($"=== {title} ===");
        }
    }
}
