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

    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<ProductVariationEntity> ProductVariations => Set<ProductVariationEntity>();
    public DbSet<SellerEntity> Sellers => Set<SellerEntity>();
    public DbSet<VisitorProfileEntity> VisitorProfiles => Set<VisitorProfileEntity>();
    public DbSet<InteractionEventEntity> InteractionEvents => Set<InteractionEventEntity>();
    public DbSet<TaxonomyNodeEntity> TaxonomyNodes => Set<TaxonomyNodeEntity>();
    public DbSet<ProductTaxonomyEntity> ProductTaxonomy => Set<ProductTaxonomyEntity>();
    public DbSet<StoreEntity> Stores => Set<StoreEntity>();
    public DbSet<StoreUserEntity> StoreUsers => Set<StoreUserEntity>();
    public DbSet<StoreProductEntity> StoreProducts => Set<StoreProductEntity>();
    public DbSet<SessionProfileEntity> SessionProfiles => Set<SessionProfileEntity>();
    public DbSet<AnonymousProfileEntity> AnonymousProfiles => Set<AnonymousProfileEntity>();
    public DbSet<ProfileKeyEntity> ProfileKeys => Set<ProfileKeyEntity>();
    public DbSet<InterestScoreEntity> InterestScores => Set<InterestScoreEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<ProductEmbeddingEntity> ProductEmbeddings => Set<ProductEmbeddingEntity>();
    public DbSet<InterestEmbeddingEntity> InterestEmbeddings => Set<InterestEmbeddingEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<JobQueueEntity> JobQueue => Set<JobQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("ltree");

        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsTrending);
            entity.HasIndex(e => e.IsFeatured);
            entity.HasIndex(e => e.Handle).IsUnique();
            entity.HasIndex(e => e.CategoryPath).HasMethod("gist");

            entity.Property(e => e.CategoryPath).HasColumnType("ltree");
            entity.Property(e => e.Tags).HasColumnType("text[]");

            entity.HasMany(e => e.Variations)
                .WithOne(v => v.Product)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ProductTaxonomy)
                .WithOne(pt => pt.Product)
                .HasForeignKey(pt => pt.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.StoreProducts)
                .WithOne(sp => sp.Product)
                .HasForeignKey(sp => sp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductVariationEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.Color);
            entity.HasIndex(e => e.Size);
            entity.HasIndex(e => new { e.ProductId, e.Color, e.Size }).IsUnique();
            entity.Property(e => e.Color).IsRequired();
            entity.Property(e => e.Size).IsRequired();
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);
        });

        modelBuilder.Entity<SellerEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Rating).HasDefaultValue(0.0);
            entity.Property(e => e.ReviewCount).HasDefaultValue(0);
            entity.Property(e => e.IsVerified).HasDefaultValue(false);

            entity.HasMany(e => e.Products)
                .WithOne(p => p.Seller)
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<TaxonomyNodeEntity>(entity =>
        {
            entity.HasIndex(e => e.Handle).IsUnique();
            entity.HasIndex(e => e.ShopifyTaxonomyId).IsUnique();
            entity.HasIndex(e => e.Path).HasMethod("gist");
            entity.Property(e => e.Path).HasColumnType("ltree");

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductTaxonomyEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.TaxonomyNodeId }).IsUnique();
        });

        modelBuilder.Entity<StoreEntity>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<StoreProductEntity>(entity =>
        {
            entity.HasIndex(e => new { e.StoreId, e.ProductId }).IsUnique();
        });

        modelBuilder.Entity<StoreUserEntity>(entity =>
        {
            entity.HasIndex(e => new { e.StoreId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<VisitorProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileToken).IsUnique();
            entity.HasIndex(e => e.LastSeenAt);
            entity.Property(e => e.Interests).HasColumnType("jsonb");
        });

        modelBuilder.Entity<InteractionEventEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Category, e.CreatedAt });
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SignalEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.SignalType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Category);
            entity.Property(e => e.Context).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SessionProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionKey).IsUnique();
            entity.HasIndex(e => e.ProfileKey);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.PromotionThreshold).HasDefaultValue(0.5);
        });

        modelBuilder.Entity<AnonymousProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileKey).IsUnique();
            entity.HasIndex(e => e.LastSeenAt);
        });

        modelBuilder.Entity<ProfileKeyEntity>(entity =>
        {
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.DerivationMethod);
        });

        modelBuilder.Entity<InterestScoreEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProfileId, e.Category })
                .IsUnique()
                .HasFilter("profile_id IS NOT NULL");

            entity.HasIndex(e => new { e.SessionId, e.Category })
                .IsUnique()
                .HasFilter("session_id IS NOT NULL");
        });

        modelBuilder.Entity<ProductEmbeddingEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductId).IsUnique();
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        modelBuilder.Entity<InterestEmbeddingEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileId);
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });
    }
}
