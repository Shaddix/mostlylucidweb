using Microsoft.EntityFrameworkCore;

namespace Mostlylucid.LlmAltText.Data;

/// <summary>
/// Database context for alt text caching
/// Supports SQLite (default) and PostgreSQL
/// </summary>
public class AltTextDbContext : DbContext
{
    public AltTextDbContext(DbContextOptions<AltTextDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Cached alt text entries
    /// </summary>
    public DbSet<ImageAltTextEntry> ImageAltTexts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ImageAltTextEntry>(entity =>
        {
            entity.ToTable("ImageAltTexts");

            // Index on source hash for fast lookups
            entity.HasIndex(e => e.SourceHash)
                .IsUnique()
                .HasDatabaseName("IX_ImageAltTexts_SourceHash");

            // Index on content hash for duplicate detection
            entity.HasIndex(e => e.ContentHash)
                .HasDatabaseName("IX_ImageAltTexts_ContentHash");

            // Index on created date for cleanup
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_ImageAltTexts_CreatedAt");
        });
    }
}
