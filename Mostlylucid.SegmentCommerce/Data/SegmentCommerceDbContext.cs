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

    // Products & Catalog
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<ProductVariationEntity> ProductVariations => Set<ProductVariationEntity>();
    public DbSet<SellerEntity> Sellers => Set<SellerEntity>();
    public DbSet<TaxonomyNodeEntity> TaxonomyNodes => Set<TaxonomyNodeEntity>();
    public DbSet<ProductTaxonomyEntity> ProductTaxonomy => Set<ProductTaxonomyEntity>();

    // Stores
    public DbSet<StoreEntity> Stores => Set<StoreEntity>();
    public DbSet<StoreUserEntity> StoreUsers => Set<StoreUserEntity>();
    public DbSet<StoreProductEntity> StoreProducts => Set<StoreProductEntity>();

    // Profiles (Zero PII)
    public DbSet<SessionProfileEntity> SessionProfiles => Set<SessionProfileEntity>();
    public DbSet<PersistentProfileEntity> PersistentProfiles => Set<PersistentProfileEntity>();
    public DbSet<ProfileKeyEntity> ProfileKeys => Set<ProfileKeyEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();

    // Legacy (to be migrated)
    public DbSet<VisitorProfileEntity> VisitorProfiles => Set<VisitorProfileEntity>();
    public DbSet<InteractionEventEntity> InteractionEvents => Set<InteractionEventEntity>();

    // Embeddings
    public DbSet<ProductEmbeddingEntity> ProductEmbeddings => Set<ProductEmbeddingEntity>();
    public DbSet<InterestEmbeddingEntity> InterestEmbeddings => Set<InterestEmbeddingEntity>();

    // Queue
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<JobQueueEntity> JobQueue => Set<JobQueueEntity>();

    // Orders (customer PII stored in transient cache only)
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("ltree");

        // ============ PRODUCTS ============
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsTrending);
            entity.HasIndex(e => e.IsFeatured);
            entity.HasIndex(e => e.Handle).IsUnique();
            entity.HasIndex(e => e.CategoryPath).HasMethod("gist");

            entity.Property(e => e.CategoryPath).HasColumnType("ltree");
            entity.Property(e => e.Tags).HasColumnType("text[]");
            entity.Property(e => e.Subcategory).HasMaxLength(100);

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
            entity.OwnsOne(e => e.Attributes, b => b.ToJson());

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductTaxonomyEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.TaxonomyNodeId }).IsUnique();
        });

        // ============ STORES ============
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

        // ============ PROFILES (ZERO PII) ============
        modelBuilder.Entity<SessionProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionKey).IsUnique();
            entity.HasIndex(e => e.PersistentProfileId);
            entity.HasIndex(e => e.ExpiresAt);

            // GIN indexes for JSONB querying
            entity.HasIndex(e => e.Interests).HasMethod("gin");
            entity.HasIndex(e => e.Signals).HasMethod("gin");

            entity.Property(e => e.Interests).HasColumnType("jsonb");
            entity.Property(e => e.Signals).HasColumnType("jsonb");
            entity.Property(e => e.ViewedProducts).HasColumnType("jsonb");
            entity.Property(e => e.Context).HasColumnType("jsonb");
        });

        modelBuilder.Entity<PersistentProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.ProfileKey).IsUnique();
            entity.HasIndex(e => e.IdentificationMode);
            entity.HasIndex(e => e.Segments);
            entity.HasIndex(e => e.LastSeenAt);

            // GIN indexes for JSONB querying
            entity.HasIndex(e => e.Interests).HasMethod("gin");
            entity.HasIndex(e => e.Affinities).HasMethod("gin");
            entity.HasIndex(e => e.LlmSegments).HasMethod("gin");

            entity.Property(e => e.Interests).HasColumnType("jsonb");
            entity.Property(e => e.Affinities).HasColumnType("jsonb");
            entity.Property(e => e.BrandAffinities).HasColumnType("jsonb");
            entity.Property(e => e.PricePreferences).HasColumnType("jsonb");
            entity.Property(e => e.Traits).HasColumnType("jsonb");
            entity.Property(e => e.LlmSegments).HasColumnType("jsonb");

            // Vector embedding for similarity
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });

        modelBuilder.Entity<ProfileKeyEntity>(entity =>
        {
            entity.HasIndex(e => new { e.KeyValue, e.KeyType }).IsUnique();
            entity.HasIndex(e => e.ProfileId);
        });

        modelBuilder.Entity<SignalEntity>(entity =>
        {
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.SignalType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Category);
            entity.OwnsOne(e => e.Context, b => b.ToJson());
        });

        // ============ LEGACY ============
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
            entity.OwnsOne(e => e.Metadata, b => b.ToJson());
        });

        // ============ EMBEDDINGS ============
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

        // ============ ORDERS ============
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.ProfileId);
            entity.HasIndex(e => e.SessionKey);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.OwnsOne(e => e.Metadata, b => b.ToJson());

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItemEntity>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProductId);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Variation)
                .WithMany()
                .HasForeignKey(e => e.VariationId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
