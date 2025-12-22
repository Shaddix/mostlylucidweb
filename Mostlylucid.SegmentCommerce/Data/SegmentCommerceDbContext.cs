using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Data;

public class SegmentCommerceDbContext : DbContext
{
    public SegmentCommerceDbContext(DbContextOptions<SegmentCommerceDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<VisitorProfileEntity> VisitorProfiles => Set<VisitorProfileEntity>();
    public DbSet<InteractionEventEntity> InteractionEvents => Set<InteractionEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product configuration
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsTrending);
            entity.HasIndex(e => e.IsFeatured);
            
            // Configure Tags as a PostgreSQL array
            entity.Property(e => e.Tags)
                .HasColumnType("text[]");
        });

        // Category configuration
        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // Visitor profile configuration
        modelBuilder.Entity<VisitorProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileToken).IsUnique();
            entity.HasIndex(e => e.LastSeenAt);
            
            // Configure JSONB for interests
            entity.Property(e => e.Interests)
                .HasColumnType("jsonb");
        });

        // Interaction event configuration
        modelBuilder.Entity<InteractionEventEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Category, e.CreatedAt });
            
            // Configure JSONB for metadata
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb");
        });
    }
}
