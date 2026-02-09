using Spectre.Console;

namespace Mostlylucid.OcrNer.CLI.Commands;

/// <summary>
/// Safe Spectre progress bar wrapper.
/// If Spectre markup parsing fails (e.g. file names with brackets like "[scan].png"),
/// the error is caught and the command continues gracefully.
/// </summary>
internal static class ProgressHelper
{
    public static async Task RunAsync(Func<ProgressContext, Task> action)
    {
        try
        {
            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(action);
        }
        catch (Exception ex)
        {
            // Walk the exception chain looking for Spectre markup errors
            var current = ex;
            while (current != null)
            {
                if (current.Message.Contains("color or style") ||
                    current.Message.Contains("markup"))
                    return; // Cosmetic error — swallow it

                current = current.InnerException;
            }

            throw; // Not a markup error — rethrow
        }
    }
}
