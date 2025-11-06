using Microsoft.EntityFrameworkCore;

namespace Mostlylucid.Markdig.FetchExtension.Sqlite;

/// <summary>
///     DbContext for SQLite-based markdown cache.
/// </summary>
public class MarkdownCacheDbContext : DbContext
{
    public MarkdownCacheDbContext(DbContextOptions<MarkdownCacheDbContext> options)
        : base(options)
    {
    }

    public DbSet<MarkdownCacheEntry> MarkdownCache { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarkdownCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.HasIndex(e => new { e.Url, e.BlogPostId });
        });
    }
}
