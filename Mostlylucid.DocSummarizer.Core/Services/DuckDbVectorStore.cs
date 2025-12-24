using System.Data;
using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// DuckDB-backed vector store for document segments and embeddings.
/// Provides an embedded, zero-external-dependency alternative to Qdrant.
/// 
/// Features:
/// - Vector similarity search (cosine, L2, inner product)
/// - Metadata filtering with SQL
/// - Persistent storage in a single file
/// - Word lists and caches in the same database
/// - No external services required
/// 
/// Use cases:
/// - Local development without Qdrant
/// - CLI tools and scripts
/// - Single-machine deployments
/// - Offline/air-gapped environments
/// 
/// For enterprise/distributed deployments, use QdrantVectorStore instead.
/// </summary>
public sealed class DuckDbVectorStore : IVectorStore
{
    private readonly DuckDBConnection _connection;
    private readonly string _dbPath;
    private readonly bool _verbose;
    private int _vectorDimension;
    private bool _initialized;
    private bool _vssLoaded;

    /// <summary>
    /// Create a DuckDB vector store.
    /// </summary>
    /// <param name="dbPath">Path to database file, or ":memory:" for in-memory</param>
    /// <param name="vectorDimension">Embedding dimension (e.g., 384 for all-MiniLM-L6-v2)</param>
    /// <param name="verbose">Enable verbose logging</param>
    public DuckDbVectorStore(string? dbPath = null, int vectorDimension = 384, bool verbose = false)
    {
        _dbPath = dbPath ?? ":memory:";
        _vectorDimension = vectorDimension;
        _verbose = verbose;
        // Use DataSource (no space) - matches working CsvLlm pattern
        _connection = new DuckDBConnection($"DataSource={_dbPath}");
        _connection.Open();
    }

    /// <inheritdoc />
    public bool IsPersistent => _dbPath != ":memory:";

    /// <inheritdoc />
    public async Task InitializeAsync(string collectionName, int vectorSize, CancellationToken ct = default)
    {
        _vectorDimension = vectorSize;
        
        if (_initialized) return;

        // Try to load VSS extension for optimized vector search
        await TryLoadVssExtensionAsync(ct);
        
        await CreateSchemaAsync(ct);
        _initialized = true;

        if (_verbose)
        {
            Console.WriteLine($"[DuckDbVectorStore] Initialized at {_dbPath}");
            Console.WriteLine($"[DuckDbVectorStore] VSS extension: {(_vssLoaded ? "loaded" : "not available, using fallback")}");
        }
    }

    private async Task TryLoadVssExtensionAsync(CancellationToken ct)
    {
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSTALL vss; LOAD vss;";
            await cmd.ExecuteNonQueryAsync(ct);
            _vssLoaded = true;
        }
        catch
        {
            // VSS not available - will use manual cosine similarity
            _vssLoaded = false;
        }
    }

    private async Task CreateSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        
        // Segments table with embedding stored as FLOAT array
        // Note: We use TEXT for embedding since DuckDB array syntax varies by version
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS segments (
                id VARCHAR PRIMARY KEY,
                collection VARCHAR NOT NULL,
                doc_id VARCHAR NOT NULL,
                content_hash VARCHAR NOT NULL,
                text TEXT NOT NULL,
                section_title VARCHAR,
                segment_type VARCHAR,
                heading_level INTEGER DEFAULT 0,
                index_position INTEGER DEFAULT 0,
                salience FLOAT DEFAULT 0.0,
                embedding TEXT,
                metadata JSON,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_segments_collection ON segments(collection);
            CREATE INDEX IF NOT EXISTS idx_segments_doc ON segments(collection, doc_id);
            CREATE INDEX IF NOT EXISTS idx_segments_hash ON segments(collection, content_hash);
            CREATE INDEX IF NOT EXISTS idx_segments_salience ON segments(collection, salience DESC);
            
            -- Summary cache table
            CREATE TABLE IF NOT EXISTS summary_cache (
                cache_key VARCHAR PRIMARY KEY,
                collection VARCHAR NOT NULL,
                evidence_hash VARCHAR NOT NULL,
                summary_json TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_cache_collection ON summary_cache(collection);
            CREATE INDEX IF NOT EXISTS idx_cache_evidence ON summary_cache(collection, evidence_hash);
            
            -- Word lists table (for entity extraction)
            CREATE TABLE IF NOT EXISTS word_lists (
                id INTEGER PRIMARY KEY,
                word VARCHAR NOT NULL,
                word_lower VARCHAR NOT NULL,
                category VARCHAR NOT NULL,
                is_custom BOOLEAN DEFAULT FALSE,
                UNIQUE(word_lower, category)
            );
            
            CREATE INDEX IF NOT EXISTS idx_wordlist_lower ON word_lists(word_lower);
            CREATE INDEX IF NOT EXISTS idx_wordlist_category ON word_lists(category);
            """;
        
        await cmd.ExecuteNonQueryAsync(ct);
    }

    #region IVectorStore Implementation

    /// <inheritdoc />
    public async Task<bool> HasDocumentAsync(string collectionName, string docId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM segments WHERE collection = $collection AND doc_id = $doc_id LIMIT 1";
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }

    /// <inheritdoc />
    public async Task UpsertSegmentsAsync(string collectionName, IEnumerable<Segment> segments, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var segmentList = segments.ToList();
        if (segmentList.Count == 0) return;

        // Use transaction for batch insert
        await using var transaction = _connection.BeginTransaction();
        
        try
        {
            foreach (var segment in segmentList)
            {
                await UpsertSegmentInternalAsync(collectionName, segment, ct);
            }
            
            transaction.Commit();
            
            if (_verbose)
                Console.WriteLine($"[DuckDbVectorStore] Upserted {segmentList.Count} segments to '{collectionName}'");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task UpsertSegmentInternalAsync(string collectionName, Segment segment, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        
        // Serialize embedding to JSON array string for storage
        var embeddingJson = segment.Embedding != null 
            ? JsonSerializer.Serialize(segment.Embedding)
            : null;
        
        // Get docId - we need to extract it from the segment Id or use a default
        // Segment.Id format: "{docId}_{type}_{index}"
        var docId = ExtractDocIdFromSegment(segment);
        
        cmd.CommandText = """
            INSERT INTO segments (id, collection, doc_id, content_hash, text, section_title, segment_type, 
                                  heading_level, index_position, salience, embedding)
            VALUES ($id, $collection, $doc_id, $hash, $text, $section, $type, $level, $idx, $salience, $embedding)
            ON CONFLICT (id) DO UPDATE SET
                text = $text,
                content_hash = $hash,
                section_title = $section,
                salience = $salience,
                embedding = $embedding
            """;
        
        cmd.Parameters.Add(new DuckDBParameter("id", segment.Id));
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        cmd.Parameters.Add(new DuckDBParameter("hash", segment.ContentHash));
        cmd.Parameters.Add(new DuckDBParameter("text", segment.Text));
        cmd.Parameters.Add(new DuckDBParameter("section", segment.SectionTitle ?? ""));
        cmd.Parameters.Add(new DuckDBParameter("type", segment.Type.ToString()));
        cmd.Parameters.Add(new DuckDBParameter("level", segment.HeadingLevel));
        cmd.Parameters.Add(new DuckDBParameter("idx", segment.Index));
        cmd.Parameters.Add(new DuckDBParameter("salience", segment.SalienceScore));
        cmd.Parameters.Add(new DuckDBParameter("embedding", embeddingJson ?? (object)DBNull.Value));
        
        await cmd.ExecuteNonQueryAsync(ct);
    }
    
    private static string ExtractDocIdFromSegment(Segment segment)
    {
        // Segment.Id format: "{docId}_{type}_{index}" e.g., "mydoc_s_42"
        var parts = segment.Id.Split('_');
        if (parts.Length >= 3)
        {
            // Join all parts except the last two (type and index)
            return string.Join("_", parts.Take(parts.Length - 2));
        }
        return segment.Id;
    }

    /// <inheritdoc />
    public async Task<List<Segment>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int topK,
        string? docId = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        // Get all segments with embeddings, then compute similarity in memory
        // This is less efficient than native vector search but works without VSS extension
        var segments = await GetAllSegmentsWithEmbeddingsAsync(collectionName, docId, ct);
        
        if (segments.Count == 0)
            return new List<Segment>();

        // Score by cosine similarity
        var scored = segments
            .Where(s => s.Embedding != null)
            .Select(s => (Segment: s, Score: CosineSimilarity(queryEmbedding, s.Embedding!)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        // Update QuerySimilarity on segments
        foreach (var (segment, score) in scored)
        {
            segment.QuerySimilarity = score;
        }

        return scored.Select(x => x.Segment).ToList();
    }

    private async Task<List<Segment>> GetAllSegmentsWithEmbeddingsAsync(string collectionName, string? docId, CancellationToken ct)
    {
        var segments = new List<Segment>();
        
        await using var cmd = _connection.CreateCommand();
        
        if (docId != null)
        {
            cmd.CommandText = """
                SELECT id, doc_id, content_hash, text, section_title, segment_type, heading_level,
                       index_position, salience, embedding
                FROM segments
                WHERE collection = $collection AND doc_id = $doc_id AND embedding IS NOT NULL
                ORDER BY index_position
                """;
            cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        }
        else
        {
            cmd.CommandText = """
                SELECT id, doc_id, content_hash, text, section_title, segment_type, heading_level,
                       index_position, salience, embedding
                FROM segments
                WHERE collection = $collection AND embedding IS NOT NULL
                ORDER BY salience DESC
                """;
        }
        
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var segment = ReadSegmentFromReader(reader);
            if (segment != null)
                segments.Add(segment);
        }
        
        return segments;
    }

    /// <inheritdoc />
    public async Task<List<Segment>> GetDocumentSegmentsAsync(string collectionName, string docId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var segments = new List<Segment>();
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, doc_id, content_hash, text, section_title, segment_type, heading_level,
                   index_position, salience, embedding
            FROM segments
            WHERE collection = $collection AND doc_id = $doc_id
            ORDER BY index_position
            """;
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var segment = ReadSegmentFromReader(reader);
            if (segment != null)
                segments.Add(segment);
        }
        
        return segments;
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM segments WHERE collection = $collection";
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        
        // Also delete cached summaries for this collection
        await using var cacheCmd = _connection.CreateCommand();
        cacheCmd.CommandText = "DELETE FROM summary_cache WHERE collection = $collection";
        cacheCmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        await cacheCmd.ExecuteNonQueryAsync(ct);
        
        if (_verbose)
            Console.WriteLine($"[DuckDbVectorStore] Deleted {deleted} segments from '{collectionName}'");
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(string collectionName, string docId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM segments WHERE collection = $collection AND doc_id = $doc_id";
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        
        if (_verbose)
            Console.WriteLine($"[DuckDbVectorStore] Deleted {deleted} segments for doc '{docId}' from '{collectionName}'");
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, Segment>> GetSegmentsByHashAsync(
        string collectionName,
        IEnumerable<string> contentHashes,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var hashList = contentHashes.ToList();
        if (hashList.Count == 0)
            return new Dictionary<string, Segment>();

        var result = new Dictionary<string, Segment>();
        
        // Query in batches to avoid very long IN clauses
        const int batchSize = 100;
        for (int i = 0; i < hashList.Count; i += batchSize)
        {
            var batch = hashList.Skip(i).Take(batchSize).ToList();
            var placeholders = string.Join(",", batch.Select((_, idx) => $"$hash{idx}"));
            
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, doc_id, content_hash, text, section_title, segment_type, heading_level,
                       index_position, salience, embedding
                FROM segments
                WHERE collection = $collection AND content_hash IN ({placeholders})
                """;
            cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
            
            for (int j = 0; j < batch.Count; j++)
            {
                cmd.Parameters.Add(new DuckDBParameter($"hash{j}", batch[j]));
            }
            
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var segment = ReadSegmentFromReader(reader);
                if (segment != null && !string.IsNullOrEmpty(segment.ContentHash))
                {
                    result[segment.ContentHash] = segment;
                }
            }
        }
        
        return result;
    }

    /// <inheritdoc />
    public async Task RemoveStaleSegmentsAsync(
        string collectionName,
        string docId,
        IEnumerable<string> validContentHashes,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var hashList = validContentHashes.ToList();
        
        if (hashList.Count == 0)
        {
            // Remove all segments for this document
            await DeleteDocumentAsync(collectionName, docId, ct);
            return;
        }

        // Delete segments NOT in the valid hash list
        var placeholders = string.Join(",", hashList.Select((_, idx) => $"$hash{idx}"));
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            DELETE FROM segments 
            WHERE collection = $collection 
              AND doc_id = $doc_id 
              AND content_hash NOT IN ({placeholders})
            """;
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("doc_id", docId));
        
        for (int i = 0; i < hashList.Count; i++)
        {
            cmd.Parameters.Add(new DuckDBParameter($"hash{i}", hashList[i]));
        }
        
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        
        if (_verbose && deleted > 0)
            Console.WriteLine($"[DuckDbVectorStore] Removed {deleted} stale segments from '{docId}'");
    }

    /// <inheritdoc />
    public async Task<DocumentSummary?> GetCachedSummaryAsync(string collectionName, string evidenceHash, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT summary_json FROM summary_cache WHERE collection = $collection AND evidence_hash = $hash";
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("hash", evidenceHash));
        
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value) return null;
        
        var json = result.ToString();
        return JsonSerializer.Deserialize<DocumentSummary>(json!);
    }

    /// <inheritdoc />
    public async Task CacheSummaryAsync(string collectionName, string evidenceHash, DocumentSummary summary, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var json = JsonSerializer.Serialize(summary);
        var cacheKey = $"{collectionName}_{evidenceHash}";
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO summary_cache (cache_key, collection, evidence_hash, summary_json, created_at)
            VALUES ($key, $collection, $hash, $json, current_timestamp)
            ON CONFLICT (cache_key) DO UPDATE SET summary_json = excluded.summary_json, created_at = excluded.created_at
            """;
        cmd.Parameters.Add(new DuckDBParameter("key", cacheKey));
        cmd.Parameters.Add(new DuckDBParameter("collection", collectionName));
        cmd.Parameters.Add(new DuckDBParameter("hash", evidenceHash));
        cmd.Parameters.Add(new DuckDBParameter("json", json));
        
        await cmd.ExecuteNonQueryAsync(ct);
        
        if (_verbose)
            Console.WriteLine($"[DuckDbVectorStore] Cached summary for evidence hash '{evidenceHash[..Math.Min(8, evidenceHash.Length)]}...'");
    }

    #endregion

    #region Word Lists (Bonus Feature)

    /// <summary>
    /// Load a word list into the database for entity extraction.
    /// </summary>
    public async Task LoadWordListAsync(string category, IEnumerable<string> words, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var transaction = _connection.BeginTransaction();
        
        try
        {
            foreach (var word in words.Where(w => !string.IsNullOrWhiteSpace(w)))
            {
                await using var cmd = _connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO word_lists (word, word_lower, category)
                    VALUES ($word, $lower, $category)
                    ON CONFLICT (word_lower, category) DO NOTHING
                    """;
                cmd.Parameters.Add(new DuckDBParameter("word", word.Trim()));
                cmd.Parameters.Add(new DuckDBParameter("lower", word.Trim().ToLowerInvariant()));
                cmd.Parameters.Add(new DuckDBParameter("category", category));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Get all words in a category.
    /// </summary>
    public async Task<HashSet<string>> GetWordListAsync(string category, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT word_lower FROM word_lists WHERE category = $category";
        cmd.Parameters.Add(new DuckDBParameter("category", category));
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            words.Add(reader.GetString(0));
        }
        
        return words;
    }

    /// <summary>
    /// Check if a word is in a category.
    /// </summary>
    public async Task<bool> IsInWordListAsync(string word, string category, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM word_lists WHERE word_lower = $word AND category = $category LIMIT 1";
        cmd.Parameters.Add(new DuckDBParameter("word", word.ToLowerInvariant()));
        cmd.Parameters.Add(new DuckDBParameter("category", category));
        
        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
    }

    #endregion

    #region Statistics & Maintenance

    /// <summary>
    /// Get database statistics.
    /// </summary>
    public async Task<(int Segments, int Collections, int CachedSummaries, long DbSizeBytes)> GetStatsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT 
                (SELECT COUNT(*) FROM segments),
                (SELECT COUNT(DISTINCT collection) FROM segments),
                (SELECT COUNT(*) FROM summary_cache)
            """;
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var segments = reader.GetInt32(0);
            var collections = reader.GetInt32(1);
            var cached = reader.GetInt32(2);
            
            long dbSize = 0;
            if (_dbPath != ":memory:" && File.Exists(_dbPath))
            {
                dbSize = new FileInfo(_dbPath).Length;
            }
            
            return (segments, collections, cached, dbSize);
        }
        
        return (0, 0, 0, 0);
    }

    /// <summary>
    /// Run vacuum to reclaim space.
    /// </summary>
    public async Task VacuumAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "VACUUM";
        await cmd.ExecuteNonQueryAsync(ct);
        
        if (_verbose)
            Console.WriteLine("[DuckDbVectorStore] Vacuum completed");
    }

    #endregion

    #region Helpers

    private Segment? ReadSegmentFromReader(DbDataReader reader)
    {
        try
        {
            var id = reader.GetString(0);
            var docId = reader.GetString(1);
            var contentHash = reader.GetString(2);
            var text = reader.GetString(3);
            var sectionTitle = reader.IsDBNull(4) ? "" : reader.GetString(4);
            var typeStr = reader.IsDBNull(5) ? "Sentence" : reader.GetString(5);
            var headingLevel = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
            var index = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            var salience = reader.IsDBNull(8) ? 0.0 : reader.GetDouble(8);
            
            // Read embedding from JSON
            float[]? embedding = null;
            if (!reader.IsDBNull(9))
            {
                var embeddingJson = reader.GetString(9);
                if (!string.IsNullOrEmpty(embeddingJson))
                {
                    embedding = JsonSerializer.Deserialize<float[]>(embeddingJson);
                }
            }
            
            var type = Enum.TryParse<SegmentType>(typeStr, out var t) ? t : SegmentType.Sentence;
            
            // Create segment using existing constructor
            var segment = new Segment(docId, text, type, index, 0, text.Length)
            {
                SalienceScore = salience,
                Embedding = embedding
            };
            
            return segment;
        }
        catch
        {
            return null;
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dotProduct / denom;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (!_initialized)
            await InitializeAsync("default", _vectorDimension, ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _connection.Close();
        await _connection.DisposeAsync();
    }

    #endregion
}
