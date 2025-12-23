using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Tests;

/// <summary>
/// Base test DbContext that ignores all PostgreSQL-specific properties (JSONB, vector, etc.)
/// for in-memory testing.
/// </summary>
public class TestDbContextBase : SegmentCommerceDbContext
{
    public TestDbContextBase(DbContextOptions<SegmentCommerceDbContext> options) : base(options) { }

    public static SegmentCommerceDbContext Create()
    {
        var serviceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddLogging()
            .BuildServiceProvider();
        
        var options = new DbContextOptionsBuilder<SegmentCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseInternalServiceProvider(serviceProvider)
            .Options;
        return new TestDbContextBase(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Ignore PostgreSQL-specific JSONB/vector types for in-memory testing
        
        // InteractionEventEntity
        modelBuilder.Entity<InteractionEventEntity>()
            .Ignore(e => e.Metadata);
        
        // SignalEntity
        modelBuilder.Entity<SignalEntity>()
            .Ignore(e => e.Context);
        
        // ProductEmbeddingEntity
        modelBuilder.Entity<ProductEmbeddingEntity>()
            .Ignore(e => e.Embedding);
        
        // InterestEmbeddingEntity
        modelBuilder.Entity<InterestEmbeddingEntity>()
            .Ignore(e => e.Embedding);
        
        // VisitorProfileEntity
        modelBuilder.Entity<VisitorProfileEntity>()
            .Ignore(e => e.Interests);
        
        // PersistentProfileEntity - has many JSONB properties
        modelBuilder.Entity<PersistentProfileEntity>()
            .Ignore(e => e.Interests)
            .Ignore(e => e.Affinities)
            .Ignore(e => e.BrandAffinities)
            .Ignore(e => e.PricePreferences)
            .Ignore(e => e.Traits)
            .Ignore(e => e.LlmSegments)
            .Ignore(e => e.Embedding);
        
        // SessionProfileEntity - has JSONB properties
        modelBuilder.Entity<SessionProfileEntity>()
            .Ignore(e => e.Interests)
            .Ignore(e => e.Signals)
            .Ignore(e => e.ViewedProducts)
            .Ignore(e => e.Context);
        
        // OrderEntity - has JSONB metadata
        modelBuilder.Entity<OrderEntity>()
            .Ignore(e => e.Metadata);
        
        // TaxonomyNodeEntity - has JSONB attributes
        modelBuilder.Entity<TaxonomyNodeEntity>()
            .Ignore(e => e.Attributes);
    }
}
