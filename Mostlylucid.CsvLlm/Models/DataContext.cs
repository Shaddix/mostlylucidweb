namespace Mostlylucid.CsvLlm.Models;

/// <summary>
/// Contains metadata about the CSV file for LLM context
/// </summary>
public class DataContext
{
    public string CsvPath { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();
    public long RowCount { get; set; }
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"File: {CsvPath} ({RowCount:N0} rows)");
        sb.AppendLine("Columns:");
        foreach (var col in Columns)
        {
            sb.AppendLine($"  - {col.Name}: {col.Type}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Information about a single column in the CSV
/// </summary>
public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}
