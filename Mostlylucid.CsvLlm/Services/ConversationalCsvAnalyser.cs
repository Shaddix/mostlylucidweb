using System.Text;
using DuckDB.NET.Data;
using Mostlylucid.CsvLlm.Models;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.CsvLlm.Services;

/// <summary>
/// Maintains conversation history for contextual follow-up questions about CSV data
/// </summary>
public class ConversationalCsvAnalyser : IDisposable
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly string _csvPath;
    private readonly DataContext _context;
    private readonly List<ConversationTurn> _history = new();
    private readonly bool _verbose;
    
    private DuckDBConnection? _connection;

    public IReadOnlyList<ConversationTurn> History => _history.AsReadOnly();

    /// <summary>
    /// Create a new conversational analyser for a specific CSV file
    /// </summary>
    public ConversationalCsvAnalyser(
        string csvPath,
        string model = "qwen2.5-coder:7b",
        string ollamaUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _csvPath = csvPath;
        _model = model;
        _verbose = verbose;
        _ollama = new OllamaApiClient(new Uri(ollamaUrl));
        
        // Open persistent connection and build context once
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
        _context = BuildContext();
    }

    /// <summary>
    /// Ask a question about the data (maintains conversation context)
    /// </summary>
    public async Task<QueryResult> AskAsync(string question, int maxRetries = 2)
    {
        if (_connection == null)
            throw new ObjectDisposedException(nameof(ConversationalCsvAnalyser));

        var prompt = BuildConversationalPrompt(question);

        if (_verbose)
        {
            Console.WriteLine($"[Prompt length: {prompt.Length} chars]");
        }

        string? lastError = null;
        string sql = "";

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var fullPrompt = lastError != null 
                ? prompt + $"\n\nPREVIOUS ERROR: {lastError}\nPlease fix and try again:"
                : prompt;

            var request = new GenerateRequest { Model = _model, Prompt = fullPrompt };
            var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
            sql = CleanSqlResponse(response?.Response ?? "");

            if (_verbose)
            {
                Console.WriteLine($"[Attempt {attempt + 1}] SQL: {sql}");
            }

            var validationError = ValidateSql(sql);
            if (validationError == null)
            {
                break;
            }

            lastError = validationError;
            if (attempt == maxRetries)
            {
                return new QueryResult
                {
                    Success = false,
                    Sql = sql,
                    Error = $"Failed after {maxRetries + 1} attempts: {lastError}"
                };
            }
        }

        // Execute and record in history
        var result = ExecuteQuery(sql);
        
        _history.Add(new ConversationTurn
        {
            Question = question,
            Sql = sql,
            Success = result.Success,
            RowCount = result.Rows.Count,
            Summary = SummarizeResult(result)
        });

        return result;
    }

    /// <summary>
    /// Clear conversation history (start fresh context)
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    /// <summary>
    /// Get the current schema context
    /// </summary>
    public DataContext GetContext() => _context;

    private DataContext BuildContext()
    {
        var context = new DataContext { CsvPath = _csvPath };

        using (var cmd = _connection!.CreateCommand())
        {
            cmd.CommandText = $"DESCRIBE SELECT * FROM '{_csvPath}'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                context.Columns.Add(new Models.ColumnInfo
                {
                    Name = reader.GetString(0),
                    Type = reader.GetString(1)
                });
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM '{_csvPath}' LIMIT 3";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                }
                context.SampleRows.Add(row);
            }
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM '{_csvPath}'";
            context.RowCount = Convert.ToInt64(cmd.ExecuteScalar());
        }

        return context;
    }

    private string BuildConversationalPrompt(string question)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a SQL expert. Generate a DuckDB SQL query to answer the user's question.");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("1. Query the CSV directly using the file path in single quotes");
        sb.AppendLine("2. DuckDB syntax: LIMIT not TOP, || for concat, ILIKE for case-insensitive");
        sb.AppendLine("3. Return ONLY the SQL query - no markdown, no explanation");
        sb.AppendLine();
        
        // Schema context
        sb.AppendLine($"CSV: '{_csvPath}' ({_context.RowCount:N0} rows)");
        sb.AppendLine("Schema:");
        foreach (var col in _context.Columns)
        {
            sb.AppendLine($"  {col.Name}: {col.Type}");
        }

        // Sample data
        if (_context.SampleRows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Sample rows:");
            foreach (var row in _context.SampleRows)
            {
                sb.AppendLine($"  {string.Join(", ", row.Select(kv => $"{kv.Key}='{kv.Value}'"))}");
            }
        }

        // Conversation history (last 5 turns for context)
        if (_history.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CONVERSATION HISTORY (for context):");
            var recentHistory = _history.TakeLast(5);
            foreach (var turn in recentHistory)
            {
                sb.AppendLine($"Q: {turn.Question}");
                sb.AppendLine($"SQL: {turn.Sql}");
                if (turn.Success)
                {
                    sb.AppendLine($"Result: {turn.Summary}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine($"NEW QUESTION: {question}");
        sb.AppendLine();
        sb.AppendLine("SQL:");

        return sb.ToString();
    }

    private string CleanSqlResponse(string response)
    {
        var sql = response.Trim();

        if (sql.StartsWith("```"))
        {
            var lines = sql.Split('\n').ToList();
            lines.RemoveAt(0);
            if (lines.Count > 0 && lines[^1].Trim().StartsWith("```"))
            {
                lines.RemoveAt(lines.Count - 1);
            }
            sql = string.Join('\n', lines);
        }

        return sql.Trim('`', ' ', '\n', '\r');
    }

    private string? ValidateSql(string sql)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"EXPLAIN {sql}";
            cmd.ExecuteNonQuery();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private QueryResult ExecuteQuery(string sql)
    {
        var result = new QueryResult { Sql = sql };

        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            while (reader.Read())
            {
                var row = new List<object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                result.Rows.Add(row);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private string SummarizeResult(QueryResult result)
    {
        if (!result.Success) return "Error";
        if (result.Rows.Count == 0) return "No rows returned";
        if (result.Rows.Count == 1 && result.Columns.Count == 1)
        {
            return $"Single value: {result.Rows[0][0]}";
        }
        return $"{result.Rows.Count} rows, {result.Columns.Count} columns";
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}

/// <summary>
/// A single turn in the conversation
/// </summary>
public class ConversationTurn
{
    public string Question { get; set; } = "";
    public string Sql { get; set; } = "";
    public bool Success { get; set; }
    public int RowCount { get; set; }
    public string Summary { get; set; } = "";
}
