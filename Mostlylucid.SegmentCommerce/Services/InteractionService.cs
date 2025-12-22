using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// Service for tracking user interactions and managing interest signatures.
/// </summary>
public class InteractionService
{
    private readonly SegmentCommerceDbContext _context;

    public InteractionService(SegmentCommerceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Record an interaction event.
    /// </summary>
    public async Task RecordEventAsync(
        string sessionId,
        string eventType,
        int? productId = null,
        string? category = null,
        Guid? profileId = null,
        Dictionary<string, object>? metadata = null)
    {
        var evt = new InteractionEventEntity
        {
            SessionId = sessionId,
            EventType = eventType,
            ProductId = productId,
            Category = category,
            ProfileId = profileId,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };

        _context.InteractionEvents.Add(evt);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get or create a persistent visitor profile by token.
    /// </summary>
    public async Task<VisitorProfileEntity?> GetProfileByTokenAsync(string token)
    {
        return await _context.VisitorProfiles
            .FirstOrDefaultAsync(p => p.ProfileToken == token);
    }

    /// <summary>
    /// Create a new persistent profile.
    /// </summary>
    public async Task<VisitorProfileEntity> CreateProfileAsync(
        string token, 
        InterestSignature signature)
    {
        var profile = new VisitorProfileEntity
        {
            ProfileToken = token,
            Interests = signature.Interests.ToDictionary(
                kvp => kvp.Key,
                kvp => new InterestWeightData
                {
                    Weight = kvp.Value.Weight,
                    LastReinforced = kvp.Value.LastReinforced,
                    ReinforcementCount = kvp.Value.ReinforcementCount,
                    DecayRate = kvp.Value.DecayRate
                }),
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

        _context.VisitorProfiles.Add(profile);
        await _context.SaveChangesAsync();

        return profile;
    }

    /// <summary>
    /// Update a persistent profile's interests.
    /// </summary>
    public async Task UpdateProfileInterestsAsync(
        Guid profileId, 
        InterestSignature signature)
    {
        var profile = await _context.VisitorProfiles.FindAsync(profileId);
        if (profile == null) return;

        profile.Interests = signature.Interests.ToDictionary(
            kvp => kvp.Key,
            kvp => new InterestWeightData
            {
                Weight = kvp.Value.Weight,
                LastReinforced = kvp.Value.LastReinforced,
                ReinforcementCount = kvp.Value.ReinforcementCount,
                DecayRate = kvp.Value.DecayRate
            });
        profile.LastSeenAt = DateTime.UtcNow;
        profile.TotalVisits++;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Convert a persistent profile to an InterestSignature.
    /// </summary>
    public static InterestSignature ProfileToSignature(VisitorProfileEntity profile)
    {
        return new InterestSignature
        {
            Interests = profile.Interests.ToDictionary(
                kvp => kvp.Key,
                kvp => new InterestWeight
                {
                    Category = kvp.Key,
                    Weight = kvp.Value.Weight,
                    LastReinforced = kvp.Value.LastReinforced,
                    ReinforcementCount = kvp.Value.ReinforcementCount,
                    DecayRate = kvp.Value.DecayRate
                }),
            LastUpdated = profile.LastSeenAt,
            IsPersistent = true,
            IsUnmasked = profile.IsUnmasked
        };
    }

    /// <summary>
    /// Get aggregate statistics for a category (for analytics dashboard).
    /// </summary>
    public async Task<CategoryStats> GetCategoryStatsAsync(
        string category, 
        DateTime? since = null)
    {
        var query = _context.InteractionEvents
            .Where(e => e.Category == category);

        if (since.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= since.Value);
        }

        var stats = await query
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventType, x => x.Count);

        var uniqueVisitors = await query
            .Select(e => e.SessionId)
            .Distinct()
            .CountAsync();

        return new CategoryStats
        {
            Category = category,
            TotalViews = stats.GetValueOrDefault(EventTypes.View),
            TotalClicks = stats.GetValueOrDefault(EventTypes.Click),
            TotalAddToCarts = stats.GetValueOrDefault(EventTypes.AddToCart),
            TotalPurchases = stats.GetValueOrDefault(EventTypes.Purchase),
            UniqueVisitors = uniqueVisitors
        };
    }
}

public class CategoryStats
{
    public string Category { get; set; } = string.Empty;
    public int TotalViews { get; set; }
    public int TotalClicks { get; set; }
    public int TotalAddToCarts { get; set; }
    public int TotalPurchases { get; set; }
    public int UniqueVisitors { get; set; }
}
