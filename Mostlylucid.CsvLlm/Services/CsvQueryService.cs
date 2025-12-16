using System.Diagnostics;
using System.Text;
using DuckDB.NET.Data;
using Mostlylucid.CsvLlm.Models;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.CsvLlm.Services;

/// <summary>
/// Service for querying CSV files using natural language via a local LLM
/// </summary>
public class CsvQueryService : IDisposable
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly bool _verbose;

    /// <summary>
    /// Create a new CSV query service
    /// </summary>
    /// <param name="model">Ollama model to use (e.g., qwen2.5-coder:7b)</param>
    /// <param name="ollamaUrl">Ollama API URL</param>
    /// <param name="verbose">Print debug information</param>
    public CsvQueryService(
        string model = "qwen2.5-coder:7b",
        string ollamaUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _ollama = new OllamaApiClient(new Uri(ollamaUrl));
        _model = model;
        _verbose = verbose;
    }

    /// <summary>
    /// Query a CSV file using natural language
    /// </summary>
    /// <param name="csvPath">Path to the CSV file</param>
    /// <param name="question">Natural language question</param>
    /// <param name="maxRetries">Maximum number of retries on SQL error</param>
    /// <returns>Query result with data or error</returns>
    public async Task<QueryResult> QueryAsync(string csvPath, string question, int maxRetries = 2)
    {
        var stopwatch = Stopwatch.StartNew();

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        // Build context about the data
        var context = BuildContext(connection, csvPath);

        if (_verbose)
        {
            Console.WriteLine($"[Context] {context}");
        }

        // Generate SQL with retry logic
        string sql;
        try
        {
            sql = await GenerateSqlWithRetryAsync(connection, context, question, maxRetries);
        }
        catch (Exception ex)
        {
            return new QueryResult
            {
                Success = false,
                Error = ex.Message,
                ExecutionTime = stopwatch.Elapsed
            };
        }

        // Execute the query
        var result = ExecuteQuery(connection, sql);
        result.ExecutionTime = stopwatch.Elapsed;

        return result;
    }

    /// <summary>
    /// Get schema information about a CSV file
    /// </summary>
    public DataContext GetSchema(string csvPath)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        return BuildContext(connection, csvPath);
    }

    private DataContext BuildContext(DuckDBConnection connection, string csvPath)
    {
        var context = new DataContext { CsvPath = csvPath };

        // Get schema
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"DESCRIBE SELECT * FROM '{csvPath}'";
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

        // Get sample rows (helps LLM understand data format)
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM '{csvPath}' LIMIT 3";
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                    row[reader.GetName(i)] = value;
                }
                context.SampleRows.Add(row);
            }
        }

        // Get row count
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM '{csvPath}'";
            context.RowCount = Convert.ToInt64(cmd.ExecuteScalar());
        }

        return context;
    }

    private async Task<string> GenerateSqlWithRetryAsync(
        DuckDBConnection connection,
        DataContext context,
        string question,
        int maxRetries)
    {
        string? lastError = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var prompt = BuildPrompt(context, question, lastError);

            if (_verbose)
            {
                Console.WriteLine($"[Attempt {attempt + 1}] Generating SQL...");
            }

            var request = new GenerateRequest { Model = _model, Prompt = prompt };
            var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
            var sql = CleanSqlResponse(response?.Response ?? "");

            if (_verbose)
            {
                Console.WriteLine($"[SQL] {sql}");
            }

            // Validate SQL by explaining it (doesn't execute)
            var validationError = ValidateSql(connection, context.CsvPath, sql);
            if (validationError == null)
            {
                return sql;
            }

            if (_verbose)
            {
                Console.WriteLine($"[Error] {validationError}");
            }

            lastError = validationError;
        }

        throw new Exception($"Failed to generate valid SQL after {maxRetries + 1} attempts. Last error: {lastError}");
    }

    private string BuildPrompt(DataContext context, string question, string? previousError)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a SQL expert. Generate a DuckDB SQL query to answer the user's question.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT RULES:");
        sb.AppendLine("1. The table is accessed directly from the CSV file path");
        sb.AppendLine("2. Use single quotes around the file path in FROM clause");
        sb.AppendLine("3. DuckDB syntax - use LIMIT not TOP, use || for string concat");
        sb.AppendLine("4. Return ONLY the SQL query, no explanation, no markdown");
        sb.AppendLine();
        sb.AppendLine($"CSV File: '{context.CsvPath}'");
        sb.AppendLine($"Row Count: {context.RowCount:N0}");
        sb.AppendLine();
        sb.AppendLine("Schema:");
        foreach (var col in context.Columns)
        {
            sb.AppendLine($"  - {col.Name}: {col.Type}");
        }

        if (context.SampleRows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Sample data (first 3 rows):");
            foreach (var row in context.SampleRows)
            {
                var values = row.Select(kv => $"{kv.Key}='{kv.Value}'");
                sb.AppendLine($"  {{ {string.Join(", ", values)} }}");
            }
        }

        if (previousError != null)
        {
            sb.AppendLine();
            sb.AppendLine("YOUR PREVIOUS QUERY HAD AN ERROR:");
            sb.AppendLine(previousError);
            sb.AppendLine();
            sb.AppendLine("Please fix the query based on this error.");
        }

        sb.AppendLine();
        sb.AppendLine($"Question: {question}");
        sb.AppendLine();
        sb.AppendLine("SQL Query (no markdown, no explanation):");

        return sb.ToString();
    }

    private string CleanSqlResponse(string response)
    {
        var sql = response.Trim();

        // Remove markdown code blocks if present
        if (sql.StartsWith("```"))
        {
            var lines = sql.Split('\n').ToList();
            // Remove first line (```sql or ```)
            lines.RemoveAt(0);
            // Remove last line if it's closing ```
            if (lines.Count > 0 && lines[^1].Trim().StartsWith("```"))
            {
                lines.RemoveAt(lines.Count - 1);
            }
            sql = string.Join('\n', lines);
        }

        // Remove any leading/trailing backticks
        sql = sql.Trim('`', ' ', '\n', '\r');

        return sql;
    }

    private string? ValidateSql(DuckDBConnection connection, string csvPath, string sql)
    {
        try
        {
            // Use EXPLAIN to validate without executing
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"EXPLAIN {sql}";
            cmd.ExecuteNonQuery();
            return null; // Valid
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private QueryResult ExecuteQuery(DuckDBConnection connection, string sql)
    {
        var result = new QueryResult { Sql = sql };

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();

            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            // Get rows
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

    public void Dispose()
    {
        // OllamaApiClient doesn't need disposal
    }
}
