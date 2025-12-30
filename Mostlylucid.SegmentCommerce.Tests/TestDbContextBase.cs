using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Tests;

/// <summary>
/// Base test DbContext that configures PostgreSQL-specific types for in-memory testing.
/// The strongly-typed JSONB classes (InteractionMetadata, SignalContext, etc.) are 
/// configured as owned entities with ToJson() in the base DbContext, which works with InMemory.
/// Only vector types and remaining Dictionary types need to be ignored.
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
        
        // ============ IGNORE VECTOR TYPES (pgvector) ============
        // These can't be simulated in InMemory - they're for similarity search only
        
        modelBuilder.Entity<ProductEmbeddingEntity>()
            .Ignore(e => e.Embedding);
        
        modelBuilder.Entity<InterestEmbeddingEntity>()
            .Ignore(e => e.Embedding);
        
        modelBuilder.Entity<PersistentProfileEntity>()
            .Ignore(e => e.Embedding);
        
        // ============ IGNORE REMAINING JSONB DICTIONARY TYPES ============
        // These still use Dictionary types and need to be ignored for InMemory
        
        // VisitorProfileEntity - legacy, uses Dictionary<string, InterestWeightData>
        modelBuilder.Entity<VisitorProfileEntity>()
            .Ignore(e => e.Interests);
        
        // PersistentProfileEntity - has complex JSONB dictionary properties
        modelBuilder.Entity<PersistentProfileEntity>()
            .Ignore(e => e.Interests)
            .Ignore(e => e.Affinities)
            .Ignore(e => e.BrandAffinities)
            .Ignore(e => e.PricePreferences)
            .Ignore(e => e.Traits)
            .Ignore(e => e.LlmSegments);
        
        // SessionProfileEntity - has JSONB dictionary properties
        modelBuilder.Entity<SessionProfileEntity>()
            .Ignore(e => e.Interests)
            .Ignore(e => e.Signals)
            .Ignore(e => e.ViewedProducts)
            .Ignore(e => e.Context);
        
        // DemoUserEntity - has JSONB properties
        modelBuilder.Entity<DemoUserEntity>()
            .Ignore(e => e.Interests)
            .Ignore(e => e.BrandAffinities)
            .Ignore(e => e.PreferredTags);
        
        // SegmentEntity - has JSONB properties
        modelBuilder.Entity<SegmentEntity>()
            .Ignore(e => e.Rules)
            .Ignore(e => e.Tags);
    }
}
