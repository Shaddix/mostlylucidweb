using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class ProfileKeyServiceTests
{
    private static SegmentCommerceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SegmentCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestSegmentCommerceDbContext(options);
    }

    private static IConfiguration CreateConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Profiles:KeySecret"] = "unit-test-secret"
        })
        .Build();

    [Fact]
    public void GenerateKey_IsStable_ForSameInput()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest("fp", "cookie", "user");
        var key1 = service.GenerateKey(request);
        var key2 = service.GenerateKey(request);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesProfileAndKey()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest("fp", "cookie", null);
        var profileKey = await service.GetOrCreateAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(profileKey.KeyHash));
        Assert.NotNull(profileKey.Profile);
        Assert.Equal(profileKey.KeyHash, profileKey.Profile.ProfileKey);
    }

    private sealed class TestSegmentCommerceDbContext : SegmentCommerceDbContext
    {
        public TestSegmentCommerceDbContext(DbContextOptions<SegmentCommerceDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Mostlylucid.SegmentCommerce.Data.Entities.InteractionEventEntity>()
                .Ignore(e => e.Metadata);
            modelBuilder.Entity<Mostlylucid.SegmentCommerce.Data.Entities.Profiles.SignalEntity>()
                .Ignore(e => e.Context);
            modelBuilder.Entity<Mostlylucid.SegmentCommerce.Data.Entities.ProductEmbeddingEntity>()
                .Ignore(e => e.Embedding);
            modelBuilder.Entity<Mostlylucid.SegmentCommerce.Data.Entities.InterestEmbeddingEntity>()
                .Ignore(e => e.Embedding);
            modelBuilder.Entity<Mostlylucid.SegmentCommerce.Data.Entities.VisitorProfileEntity>()
                .Ignore(e => e.Interests);
        }
    }
}
