using System.Text;

namespace Mostlylucid.Summarizer.Shared.Services;

/// <summary>
/// Shared markdown report generation utilities.
/// </summary>
public static class ReportGenerator
{
    /// <summary>
    /// Create a markdown table from data
    /// </summary>
    public static string CreateTable(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        var headerList = headers.ToList();
        
        // Header row
        sb.AppendLine($"| {string.Join(" | ", headerList)} |");
        
        // Separator
        sb.AppendLine($"| {string.Join(" | ", headerList.Select(_ => "---"))} |");
        
        // Data rows
        foreach (var row in rows)
        {
            sb.AppendLine($"| {string.Join(" | ", row)} |");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Create a summary box
    /// </summary>
    public static string CreateSummaryBox(string title, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"> **{title}**");
        sb.AppendLine(">");
        foreach (var line in content.Split('\n'))
        {
            sb.AppendLine($"> {line}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Create a collapsible section (GitHub markdown)
    /// </summary>
    public static string CreateCollapsible(string summary, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<details>");
        sb.AppendLine($"<summary>{summary}</summary>");
        sb.AppendLine();
        sb.AppendLine(content);
        sb.AppendLine();
        sb.AppendLine("</details>");
        return sb.ToString();
    }

    /// <summary>
    /// Escape markdown special characters
    /// </summary>
    public static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        return text
            .Replace("|", "\\|")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("`", "\\`")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }

    /// <summary>
    /// Truncate text with ellipsis
    /// </summary>
    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Format a number for display
    /// </summary>
    public static string FormatNumber(double value, int decimals = 1)
    {
        var format = $"F{decimals}";
        if (Math.Abs(value) >= 1_000_000)
            return (value / 1_000_000).ToString(format) + "M";
        if (Math.Abs(value) >= 1_000)
            return (value / 1_000).ToString(format) + "K";
        return value.ToString(format);
    }

    /// <summary>
    /// Format duration
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1}h";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1}m";
        return $"{duration.TotalSeconds:F1}s";
    }
}
