using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using DuckDB.NET.Data;
using Mostlylucid.DataSummarizer.Models;
using OllamaSharp;
using OllamaSharp.Models;

namespace Mostlylucid.DataSummarizer.Services;

/// <summary>
/// Uses LLM to generate analytical queries based on statistical profile.
/// The profile grounds the LLM - preventing hallucination about column names/types.
/// </summary>
public class LlmInsightGenerator : IDisposable
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    private readonly bool _verbose;
    private DuckDBConnection? _connection;

    public LlmInsightGenerator(
        string model = "qwen2.5-coder:7b",
        string ollamaUrl = "http://localhost:11434",
        bool verbose = false)
    {
        _ollama = new OllamaApiClient(new Uri(ollamaUrl));
        _model = model;
        _verbose = verbose;
    }

    /// <summary>
    /// Generate LLM-powered insights using the statistical profile as grounding context
    /// </summary>
    public async Task<List<DataInsight>> GenerateInsightsAsync(
        string filePath, 
        DataProfile profile,
        int maxInsights = 5)
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        
        // Install extensions if needed
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls")
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSTALL excel; LOAD excel;";
            await cmd.ExecuteNonQueryAsync();
        }

        var readExpr = GetReadExpression(filePath, profile.SheetName);
        var insights = new List<DataInsight>();

        // Step 1: Ask LLM to generate analytical questions based on the profile
        var questions = await GenerateQuestionsAsync(profile);
        
        if (_verbose)
        {
            Console.WriteLine($"[LLM] Generated {questions.Count} analytical questions");
        }

        // Step 2: For each question, generate and execute SQL
        foreach (var question in questions.Take(maxInsights))
        {
            try
            {
                var insight = await GenerateAndExecuteInsightAsync(readExpr, profile, question);
                if (insight != null)
                {
                    insights.Add(insight);
                }
            }
            catch (Exception ex)
            {
                if (_verbose) Console.WriteLine($"[LLM] Failed: {question} - {ex.Message}");
            }
        }

        return insights;
    }

    /// <summary>
    /// Ask a specific question about the data
    /// </summary>
    public async Task<DataInsight?> AskAsync(string filePath, DataProfile profile, string question, string conversationContext = "")
    {
        // For broad descriptive questions, return an LLM summary without SQL
        if (IsBroadSummaryQuestion(question))
        {
            return await GenerateProfileSummaryAsync(profile, question, conversationContext);
        }

        _connection = new DuckDBConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls")
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSTALL excel; LOAD excel;";
            await cmd.ExecuteNonQueryAsync();
        }

        var readExpr = GetReadExpression(filePath, profile.SheetName);
        return await GenerateAndExecuteInsightAsync(readExpr, profile, question);
    }

    private static bool IsBroadSummaryQuestion(string question)
    {
        var q = question.ToLowerInvariant();
        return q.Contains("tell me about") || q.Contains("overview") || q.Contains("summarize") || q.Contains("summary") || q.Contains("what is in this data");
    }

    private async Task<DataInsight> GenerateProfileSummaryAsync(DataProfile profile, string question, string conversationContext)
    {
        var prompt = BuildProfileSummaryPrompt(profile, question, conversationContext);
        var req = new GenerateRequest { Model = _model, Prompt = prompt };
        var resp = await _ollama.GenerateAsync(req).StreamToEndAsync();
        var text = (resp?.Response ?? "").Trim();

        return new DataInsight
        {
            Title = "Dataset summary",
            Description = text,
            Source = InsightSource.LlmGenerated,
            RelatedColumns = profile.Columns.Select(c => c.Name).Take(10).ToList()
        };
    }

    private string BuildProfileSummaryPrompt(DataProfile profile, string question, string conversationContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a precise data analyst. Give a short, factual summary of the dataset using ONLY the provided profile. Do not speculate or invent columns. Keep it to 3-5 sentences.");
        sb.AppendLine();
        sb.AppendLine($"Question: {question}");
        if (!string.IsNullOrWhiteSpace(conversationContext))
        {
            sb.AppendLine();
            sb.AppendLine("Prior conversation (for continuity, do not invent new facts):");
            sb.AppendLine(conversationContext);
        }
        sb.AppendLine();
        sb.AppendLine($"Rows: {profile.RowCount:N0}, Columns: {profile.ColumnCount}");
        sb.AppendLine("Column types:");
        sb.AppendLine($"- Numeric: {profile.Columns.Count(c => c.InferredType == ColumnType.Numeric)}");
        sb.AppendLine($"- Categorical: {profile.Columns.Count(c => c.InferredType == ColumnType.Categorical)}");
        sb.AppendLine($"- Date/Time: {profile.Columns.Count(c => c.InferredType == ColumnType.DateTime)}");
        sb.AppendLine();
        sb.AppendLine("Columns:");
        foreach (var col in profile.Columns.Take(12))
        {
            var parts = new List<string> { col.InferredType.ToString() };
            if (col.Mean.HasValue) parts.Add($"mean {col.Mean:F1}");
            if (col.StdDev.HasValue) parts.Add($"std {col.StdDev:F1}");
            if (col.Mad.HasValue) parts.Add($"mad {col.Mad:F1}");
            if (col.Min.HasValue && col.Max.HasValue) parts.Add($"range {col.Min:F1}-{col.Max:F1}");
            if (col.Skewness.HasValue) parts.Add($"skew {col.Skewness:F2}");
            if (col.OutlierCount > 0) parts.Add($"outliers {col.OutlierCount}");
            if (col.TopValues?.Count > 0) parts.Add($"top {col.TopValues[0].Value}");
            if (col.TextPatterns.Count > 0) parts.Add($"pattern {col.TextPatterns[0].PatternType}");
            if (col.Distribution.HasValue && col.Distribution != DistributionType.Unknown) parts.Add($"dist {col.Distribution}");
            if (col.Trend?.Direction is TrendDirection.Increasing or TrendDirection.Decreasing) parts.Add($"trend {col.Trend.Direction} (R2={col.Trend.RSquared:F2})");
            if (col.TimeSeries != null) parts.Add($"ts {col.TimeSeries.Granularity}");
            sb.AppendLine($"- {col.Name}: {string.Join(", ", parts)}");
        }
        sb.AppendLine();
        if (profile.Correlations.Count > 0)
        {
            sb.AppendLine("Correlations (|r|>=0.3):");
            foreach (var corr in profile.Correlations.Take(5))
                sb.AppendLine($"- {corr.Column1} ↔ {corr.Column2}: {corr.Correlation:F2}");
        }
        if (profile.Alerts.Count > 0)
        {
            sb.AppendLine("Alerts:");
            foreach (var alert in profile.Alerts.Take(5))
                sb.AppendLine($"- {alert.Column}: {alert.Message}");
        }
        if (profile.Patterns.Count > 0)
        {
            sb.AppendLine("Patterns:");
            foreach (var p in profile.Patterns.Take(5))
                sb.AppendLine($"- {p.Type}: {p.Description}");
        }
        return sb.ToString();
    }

    public class ReportNarrative
    {
        public string Summary { get; set; } = string.Empty;
        public Dictionary<string, string> FocusAnswers { get; set; } = new();
    }

    public async Task<ReportNarrative> GenerateReportNarrativeAsync(DataProfile profile, IReadOnlyCollection<string>? focusQuestions)
    {
        var prompt = BuildNarrativePrompt(profile, focusQuestions ?? Array.Empty<string>());
        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
        var text = (response?.Response ?? string.Empty).Trim();
        return ParseNarrative(text, focusQuestions ?? Array.Empty<string>());
    }

    private string BuildNarrativePrompt(DataProfile profile, IReadOnlyCollection<string> focusQuestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior data analyst. Using only the profile below, write a concise summary (3 sentences) and answer each focus question directly.");
        sb.AppendLine("Do NOT invent columns or values. Ground every statement in the stats provided.");
        sb.AppendLine();
        sb.AppendLine("Return valid JSON with this schema:");
        sb.AppendLine("{ \"summary\": \"...\", \"focus\": [ { \"question\": \"...\", \"answer\": \"...\" } ] }");
        sb.AppendLine();
        sb.AppendLine($"Rows: {profile.RowCount:N0}, Columns: {profile.ColumnCount}");
        sb.AppendLine("Columns:");
        foreach (var col in profile.Columns.Take(15))
        {
            var parts = new List<string> { col.InferredType.ToString() };
            if (col.Mean.HasValue) parts.Add($"mean {col.Mean:F1}");
            if (col.StdDev.HasValue) parts.Add($"std {col.StdDev:F1}");
            if (col.Min.HasValue && col.Max.HasValue) parts.Add($"range {col.Min:F1}-{col.Max:F1}");
            if (col.NullPercent > 0) parts.Add($"nulls {col.NullPercent:F1}%");
            if (col.TopValues?.Count > 0) parts.Add($"top {col.TopValues[0].Value}");
            sb.AppendLine($"- {col.Name}: {string.Join(", ", parts)}");
        }
        if (profile.Target != null)
        {
            sb.AppendLine();
            sb.AppendLine("Target distribution:");
            foreach (var kvp in profile.Target.ClassDistribution)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value:P1}");
            }
        }
        if (focusQuestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("FocusQuestions:");
            foreach (var question in focusQuestions)
            {
                sb.AppendLine($"- {question}");
            }
        }
        return sb.ToString();
    }

    private ReportNarrative ParseNarrative(string rawResponse, IReadOnlyCollection<string> focusQuestions)
    {
        var narrative = new ReportNarrative();
        var json = ExtractJsonBlock(rawResponse);
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("summary", out var summary))
                {
                    narrative.Summary = summary.GetString() ?? string.Empty;
                }
                if (doc.RootElement.TryGetProperty("focus", out var focusArray) && focusArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in focusArray.EnumerateArray())
                    {
                        var question = item.TryGetProperty("question", out var qEl) ? qEl.GetString() : null;
                        var answer = item.TryGetProperty("answer", out var aEl) ? aEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                        {
                            narrative.FocusAnswers[question!] = answer!;
                        }
                    }
                }
            }
            catch
            {
                narrative.Summary = rawResponse;
            }
        }
        else
        {
            narrative.Summary = rawResponse;
        }

        if (focusQuestions.Count > 0 && narrative.FocusAnswers.Count == 0)
        {
            foreach (var question in focusQuestions)
            {
                narrative.FocusAnswers[question] = "Not enough information to answer without LLM output.";
            }
        }

        return narrative;
    }

    private static string? ExtractJsonBlock(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return response[start..(end + 1)];
        }
        return null;
    }

    private async Task<List<string>> GenerateQuestionsAsync(DataProfile profile)
    {
        var prompt = BuildQuestionGenerationPrompt(profile);
        
        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
        
        // Parse questions from response (one per line)
        var questions = (response?.Response ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(q => q.Trim().TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '))
            .Where(q => q.Length > 10 && !q.StartsWith("```"))
            .Take(10)
            .ToList();

        return questions;
    }

    private string BuildQuestionGenerationPrompt(DataProfile profile)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a data analyst. Based on the dataset profile below, generate 5-7 insightful analytical questions that would reveal interesting patterns or business insights.");
        sb.AppendLine();
        sb.AppendLine("DATASET PROFILE:");
        sb.AppendLine($"- Rows: {profile.RowCount:N0}");
        sb.AppendLine($"- Source: {profile.SourcePath}");
        sb.AppendLine();
        
        sb.AppendLine("COLUMNS:");
        foreach (var col in profile.Columns)
        {
            sb.Append($"  - {col.Name} ({col.InferredType})");
            
            if (col.InferredType == ColumnType.Numeric && col.Mean.HasValue)
            {
                var madText = col.Mad.HasValue ? $", MAD: {col.Mad:F1}" : "";
                var skewText = col.Skewness.HasValue ? $", skew: {col.Skewness:F2}" : "";
                sb.Append($" [range: {col.Min:F0}-{col.Max:F0}, avg: {col.Mean:F1}, std: {col.StdDev:F1}{madText}{skewText}]");
            }
            else if (col.InferredType == ColumnType.Categorical && col.TopValues?.Count > 0)
            {
                var topVals = string.Join(", ", col.TopValues.Take(3).Select(v => v.Value));
                sb.Append($" [values: {topVals}...]");
            }
            else if (col.InferredType == ColumnType.DateTime)
            {
                sb.Append($" [range: {col.MinDate:yyyy-MM-dd} to {col.MaxDate:yyyy-MM-dd}]");
            }
            
            if (col.NullPercent > 5)
            {
                sb.Append($" ({col.NullPercent:F0}% null)");
            }
            
            sb.AppendLine();
        }

        // Include alerts as hints
        if (profile.Alerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DATA QUALITY NOTES:");
            foreach (var alert in profile.Alerts.Take(5))
            {
                sb.AppendLine($"  - {alert.Column}: {alert.Message}");
            }
        }

        // Include correlations as hints
        if (profile.Correlations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CORRELATIONS DETECTED:");
            foreach (var corr in profile.Correlations.Take(3))
            {
                sb.AppendLine($"  - {corr.Column1} ↔ {corr.Column2}: {corr.Correlation:F2} ({corr.Strength})");
            }
        }

        // Alerts overview
        if (profile.Alerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DATA QUALITY:");
            var warn = profile.Alerts.Count(a => a.Severity == AlertSeverity.Warning);
            var info = profile.Alerts.Count(a => a.Severity == AlertSeverity.Info);
            sb.AppendLine($"  - {profile.Alerts.Count} alerts (warnings: {warn}, info: {info})");
            foreach (var alert in profile.Alerts.Take(3))
            {
                sb.AppendLine($"    * {alert.Column}: {alert.Message}");
            }
        }

        // Include detected patterns
        AppendPatternContext(sb, profile);

        sb.AppendLine();
        sb.AppendLine("Generate 5-7 analytical questions. Focus on:");
        sb.AppendLine("- Trends and patterns");
        sb.AppendLine("- Comparisons between categories");
        sb.AppendLine("- Distributions and outliers");
        sb.AppendLine("- Relationships between columns");
        sb.AppendLine("- Aggregations that reveal business insights");
        
        if (profile.Columns.Any(c => c.TimeSeries != null))
            sb.AppendLine("- Time-based analysis and seasonality");
        
        if (profile.Columns.Any(c => c.Trend != null))
            sb.AppendLine("- Growth/decline rates and projections");
            
        sb.AppendLine();
        sb.AppendLine("Return ONLY the questions, one per line, no numbering:");

        return sb.ToString();
    }

    private static void AppendPatternContext(StringBuilder sb, DataProfile profile)
    {
        var hasPatterns = false;

        // Time series info
        var dateCol = profile.Columns.FirstOrDefault(c => c.TimeSeries != null);
        if (dateCol?.TimeSeries != null)
        {
            if (!hasPatterns)
            {
                sb.AppendLine();
                sb.AppendLine("DETECTED PATTERNS:");
                hasPatterns = true;
            }
            
            var ts = dateCol.TimeSeries;
            sb.Append($"  - TIME SERIES: {ts.Granularity} data indexed by '{dateCol.Name}'");
            if (!ts.IsContiguous)
                sb.Append($" ({ts.GapCount} gaps)");
            if (ts.HasSeasonality)
                sb.Append($" [seasonal period: {ts.SeasonalPeriod}]");
            sb.AppendLine();
        }

        // Distribution info
        var distributedCols = profile.Columns
            .Where(c => c.Distribution.HasValue && c.Distribution != DistributionType.Unknown)
            .Take(3)
            .ToList();
        
        if (distributedCols.Any())
        {
            if (!hasPatterns)
            {
                sb.AppendLine();
                sb.AppendLine("DETECTED PATTERNS:");
                hasPatterns = true;
            }
            
            foreach (var col in distributedCols)
            {
                sb.AppendLine($"  - DISTRIBUTION: '{col.Name}' is {col.Distribution}");
            }
        }

        // Trend info
        var trendCols = profile.Columns
            .Where(c => c.Trend != null && c.Trend.Direction != TrendDirection.None)
            .Take(3)
            .ToList();
        
        if (trendCols.Any())
        {
            if (!hasPatterns)
            {
                sb.AppendLine();
                sb.AppendLine("DETECTED PATTERNS:");
                hasPatterns = true;
            }
            
            foreach (var col in trendCols)
            {
                var t = col.Trend!;
                sb.AppendLine($"  - TREND: '{col.Name}' is {t.Direction} (R²={t.RSquared:F2})");
            }
        }

        // Text patterns
        var textPatternCols = profile.Columns
            .Where(c => c.TextPatterns.Count > 0)
            .Take(3)
            .ToList();
        
        if (textPatternCols.Any())
        {
            if (!hasPatterns)
            {
                sb.AppendLine();
                sb.AppendLine("DETECTED PATTERNS:");
                hasPatterns = true;
            }
            
            foreach (var col in textPatternCols)
            {
                var pattern = col.TextPatterns.First();
                sb.AppendLine($"  - TEXT FORMAT: '{col.Name}' contains {pattern.PatternType} values ({pattern.MatchPercent:F0}%)");
            }
        }

        // Dataset-level patterns
        if (profile.Patterns.Count > 0)
        {
            if (!hasPatterns)
            {
                sb.AppendLine();
                sb.AppendLine("DETECTED PATTERNS:");
            }
            
            foreach (var pattern in profile.Patterns.Take(3))
            {
                sb.AppendLine($"  - {pattern.Type}: {pattern.Description}");
            }
        }
    }

    private async Task<DataInsight?> GenerateAndExecuteInsightAsync(
        string readExpr, 
        DataProfile profile, 
        string question)
    {
        // Generate SQL using profile as grounding
        var sql = await GenerateSqlAsync(readExpr, profile, question);
        
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        if (_verbose)
        {
            Console.WriteLine($"[LLM] Q: {question}");
            Console.WriteLine($"[LLM] SQL: {sql}");
        }

        // Validate SQL
        if (!await ValidateSqlAsync(sql))
        {
            // Retry once with error feedback
            sql = await GenerateSqlAsync(readExpr, profile, question, "Previous SQL was invalid");
            if (string.IsNullOrWhiteSpace(sql) || !await ValidateSqlAsync(sql))
                return null;
        }

        // Execute and format result
        var result = await ExecuteQueryAsync(sql);
        
        if (result == null)
            return null;

        // Generate natural language summary of result
        var summary = await SummarizeResultAsync(question, result);

        return new DataInsight
        {
            Title = TruncateQuestion(question),
            Description = summary,
            Sql = sql,
            Result = result,
            Source = InsightSource.LlmGenerated,
            RelatedColumns = ExtractColumnNames(sql, profile)
        };
    }

    private async Task<string> GenerateSqlAsync(
        string readExpr, 
        DataProfile profile, 
        string question,
        string? errorHint = null)
    {
        var prompt = BuildSqlGenerationPrompt(readExpr, profile, question, errorHint);
        
        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
        
        return CleanSqlResponse(response?.Response ?? "");
    }

    private string BuildSqlGenerationPrompt(
        string readExpr, 
        DataProfile profile, 
        string question,
        string? errorHint)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Generate a DuckDB SQL query to answer the question below.");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("1. Use the exact table expression provided (don't modify the FROM clause)");
        sb.AppendLine("2. Use double quotes around column names");
        sb.AppendLine("3. DuckDB syntax: LIMIT not TOP, || for concat, ILIKE for case-insensitive");
        sb.AppendLine("4. Return ONLY the SQL - no markdown, no explanation");
        sb.AppendLine("5. Limit results to 20 rows max");
        sb.AppendLine();
        
        sb.AppendLine($"TABLE: {readExpr}");
        sb.AppendLine();
        
        sb.AppendLine("SCHEMA (use these exact column names):");
        foreach (var col in profile.Columns)
        {
            sb.AppendLine($"  \"{col.Name}\" {col.DuckDbType} -- {col.InferredType}");
        }

        if (errorHint != null)
        {
            sb.AppendLine();
            sb.AppendLine($"ERROR FROM PREVIOUS ATTEMPT: {errorHint}");
            sb.AppendLine("Please fix the query.");
        }

        sb.AppendLine();
        sb.AppendLine($"QUESTION: {question}");
        sb.AppendLine();
        sb.AppendLine("SQL:");

        return sb.ToString();
    }

    private async Task<bool> ValidateSqlAsync(string sql)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = $"EXPLAIN {sql}";
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<object?> ExecuteQueryAsync(string sql)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync() && rows.Count < 20)
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return new { columns, rows, rowCount = rows.Count };
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> SummarizeResultAsync(string question, object result)
    {
        var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // Truncate if too long
        if (resultJson.Length > 2000)
        {
            resultJson = resultJson[..2000] + "...";
        }

        var prompt = $"""
            Summarize this query result in 1-2 sentences. Be specific with numbers.
            
            Question: {question}
            Result: {resultJson}
            
            Summary (1-2 sentences, include key numbers):
            """;

        var request = new GenerateRequest { Model = _model, Prompt = prompt };
        var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
        
        return (response?.Response ?? "").Trim();
    }

    private static string CleanSqlResponse(string response)
    {
        var sql = response.Trim();

        // Remove markdown code blocks
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

    private static string TruncateQuestion(string question)
    {
        if (question.Length <= 60) return question;
        return question[..57] + "...";
    }

    private static List<string> ExtractColumnNames(string sql, DataProfile profile)
    {
        var colNames = profile.Columns.Select(c => c.Name).ToList();
        return colNames.Where(c => sql.Contains($"\"{c}\"", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string GetReadExpression(string filePath, string? sheetName)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var escaped = filePath.Replace("'", "''").Replace("\\", "/");
        
        return ext switch
        {
            ".xlsx" or ".xls" when sheetName != null => 
                $"read_xlsx('{escaped}', sheet = '{sheetName}', header = true)",
            ".xlsx" or ".xls" => 
                $"read_xlsx('{escaped}', header = true)",
            ".parquet" => 
                $"read_parquet('{escaped}')",
            ".json" => 
                $"read_json_auto('{escaped}')",
            _ => 
                $"read_csv_auto('{escaped}')"
        };
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
