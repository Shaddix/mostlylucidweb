using System.Text;
using System.Text.RegularExpressions;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
///     Formats fetch metadata summaries using template strings
/// </summary>
public partial class FetchMetadataSummary
{
    private const string DefaultTemplate = "_Content fetched from [{url}]({url}) on {retrieved:dd MMM yyyy} ({age})_";
    private const string DefaultCssClass = "ft_summary";

    [GeneratedRegex(@"\{([^:}]+)(?::([^}]+))?\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    ///     Formats a fetch result using the provided template and wraps in a div with CSS class
    /// </summary>
    /// <param name="result">The fetch result with metadata</param>
    /// <param name="template">Optional template string with placeholders</param>
    /// <param name="cssClass">Optional CSS class for the wrapper div (default: "ft_summary")</param>
    public static string Format(MarkdownFetchResult result, string? template = null, string? cssClass = null)
    {
        if (!result.Success || result.LastRetrieved == null)
            return string.Empty;

        var templateToUse = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        var cssClassToUse = string.IsNullOrWhiteSpace(cssClass) ? DefaultCssClass : cssClass;

        var content = PlaceholderRegex().Replace(templateToUse, match =>
        {
            var key = match.Groups[1].Value.ToLowerInvariant();
            var format = match.Groups.Count > 2 && match.Groups[2].Success ? match.Groups[2].Value : null;

            return key switch
            {
                "retrieved" => FormatDateTime(result.LastRetrieved.Value, format),
                "age" => FormatAge(result.LastRetrieved.Value),
                "url" => result.SourceUrl ?? string.Empty,
                "nextrefresh" => FormatNextRefresh(result.LastRetrieved.Value, result.PollFrequencyHours, format),
                "pollfrequency" => result.PollFrequencyHours.ToString(),
                "status" => FormatStatus(result),
                _ => match.Value // Keep original if unknown placeholder
            };
        });

        // Wrap in div with CSS class
        return $"<div class=\"{cssClassToUse}\">{content}</div>";
    }

    private static string FormatDateTime(DateTime dateTime, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return dateTime.ToString("yyyy-MM-dd HH:mm");

        // Handle special format shortcuts
        return format.ToLowerInvariant() switch
        {
            "relative" => FormatAge(dateTime),
            "short" => dateTime.ToString("dd MMM yyyy"),
            "long" => dateTime.ToString("dd MMMM yyyy HH:mm"),
            "time" => dateTime.ToString("HH:mm"),
            "date" => dateTime.ToString("yyyy-MM-dd"),
            "day" => dateTime.Day.ToString(),
            "month" => dateTime.Month.ToString(),
            "month-text" => dateTime.ToString("MMMM"),
            "month-short" => dateTime.ToString("MMM"),
            "year" => dateTime.Year.ToString(),
            "iso" => dateTime.ToString("o"),
            _ => dateTime.ToString(format) // Use as standard .NET format string
        };
    }

    private static string FormatAge(DateTime dateTime)
    {
        var age = DateTime.UtcNow - dateTime;

        if (age.TotalMinutes < 1)
            return "just now";
        if (age.TotalMinutes < 60)
            return $"{(int)age.TotalMinutes} minute{((int)age.TotalMinutes != 1 ? "s" : "")} ago";
        if (age.TotalHours < 24)
            return $"{(int)age.TotalHours} hour{((int)age.TotalHours != 1 ? "s" : "")} ago";
        if (age.TotalDays < 30)
            return $"{(int)age.TotalDays} day{((int)age.TotalDays != 1 ? "s" : "")} ago";
        if (age.TotalDays < 365)
        {
            var months = (int)(age.TotalDays / 30);
            return $"{months} month{(months != 1 ? "s" : "")} ago";
        }

        var years = (int)(age.TotalDays / 365);
        return $"{years} year{(years != 1 ? "s" : "")} ago";
    }

    private static string FormatNextRefresh(DateTime lastRetrieved, int pollFrequencyHours, string? format)
    {
        if (pollFrequencyHours == 0)
            return "always fresh";

        var nextRefresh = lastRetrieved.AddHours(pollFrequencyHours);
        var timeUntil = nextRefresh - DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(format) || format == "relative")
        {
            if (timeUntil.TotalMinutes < 0)
                return "due for refresh";
            if (timeUntil.TotalMinutes < 60)
                return $"in {(int)timeUntil.TotalMinutes} minute{((int)timeUntil.TotalMinutes != 1 ? "s" : "")}";
            if (timeUntil.TotalHours < 24)
                return $"in {(int)timeUntil.TotalHours} hour{((int)timeUntil.TotalHours != 1 ? "s" : "")}";

            return $"in {(int)timeUntil.TotalDays} day{((int)timeUntil.TotalDays != 1 ? "s" : "")}";
        }

        return FormatDateTime(nextRefresh, format);
    }

    private static string FormatStatus(MarkdownFetchResult result)
    {
        if (!result.IsCached)
            return "fresh";
        if (result.IsStale)
            return "stale";

        return "cached";
    }
}
