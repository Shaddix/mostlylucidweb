using System.Text.Json;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;
using Xunit;

namespace Mostlylucid.DocSummarizer.Tests.Services;

public class ChunkCacheServiceTests
{
    [Fact]
    public async Task SaveAndLoad_ReturnsChunks_WhenHashesMatch()
    {
        var tempDir = CreateTempDir();
        try
        {
            var service = CreateService(tempDir, retentionDays: 30);
            Assert.True(service.Enabled);
            var chunks = new List<DocumentChunk>
            {
                new(0, "Heading 1", 1, "content one", "hash1"),
                new(1, "Heading 2", 1, "content two", "hash2")
            };

            await service.SaveAsync("doc", "filehash", chunks);

            var expectedPath = Path.Combine(tempDir, "doc_filehash.json");
            Assert.True(File.Exists(expectedPath));
 
            var json = await File.ReadAllTextAsync(expectedPath);
            Console.WriteLine($"Exists:{File.Exists(expectedPath)}");
            Console.WriteLine($"Files:{string.Join(',', Directory.GetFiles(tempDir))}");
            Console.WriteLine($"Json:{json}");

            var entry = JsonSerializer.Deserialize<ChunkCacheEntry>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.General)
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    PropertyNameCaseInsensitive = true
                });
            Assert.True(entry != null, $"Entry null. json={json}");
            Assert.Equal("filehash", entry!.FileHash);
            Assert.Equal("v1", entry.Version);
 
            var loaded = await service.TryLoadAsync("doc", "filehash");
 
            Assert.True(loaded != null, $"Cache load failed. Enabled={service.Enabled}, pathExists={File.Exists(expectedPath)}, json={json}");
            Assert.Equal(2, loaded!.Count);
            Assert.All(loaded, c => Assert.Equal(2, c.TotalChunks));

        }
        finally
        {
            SafeDeleteDir(tempDir);
        }
    }

    [Fact]
    public async Task TryLoad_ReturnsNull_WhenHashDoesNotMatch()
    {
        var tempDir = CreateTempDir();
        try
        {
            var service = CreateService(tempDir, retentionDays: 30);
            var chunks = new List<DocumentChunk> { new(0, "Heading", 1, "content", "hash1") };

            await service.SaveAsync("doc", "filehash", chunks);
            var loaded = await service.TryLoadAsync("doc", "different-hash");

            Assert.Null(loaded);
        }
        finally
        {
            SafeDeleteDir(tempDir);
        }
    }

    [Fact]
    public async Task TryLoad_PrunesExpiredEntries()
    {
        var tempDir = CreateTempDir();
        try
        {
            var service = CreateService(tempDir, retentionDays: 1);
            Assert.True(service.Enabled);
            var chunks = new List<DocumentChunk> { new(0, "Heading", 1, "content", "hash1") };

            await service.SaveAsync("doc", "filehash", chunks);
            var expectedPath = Path.Combine(tempDir, "doc_filehash.json");
            Assert.True(File.Exists(expectedPath));
 
            var oldEntry = new ChunkCacheEntry
            {
                DocId = "doc",
                FileHash = "filehash",
                Version = "v1",
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-10),
                LastAccessUtc = DateTimeOffset.UtcNow.AddDays(-10),
                Chunks = chunks
            };


            var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            await using (var stream = File.Create(expectedPath))
            {
                await JsonSerializer.SerializeAsync(stream, oldEntry, options);
            }
 
            var loaded = await service.TryLoadAsync("doc", "filehash");
 
            Assert.Null(loaded);
            Assert.False(File.Exists(expectedPath));

        }
        finally
        {
            SafeDeleteDir(tempDir);
        }
    }

    private static ChunkCacheService CreateService(string dir, int retentionDays)
    {
        var config = new ChunkCacheConfig
        {
            EnableChunkCache = true,
            CacheDirectory = dir,
            RetentionDays = retentionDays,
            VersionToken = "v1"
        };

        return new ChunkCacheService(config, verbose: true);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cachetests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void SafeDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
            // ignore
        }
    }
}
