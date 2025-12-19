using System.Globalization;
using System.Text.Json;
using DuckDB.NET.Data;
using Mostlylucid.DataSummarizer.Models;

namespace Mostlylucid.DataSummarizer.Services;

/// <summary>
/// Simple DuckDB-based vector store using the vss extension.
/// Persists profiles, summaries, and embeddings for reuse across sessions.
/// </summary>
public class VectorStoreService : IDisposable
{
    private readonly string _dbPath;
    private readonly bool _verbose;
    private DuckDBConnection? _conn;
    private bool _available;
    private bool _useVss;

    public bool IsAvailable => _available;
    internal DuckDBConnection? Connection => _conn;

    public VectorStoreService(string dbPath, bool verbose = false)
    {
        _dbPath = dbPath;
        _verbose = verbose;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _conn = new DuckDBConnection($"Data Source={_dbPath}");
            await _conn.OpenAsync();

            // Try VSS; if fails, fall back to manual similarity
            try
            {
                await ExecAsync("INSTALL vss; LOAD vss;");
                _useVss = true;
            }
            catch (Exception ex)
            {
                _useVss = false;
                if (_verbose) Console.WriteLine($"[VectorStore] VSS unavailable, falling back to in-process similarity: {ex.Message}");
            }

            // Tables
        await ExecAsync(@"
            CREATE TABLE IF NOT EXISTS registry_files (
                file_path TEXT PRIMARY KEY,
                row_count BIGINT,
                column_count INTEGER,
                profile_json TEXT,
                updated_at TIMESTAMP DEFAULT NOW()
            );
        ");

        await ExecAsync(@"
            CREATE TABLE IF NOT EXISTS registry_embeddings (
                id BIGINT PRIMARY KEY,
                file_path TEXT,
                label TEXT,
                kind TEXT,
                metadata TEXT,
                embedding FLOAT[128],
                embedding_json TEXT
            );
        ");

        await ExecAsync(@"
            CREATE TABLE IF NOT EXISTS registry_conversations (
                session_id TEXT,
                turn_id BIGINT,
                role TEXT,
                content TEXT,
                embedding FLOAT[128],
                embedding_json TEXT,
                created_at TIMESTAMP DEFAULT NOW(),
                PRIMARY KEY (session_id, turn_id)
            );
        ");

        await ExecAsync(@"CREATE SEQUENCE IF NOT EXISTS registry_embeddings_seq;");
        await ExecAsync(@"CREATE SEQUENCE IF NOT EXISTS registry_conversations_seq;");
        if (_useVss)
        {
            try
            {
                await ExecAsync(@"CREATE INDEX IF NOT EXISTS idx_registry_embeddings_vss ON registry_embeddings USING vss(embedding);");
                await ExecAsync(@"CREATE INDEX IF NOT EXISTS idx_registry_conversations_vss ON registry_conversations USING vss(embedding);");
            }
            catch (Exception ex)
            {
                _useVss = false;
                if (_verbose) Console.WriteLine($"[VectorStore] VSS index unavailable, using fallback: {ex.Message}");
            }
        }

        _available = true;
    }
    catch (Exception ex)
    {
        _available = false;
        if (_verbose) Console.WriteLine($"[VectorStore] Disabled: {ex.Message}");
    }
    }

    public async Task UpsertProfileAsync(DataProfile profile)
    {
        Ensure();
        var json = JsonSerializer.Serialize(profile);
        var sql = "INSERT OR REPLACE INTO registry_files (file_path, row_count, column_count, profile_json, updated_at) VALUES (?, ?, ?, ?, NOW())";
        await ExecAsync(sql, profile.SourcePath, profile.RowCount, profile.ColumnCount, json);
    }

    public async Task UpsertEmbeddingsAsync(DataProfile profile)
    {
        if (!_available) return;
        Ensure();
        // Remove previous entries for this file
        await ExecAsync("DELETE FROM registry_embeddings WHERE file_path = ?", profile.SourcePath);

        // Dataset-level summary
        var summaryText = BuildDatasetSummary(profile);
        await InsertEmbeddingAsync(profile.SourcePath, "dataset_summary", "summary", "{}", MakeVector(summaryText));

        // Columns
        foreach (var col in profile.Columns)
        {
            var meta = JsonSerializer.Serialize(new
            {
                col.InferredType,
                col.NullPercent,
                col.UniquePercent,
                col.Mean,
                col.StdDev,
                col.Min,
                col.Max,
                col.Distribution,
                col.Trend,
                col.TimeSeries
            });
            var text = BuildColumnSummary(col);
            await InsertEmbeddingAsync(profile.SourcePath, col.Name, "column", meta, MakeVector(text));
        }

        // Insights
        foreach (var insight in profile.Insights.Take(20))
        {
            var meta = JsonSerializer.Serialize(new { insight.Title, insight.Source, insight.RelatedColumns });
            var text = $"{insight.Title}: {insight.Description}";
            await InsertEmbeddingAsync(profile.SourcePath, insight.Title, "insight", meta, MakeVector(text));
        }
    }

    public async Task<List<RegistryHit>> SearchAsync(string query, int topK = 6)
    {
        if (!_available) return [];
        Ensure();
        var queryVec = EmbeddingHelper.EmbedText(query);

        // If VSS available, use index
        if (_useVss)
        {
            var vecLiteral = VectorLiteral(queryVec);
            var sql = $@"
                SELECT file_path, label, kind, metadata, vss_distance(embedding, {vecLiteral}) AS distance
                FROM registry_embeddings
                ORDER BY distance ASC
                LIMIT {topK};";

            var hits = new List<RegistryHit>();
            await using var cmd = _conn!.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                hits.Add(new RegistryHit
                {
                    FilePath = reader.GetString(0),
                    Label = reader.GetString(1),
                    Kind = reader.GetString(2),
                    Metadata = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Score = reader.IsDBNull(4) ? 1.0 : reader.GetDouble(4)
                });
            }
            return hits;
        }

        // Fallback: brute-force cosine similarity in-process
        var fallbackHits = new List<RegistryHit>();
        var sqlAll = "SELECT file_path, label, kind, metadata, embedding_json FROM registry_embeddings";
        await using var cmdAll = _conn!.CreateCommand();
        cmdAll.CommandText = sqlAll;
        await using var readerAll = await cmdAll.ExecuteReaderAsync();
        while (await readerAll.ReadAsync())
        {
            var filePath = readerAll.GetString(0);
            var label = readerAll.GetString(1);
            var kind = readerAll.GetString(2);
            var metadata = readerAll.IsDBNull(3) ? "" : readerAll.GetString(3);
            var json = readerAll.IsDBNull(4) ? null : readerAll.GetString(4);
            if (json is null) continue;

            try
            {
                var emb = JsonSerializer.Deserialize<float[]>(json);
                if (emb == null || emb.Length == 0) continue;
                var score = CosineDistance(queryVec, emb);
                fallbackHits.Add(new RegistryHit
                {
                    FilePath = filePath,
                    Label = label,
                    Kind = kind,
                    Metadata = metadata,
                    Score = score
                });
            }
            catch { /* ignore bad rows */ }
        }

        return fallbackHits
            .OrderBy(h => h.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 1.0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 1.0;
        var sim = dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        // distance style: lower is better
        return 1 - sim;
    }

    private static string BuildDatasetSummary(DataProfile profile)
    {
        return $"Dataset {Path.GetFileName(profile.SourcePath)}: {profile.RowCount} rows, {profile.ColumnCount} columns. " +
               $"Numeric: {profile.Columns.Count(c => c.InferredType == ColumnType.Numeric)}, " +
               $"Categorical: {profile.Columns.Count(c => c.InferredType == ColumnType.Categorical)}, " +
               $"Date/time: {profile.Columns.Count(c => c.InferredType == ColumnType.DateTime)}.";
    }

    private static string BuildColumnSummary(ColumnProfile col)
    {
        var parts = new List<string> { $"Column {col.Name} ({col.InferredType})" };
        if (col.Mean.HasValue) parts.Add($"mean {col.Mean.Value:F2}");
        if (col.StdDev.HasValue) parts.Add($"std {col.StdDev.Value:F2}");
        if (col.Mad.HasValue) parts.Add($"mad {col.Mad.Value:F2}");
        if (col.Min.HasValue && col.Max.HasValue) parts.Add($"range {col.Min:F2}-{col.Max:F2}");
        if (col.Distribution.HasValue && col.Distribution != DistributionType.Unknown) parts.Add($"dist {col.Distribution}");
        if (col.Trend?.Direction is TrendDirection.Increasing or TrendDirection.Decreasing)
            parts.Add($"trend {col.Trend.Direction} (R2={col.Trend.RSquared:F2})");
        if (col.TimeSeries != null) parts.Add($"time series {col.TimeSeries.Granularity}");
        if (col.TextPatterns.Count > 0) parts.Add($"text {col.TextPatterns[0].PatternType}");
        return string.Join(", ", parts);
    }

    private static float[] MakeVector(string text) => EmbeddingHelper.EmbedText(text);

    private static string VectorLiteral(float[] vector)
    {
        var parts = vector.Select(v => v.ToString("G", CultureInfo.InvariantCulture));
        return $"[{string.Join(",", parts)}]";
    }

    private async Task InsertEmbeddingAsync(string filePath, string label, string kind, string metadata, float[] embedding)
    {
        var vecLiteral = VectorLiteral(embedding);
        var json = JsonSerializer.Serialize(embedding);
        var sql = $@"INSERT INTO registry_embeddings (id, file_path, label, kind, metadata, embedding, embedding_json)
                     VALUES (nextval('registry_embeddings_seq'), ?, ?, ?, ?, {vecLiteral}, ?);";
        await ExecAsync(sql, filePath, label, kind, metadata, json);
    }

    public async Task AppendConversationTurnAsync(string sessionId, string role, string content)
    {
        if (!_available) return;
        Ensure();
        var embedding = MakeVector(content);
        var vecLiteral = VectorLiteral(embedding);
        var json = JsonSerializer.Serialize(embedding);
        var sql = $@"INSERT INTO registry_conversations (session_id, turn_id, role, content, embedding, embedding_json)
                     VALUES (?, nextval('registry_conversations_seq'), ?, ?, {vecLiteral}, ?);";
        await ExecAsync(sql, sessionId, role, content, json);
    }

    public async Task<List<ConversationTurn>> GetConversationContextAsync(string sessionId, string query, int topK = 5)
    {
        var result = new List<ConversationTurn>();
        if (!_available) return result;
        Ensure();
        var queryVec = MakeVector(query);

        if (_useVss)
        {
            var vecLiteral = VectorLiteral(queryVec);
            var sql = $@"SELECT role, content, vss_distance(embedding, {vecLiteral}) as distance
                         FROM registry_conversations
                         WHERE session_id = ?
                         ORDER BY distance ASC, created_at DESC
                         LIMIT {topK};";
            await using var cmd = _conn!.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new DuckDBParameter { Value = sessionId });
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ConversationTurn
                {
                    Role = reader.GetString(0),
                    Content = reader.GetString(1)
                });
            }
            return result;
        }

        // Fallback cosine within session
        var sqlAll = "SELECT role, content, embedding_json, created_at FROM registry_conversations WHERE session_id = ?";
        await using var cmdAll = _conn!.CreateCommand();
        cmdAll.CommandText = sqlAll;
        cmdAll.Parameters.Add(new DuckDBParameter { Value = sessionId });
        await using var readerAll = await cmdAll.ExecuteReaderAsync();
        var temp = new List<(string role, string content, float[] emb, DateTime created)>();
        while (await readerAll.ReadAsync())
        {
            var role = readerAll.GetString(0);
            var content = readerAll.GetString(1);
            var json = readerAll.IsDBNull(2) ? null : readerAll.GetString(2);
            var created = readerAll.IsDBNull(3) ? DateTime.UtcNow : readerAll.GetDateTime(3);
            if (json is null) continue;
            var emb = JsonSerializer.Deserialize<float[]>(json);
            if (emb == null || emb.Length == 0) continue;
            temp.Add((role, content, emb, created));
        }
        result = temp
            .Select(t => new { t.role, t.content, score = CosineDistance(queryVec, t.emb), t.created })
            .OrderBy(x => x.score)
            .ThenByDescending(x => x.created)
            .Take(topK)
            .Select(x => new ConversationTurn { Role = x.role, Content = x.content })
            .ToList();
        return result;
    }

    private async Task ExecAsync(string sql, params object?[] args)
    {
        if (_conn is null) throw new InvalidOperationException("Vector store connection not initialized");
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < args.Length; i++)
        {
            cmd.Parameters.Add(new DuckDBParameter { Value = args[i] ?? DBNull.Value });
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private void Ensure()
    {
        if (_conn == null) throw new InvalidOperationException("Vector store not initialized");
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}

public record RegistryHit
{
    public string FilePath { get; init; } = "";
    public string Label { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Metadata { get; init; } = "";
    public double Score { get; init; }
}

public record ConversationTurn
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
}
