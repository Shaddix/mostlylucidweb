using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Data;

public class SegmentCommerceDbContext : DbContext
{
    public SegmentCommerceDbContext(DbContextOptions<SegmentCommerceDbContext> options)
        : base(options)
    {
    }

    // Core entities
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<VisitorProfileEntity> VisitorProfiles => Set<VisitorProfileEntity>();
    public DbSet<InteractionEventEntity> InteractionEvents => Set<InteractionEventEntity>();

    // Profiles
    public DbSet<SessionProfileEntity> SessionProfiles => Set<SessionProfileEntity>();
    public DbSet<AnonymousProfileEntity> AnonymousProfiles => Set<AnonymousProfileEntity>();
    public DbSet<ProfileKeyEntity> ProfileKeys => Set<ProfileKeyEntity>();
    public DbSet<InterestScoreEntity> InterestScores => Set<InterestScoreEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();

    // Embeddings (pgvector)
    public DbSet<ProductEmbeddingEntity> ProductEmbeddings => Set<ProductEmbeddingEntity>();
    public DbSet<InterestEmbeddingEntity> InterestEmbeddings => Set<InterestEmbeddingEntity>();

    // Queue system
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<JobQueueEntity> JobQueue => Set<JobQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

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

        // Signal configuration
        modelBuilder.Entity<SignalEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.SignalType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.Context)
                .HasColumnType("jsonb");
        });

        // Session profile configuration
        modelBuilder.Entity<SessionProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionKey).IsUnique();
            entity.HasIndex(e => e.ProfileKey);
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property(e => e.PromotionThreshold)
                .HasDefaultValue(0.5);
        });

        // Anonymous profile configuration
        modelBuilder.Entity<AnonymousProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileKey).IsUnique();
            entity.HasIndex(e => e.LastSeenAt);
        });

        // Profile key configuration
        modelBuilder.Entity<ProfileKeyEntity>(entity =>
        {
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.DerivationMethod);
        });

        // Interest score configuration
        modelBuilder.Entity<InterestScoreEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProfileId, e.Category })
                .IsUnique()
                .HasFilter("profile_id IS NOT NULL");

            entity.HasIndex(e => new { e.SessionId, e.Category })
                .IsUnique()
                .HasFilter("session_id IS NOT NULL");
        });

        // Product embedding configuration (pgvector)
        modelBuilder.Entity<ProductEmbeddingEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductId).IsUnique();

            // Create HNSW index for fast approximate nearest neighbour search
            // Using cosine distance for normalized embeddings
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        // Interest embedding configuration (pgvector)
        modelBuilder.Entity<InterestEmbeddingEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileId);
            entity.HasIndex(e => e.SessionId);

            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        // Outbox message configuration
        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            // Index for polling unprocessed messages
            entity.HasIndex(e => new { e.ProcessedAt, e.CreatedAt })
                .HasFilter("processed_at IS NULL");

            // Index for retry logic
            entity.HasIndex(e => e.NextRetryAt)
                .HasFilter("processed_at IS NULL AND next_retry_at IS NOT NULL");

            entity.HasIndex(e => e.AggregateId);
        });

        // Job queue configuration
        modelBuilder.Entity<JobQueueEntity>(entity =>
        {
            // Composite index for efficient job polling with SKIP LOCKED
            entity.HasIndex(e => new { e.Queue, e.Status, e.ScheduledAt, e.Priority })
                .HasFilter("status = 0"); // Pending jobs only

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.JobType);
            entity.HasIndex(e => e.CreatedAt);

            // Index for finding stuck jobs (processing too long)
            entity.HasIndex(e => e.StartedAt)
                .HasFilter("status = 1"); // Processing jobs
        });
    }
}
