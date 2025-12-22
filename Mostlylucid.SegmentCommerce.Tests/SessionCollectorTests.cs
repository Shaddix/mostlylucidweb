using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class SessionCollectorTests
{
    private static SegmentCommerceDbContext CreateContext() => TestDbContextBase.Create();

    private static IConfiguration CreateConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Profiles:SessionTimeoutMinutes"] = "30"
        })
        .Build();

    [Fact]
    public async Task RecordSignalAsync_CreatesNewSession_WhenNotExists()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        var input = new SessionSignalInput(
            SessionKey: "new-session-key",
            SignalType: SignalTypes.PageView,
            Category: "tech",
            ProductId: null,
            Weight: null,
            Context: null,
            PageUrl: "/products/tech");

        var session = await collector.RecordSignalAsync(input);

        Assert.NotNull(session);
        Assert.Equal("new-session-key", session.SessionKey);
        Assert.Equal(1, session.SignalCount);
        Assert.Equal(1, session.PageViews);
    }

    [Fact]
    public async Task RecordSignalAsync_UpdatesExistingSession()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        var input1 = new SessionSignalInput("session-1", SignalTypes.PageView, "tech", null, null, null, "/");
        var input2 = new SessionSignalInput("session-1", SignalTypes.ProductView, "tech", 123, null, null, "/products/123");

        await collector.RecordSignalAsync(input1);
        var session = await collector.RecordSignalAsync(input2);

        Assert.Equal(2, session.SignalCount);
        Assert.Equal(1, session.PageViews);
        Assert.Equal(1, session.ProductViews);
    }

    [Fact]
    public async Task RecordSignalAsync_TracksInterests_ByCategory()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        await collector.RecordSignalAsync(new SessionSignalInput("session-interests", SignalTypes.ProductView, "tech", 1, null, null, null));
        await collector.RecordSignalAsync(new SessionSignalInput("session-interests", SignalTypes.ProductView, "tech", 2, null, null, null));
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-interests", SignalTypes.ProductView, "fashion", 3, null, null, null));

        Assert.True(session.Interests.ContainsKey("tech"));
        Assert.True(session.Interests.ContainsKey("fashion"));
        Assert.True(session.Interests["tech"] > session.Interests["fashion"]);
    }

    [Fact]
    public async Task RecordSignalAsync_TracksViewedProducts()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        await collector.RecordSignalAsync(new SessionSignalInput("session-views", SignalTypes.ProductView, "tech", 100, null, null, null));
        await collector.RecordSignalAsync(new SessionSignalInput("session-views", SignalTypes.ProductView, "tech", 200, null, null, null));
        // View same product again - should not duplicate
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-views", SignalTypes.ProductView, "tech", 100, null, null, null));

        Assert.Equal(2, session.ViewedProducts.Count);
        Assert.Contains(100, session.ViewedProducts);
        Assert.Contains(200, session.ViewedProducts);
        Assert.Equal(2, session.ProductViews); // Only counts unique
    }

    [Fact]
    public async Task RecordSignalAsync_TracksCartAdds()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        await collector.RecordSignalAsync(new SessionSignalInput("session-cart", SignalTypes.AddToCart, "tech", 1, null, null, null));
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-cart", SignalTypes.AddToCart, "tech", 2, null, null, null));

        Assert.Equal(2, session.CartAdds);
    }

    [Fact]
    public async Task RecordSignalAsync_AppliesBaseWeight_WhenNotSpecified()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        // Add to cart has base weight of 0.35
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-weight", SignalTypes.AddToCart, "tech", 1, null, null, null));

        Assert.Equal(0.35, session.TotalWeight, precision: 2);
    }

    [Fact]
    public async Task RecordSignalAsync_UsesCustomWeight_WhenSpecified()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-custom", SignalTypes.ProductView, "tech", 1, 0.99, null, null));

        Assert.Equal(0.99, session.TotalWeight, precision: 2);
    }

    [Fact]
    public async Task RecordSignalAsync_ExtendsSessionExpiry()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        var first = await collector.RecordSignalAsync(new SessionSignalInput("session-expiry", SignalTypes.PageView, null, null, null, null, null));
        var originalExpiry = first.ExpiresAt;

        await Task.Delay(10); // Small delay

        var second = await collector.RecordSignalAsync(new SessionSignalInput("session-expiry", SignalTypes.PageView, null, null, null, null, null));

        Assert.True(second.ExpiresAt >= originalExpiry);
        Assert.True(second.LastActivityAt > first.StartedAt);
    }

    [Fact]
    public async Task ElevateToProfileAsync_MergesInterests()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        // Create session with interests
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-elevate", SignalTypes.ProductView, "tech", 1, 0.5, null, null));
        await collector.RecordSignalAsync(new SessionSignalInput("session-elevate", SignalTypes.ProductView, "fashion", 2, 0.3, null, null));

        // Create profile
        var profile = new PersistentProfileEntity { ProfileKey = "test-profile" };
        context.PersistentProfiles.Add(profile);
        await context.SaveChangesAsync();

        // Elevate
        await collector.ElevateToProfileAsync(session, profile);

        Assert.True(profile.Interests.ContainsKey("tech"));
        Assert.True(profile.Interests.ContainsKey("fashion"));
        Assert.Equal(1, profile.TotalSessions);
        Assert.True(session.IsElevated);
    }

    [Fact]
    public async Task ElevateToProfileAsync_DoesNotDuplicate_IfAlreadyElevated()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-no-dup", SignalTypes.ProductView, "tech", 1, 0.5, null, null));

        var profile = new PersistentProfileEntity { ProfileKey = "test-profile-2" };
        context.PersistentProfiles.Add(profile);
        await context.SaveChangesAsync();

        // Elevate twice
        await collector.ElevateToProfileAsync(session, profile);
        await collector.ElevateToProfileAsync(session, profile);

        Assert.Equal(1, profile.TotalSessions);
    }

    [Fact]
    public async Task RecordSignalAsync_TracksSignalsByType()
    {
        using var context = CreateContext();
        var collector = new SessionCollector(context, NullLogger<SessionCollector>.Instance, CreateConfig());

        await collector.RecordSignalAsync(new SessionSignalInput("session-signals", SignalTypes.ProductView, "tech", 1, null, null, null));
        await collector.RecordSignalAsync(new SessionSignalInput("session-signals", SignalTypes.ProductView, "tech", 2, null, null, null));
        var session = await collector.RecordSignalAsync(new SessionSignalInput("session-signals", SignalTypes.AddToCart, "tech", 1, null, null, null));

        Assert.True(session.Signals.ContainsKey("tech"));
        Assert.Equal(2, session.Signals["tech"][SignalTypes.ProductView]);
        Assert.Equal(1, session.Signals["tech"][SignalTypes.AddToCart]);
    }

}
