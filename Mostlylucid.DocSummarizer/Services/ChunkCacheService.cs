using System.Text.Json;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// Simple disk-based cache for Docling chunks keyed by file hash.
/// </summary>
public class ChunkCacheService
{
    private readonly ChunkCacheConfig _config;
    private readonly bool _verbose;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ChunkCacheService(ChunkCacheConfig config, bool verbose = false)
    {
        _config = config ?? new ChunkCacheConfig();
        _verbose = verbose;
        EnsureCacheDirectory();
        CleanupExpired();
    }

    public bool Enabled => _config.EnableChunkCache;

    /// <summary>
    /// Try to load cached chunks for a document if the file hash matches.
    /// </summary>
    public async Task<List<DocumentChunk>?> TryLoadAsync(string docId, string fileHash, CancellationToken ct = default)
    {
        if (!Enabled) return null;

        var path = GetCachePath(docId, fileHash);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            var entry = JsonSerializer.Deserialize<ChunkCacheEntry>(json, _jsonOptions);
            if (entry == null) return null;

            if (!string.Equals(entry.Version, _config.VersionToken, StringComparison.Ordinal))
                return null;

            if (!string.Equals(entry.FileHash, fileHash, StringComparison.Ordinal))
                return null;

            if (IsExpired(entry.CreatedUtc))
            {
                TryDelete(path);
                return null;
            }

            // Touch last access
            entry.LastAccessUtc = DateTimeOffset.UtcNow;
            await SaveEntryAsync(path, entry, ct);

            var total = entry.Chunks?.Count ?? 0;
            return entry.Chunks?.Select(c => c.WithTotalChunks(total)).ToList();
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                Console.WriteLine($"[Cache] Failed to load cache: {ex.Message}");
            }
            return null;
        }
    }

    /// <summary>
    /// Persist chunks for future runs.
    /// </summary>
    public async Task SaveAsync(string docId, string fileHash, List<DocumentChunk> chunks, CancellationToken ct = default)
    {
        if (!Enabled || chunks.Count == 0) return;

        var path = GetCachePath(docId, fileHash);
        var entry = new ChunkCacheEntry
        {
            DocId = docId,
            FileHash = fileHash,
            Version = _config.VersionToken,
            CreatedUtc = DateTimeOffset.UtcNow,
            LastAccessUtc = DateTimeOffset.UtcNow,
            Chunks = chunks
        };

        EnsureCacheDirectory();
        await SaveEntryAsync(path, entry, ct);
    }

    /// <summary>
    /// Remove cache files older than retention window.
    /// </summary>
    public void CleanupExpired()
    {
        if (!Enabled || _config.RetentionDays <= 0) return;
        var dir = GetCacheDirectory();
        if (!Directory.Exists(dir)) return;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_config.RetentionDays);
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    TryDelete(file);
                    if (_verbose) Console.WriteLine($"[Cache] Removed expired chunk cache: {info.Name}");
                }
            }
            catch
            {
                // Ignore cleanup issues
            }
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetCachePath(string docId, string fileHash)
    {
        var safeId = string.Join("", docId.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var name = $"{safeId}_{fileHash}.json";
        return Path.Combine(GetCacheDirectory(), name);
    }

    private string GetCacheDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_config.CacheDirectory))
            return _config.CacheDirectory!;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docsummarizer", "chunks");
    }

    private void EnsureCacheDirectory()
    {
        if (!Enabled) return;
        var dir = GetCacheDirectory();
        Directory.CreateDirectory(dir);
    }

    private bool IsExpired(DateTimeOffset created)
    {
        if (_config.RetentionDays <= 0) return false;
        return created < DateTimeOffset.UtcNow.AddDays(-_config.RetentionDays);
    }

    private async Task SaveEntryAsync(string path, ChunkCacheEntry entry, CancellationToken ct)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, entry, _jsonOptions, ct);
    }
}

public class ChunkCacheEntry
{
    public string DocId { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessUtc { get; set; }
        = DateTimeOffset.UtcNow;
    public List<DocumentChunk> Chunks { get; set; } = [];
}
