using System.Text;

namespace Mostlylucid.CsvLlm.Models;

/// <summary>
/// Result of a CSV query operation
/// </summary>
public class QueryResult
{
    public bool Success { get; set; }
    public string Sql { get; set; } = "";
    public string? Error { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Format the result as a readable table
    /// </summary>
    public override string ToString()
    {
        if (!Success)
            return $"Error: {Error}\n\nGenerated SQL:\n{Sql}";

        var sb = new StringBuilder();
        
        // Calculate column widths
        var widths = new int[Columns.Count];
        for (int i = 0; i < Columns.Count; i++)
        {
            widths[i] = Columns[i].Length;
        }
        
        foreach (var row in Rows)
        {
            for (int i = 0; i < row.Count && i < widths.Length; i++)
            {
                var len = (row[i]?.ToString() ?? "NULL").Length;
                if (len > widths[i]) widths[i] = Math.Min(len, 50); // Cap at 50 chars
            }
        }

        // Header
        for (int i = 0; i < Columns.Count; i++)
        {
            sb.Append(Columns[i].PadRight(widths[i] + 2));
        }
        sb.AppendLine();
        
        // Separator
        for (int i = 0; i < Columns.Count; i++)
        {
            sb.Append(new string('-', widths[i]));
            sb.Append("  ");
        }
        sb.AppendLine();

        // Rows
        foreach (var row in Rows)
        {
            for (int i = 0; i < row.Count && i < widths.Length; i++)
            {
                var value = row[i]?.ToString() ?? "NULL";
                if (value.Length > 50) value = value[..47] + "...";
                sb.Append(value.PadRight(widths[i] + 2));
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine($"({Rows.Count} rows, {ExecutionTime.TotalMilliseconds:F1}ms)");

        return sb.ToString();
    }

    /// <summary>
    /// Convert result to CSV format
    /// </summary>
    public string ToCsv()
    {
        if (!Success) return "";

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns.Select(c => $"\"{c}\"")));
        
        foreach (var row in Rows)
        {
            var values = row.Select(v => 
            {
                var str = v?.ToString() ?? "";
                // Escape quotes and wrap in quotes if needed
                if (str.Contains(',') || str.Contains('"') || str.Contains('\n'))
                {
                    str = "\"" + str.Replace("\"", "\"\"") + "\"";
                }
                return str;
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }
}
