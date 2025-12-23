using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class InteractionServiceTests
{
    private static SegmentCommerceDbContext CreateContext() => TestDbContextBase.Create();

    [Fact]
    public async Task RecordEventAsync_CreatesEvent()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);

        await service.RecordEventAsync(
            sessionId: "session-123",
            eventType: EventTypes.View,
            productId: 1,
            category: "tech");

        var events = await context.InteractionEvents.ToListAsync();
        Assert.Single(events);
        Assert.Equal("session-123", events[0].SessionId);
        Assert.Equal(EventTypes.View, events[0].EventType);
        Assert.Equal(1, events[0].ProductId);
        Assert.Equal("tech", events[0].Category);
    }

    [Fact]
    public async Task RecordEventAsync_StoresMetadata()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var metadata = new InteractionMetadata { ScrollDepth = 75, TimeOnPageSeconds = 120 };

        await service.RecordEventAsync(
            sessionId: "session-meta",
            eventType: EventTypes.Click,
            metadata: metadata);

        var evt = await context.InteractionEvents.FirstAsync();
        Assert.NotNull(evt.Metadata);
        Assert.Equal(75, evt.Metadata.ScrollDepth);
        Assert.Equal(120, evt.Metadata.TimeOnPageSeconds);
    }

    [Fact]
    public async Task RecordEventAsync_LinksToProfile()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var profileId = Guid.NewGuid();

        await service.RecordEventAsync(
            sessionId: "session-profile",
            eventType: EventTypes.AddToCart,
            profileId: profileId);

        var evt = await context.InteractionEvents.FirstAsync();
        Assert.Equal(profileId, evt.ProfileId);
    }

    [Fact]
    public async Task CreateProfileAsync_CreatesNewProfile()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var signature = new InterestSignature
        {
            Interests = new Dictionary<string, InterestWeight>
            {
                ["tech"] = new InterestWeight { Category = "tech", Weight = 0.8, ReinforcementCount = 3 }
            }
        };

        var profile = await service.CreateProfileAsync("token-123", signature);

        Assert.NotNull(profile);
        Assert.Equal("token-123", profile.ProfileToken);
        Assert.True(profile.Interests.ContainsKey("tech"));
    }

    [Fact]
    public async Task GetProfileByTokenAsync_ReturnsProfile_WhenExists()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var signature = new InterestSignature();

        await service.CreateProfileAsync("token-find", signature);
        var found = await service.GetProfileByTokenAsync("token-find");

        Assert.NotNull(found);
        Assert.Equal("token-find", found.ProfileToken);
    }

    [Fact]
    public async Task GetProfileByTokenAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);

        var found = await service.GetProfileByTokenAsync("non-existent");

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateProfileInterestsAsync_UpdatesInterests()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var initialSignature = new InterestSignature
        {
            Interests = new Dictionary<string, InterestWeight>
            {
                ["tech"] = new InterestWeight { Category = "tech", Weight = 0.5, ReinforcementCount = 1 }
            }
        };

        var profile = await service.CreateProfileAsync("token-update", initialSignature);

        var updatedSignature = new InterestSignature
        {
            Interests = new Dictionary<string, InterestWeight>
            {
                ["tech"] = new InterestWeight { Category = "tech", Weight = 0.9, ReinforcementCount = 5 },
                ["fashion"] = new InterestWeight { Category = "fashion", Weight = 0.3, ReinforcementCount = 2 }
            }
        };

        await service.UpdateProfileInterestsAsync(profile.Id, updatedSignature);

        var updated = await context.VisitorProfiles.FindAsync(profile.Id);
        Assert.Equal(2, updated!.Interests.Count);
        Assert.Equal(0.9, updated.Interests["tech"].Weight, 1);
    }

    [Fact]
    public async Task UpdateProfileInterestsAsync_IncrementsTotalVisits()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);
        var signature = new InterestSignature();

        var profile = await service.CreateProfileAsync("token-visits", signature);
        Assert.Equal(1, profile.TotalVisits); // First visit on creation

        await service.UpdateProfileInterestsAsync(profile.Id, signature);

        var updated = await context.VisitorProfiles.FindAsync(profile.Id);
        Assert.Equal(2, updated!.TotalVisits); // Incremented after update
    }

    [Fact]
    public void ProfileToSignature_ConvertsCorrectly()
    {
        var profile = new VisitorProfileEntity
        {
            ProfileToken = "test",
            Interests = new Dictionary<string, InterestWeightData>
            {
                ["tech"] = new InterestWeightData { Weight = 0.8, ReinforcementCount = 3, DecayRate = 0.1 }
            },
            LastSeenAt = DateTime.UtcNow,
            IsUnmasked = true
        };

        var signature = InteractionService.ProfileToSignature(profile);

        Assert.True(signature.IsPersistent);
        Assert.True(signature.IsUnmasked);
        Assert.Equal(0.8, signature.Interests["tech"].Weight, 1);
    }

    [Fact]
    public async Task GetCategoryStatsAsync_AggregatesCorrectly()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);

        // Create events
        await service.RecordEventAsync("s1", EventTypes.View, category: "tech");
        await service.RecordEventAsync("s1", EventTypes.View, category: "tech");
        await service.RecordEventAsync("s2", EventTypes.View, category: "tech");
        await service.RecordEventAsync("s1", EventTypes.Click, category: "tech");
        await service.RecordEventAsync("s1", EventTypes.AddToCart, category: "tech");

        var stats = await service.GetCategoryStatsAsync("tech");

        Assert.Equal("tech", stats.Category);
        Assert.Equal(3, stats.TotalViews);
        Assert.Equal(1, stats.TotalClicks);
        Assert.Equal(1, stats.TotalAddToCarts);
        Assert.Equal(2, stats.UniqueVisitors);
    }

    [Fact]
    public async Task GetCategoryStatsAsync_FiltersBySince()
    {
        using var context = CreateContext();
        var service = new InteractionService(context);

        // Create first event
        await service.RecordEventAsync("s1", EventTypes.View, category: "tech");
        
        // Wait a bit and capture the cutoff time
        await Task.Delay(50);
        var since = DateTime.UtcNow;
        await Task.Delay(50);
        
        // Create second event after the cutoff
        await service.RecordEventAsync("s2", EventTypes.View, category: "tech");

        var stats = await service.GetCategoryStatsAsync("tech", since);

        // Only the second event should be counted (created after 'since')
        Assert.Equal(1, stats.TotalViews);
    }

}
