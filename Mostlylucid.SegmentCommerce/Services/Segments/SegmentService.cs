using System.Text.Json;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Service for computing and managing dynamic segments.
/// Evaluates profiles against segment rules to determine fuzzy membership.
/// </summary>
public class SegmentService : ISegmentService
{
    private readonly List<SegmentDefinition> _segments = [];
    private readonly ILogger<SegmentService>? _logger;

    public SegmentService(ILogger<SegmentService>? logger = null)
    {
        _logger = logger;
        InitializeDefaultSegments();
    }

    /// <summary>
    /// Get all defined segments.
    /// </summary>
    public IReadOnlyList<SegmentDefinition> GetSegments() => _segments.AsReadOnly();

    /// <summary>
    /// Get a segment by ID.
    /// </summary>
    public SegmentDefinition? GetSegment(string id) => _segments.FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// Add a new segment definition.
    /// </summary>
    public void AddSegment(SegmentDefinition segment)
    {
        _segments.Add(segment);
    }

    /// <summary>
    /// Compute segment memberships for a profile.
    /// Returns all segments with their membership scores.
    /// </summary>
    public List<SegmentMembership> ComputeMemberships(ProfileData profile)
    {
        var memberships = new List<SegmentMembership>();

        foreach (var segment in _segments)
        {
            var membership = EvaluateSegment(profile, segment);
            memberships.Add(membership);
        }

        return memberships.OrderByDescending(m => m.Score).ToList();
    }

    /// <summary>
    /// Get segments where profile is a member (score >= threshold).
    /// </summary>
    public List<SegmentMembership> GetMemberSegments(ProfileData profile)
    {
        return ComputeMemberships(profile).Where(m => m.IsMember).ToList();
    }

    /// <summary>
    /// Evaluate a profile against a single segment.
    /// </summary>
    public SegmentMembership EvaluateSegment(ProfileData profile, SegmentDefinition segment)
    {
        var ruleScores = new List<RuleScore>();
        
        foreach (var rule in segment.Rules)
        {
            var (score, actualValue) = EvaluateRule(profile, rule);
            ruleScores.Add(new RuleScore
            {
                RuleDescription = rule.Description ?? $"{rule.Field} {rule.Operator} {rule.Value}",
                Score = score,
                Weight = rule.Weight,
                ActualValue = actualValue
            });
        }

        // Combine rule scores based on combination method
        double finalScore = segment.Combination switch
        {
            RuleCombination.All => ruleScores.Count > 0 ? ruleScores.Min(r => r.Score) : 0,
            RuleCombination.Any => ruleScores.Count > 0 ? ruleScores.Max(r => r.Score) : 0,
            RuleCombination.Weighted => ComputeWeightedScore(ruleScores),
            _ => 0
        };

        return new SegmentMembership
        {
            SegmentId = segment.Id,
            SegmentName = segment.Name,
            SegmentColor = segment.Color,
            SegmentIcon = segment.Icon,
            Score = Math.Round(finalScore, 3),
            IsMember = finalScore >= segment.MembershipThreshold,
            RuleScores = ruleScores
        };
    }

    private double ComputeWeightedScore(List<RuleScore> ruleScores)
    {
        if (ruleScores.Count == 0) return 0;
        
        var totalWeight = ruleScores.Sum(r => r.Weight);
        if (totalWeight <= 0) return 0;
        
        return ruleScores.Sum(r => r.Score * r.Weight) / totalWeight;
    }

    private (double Score, string? ActualValue) EvaluateRule(ProfileData profile, SegmentRule rule)
    {
        try
        {
            return rule.Type switch
            {
                RuleType.CategoryInterest => EvaluateCategoryInterest(profile, rule),
                RuleType.BrandAffinity => EvaluateBrandAffinity(profile, rule),
                RuleType.PriceRange => EvaluatePriceRange(profile, rule),
                RuleType.Trait => EvaluateTrait(profile, rule),
                RuleType.Statistic => EvaluateStatistic(profile, rule),
                RuleType.TagAffinity => EvaluateTagAffinity(profile, rule),
                RuleType.Recency => EvaluateRecency(profile, rule),
                _ => (0, null)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error evaluating rule {RuleType} on field {Field}", rule.Type, rule.Field);
            return (0, "error");
        }
    }

    private (double, string?) EvaluateCategoryInterest(ProfileData profile, SegmentRule rule)
    {
        var category = rule.Field.Replace("interests.", "");
        if (!profile.Interests.TryGetValue(category, out var interest))
            return (0, "0");

        var threshold = Convert.ToDouble(rule.Value ?? 0.5);
        var score = rule.Operator switch
        {
            RuleOperator.GreaterThan => interest > threshold ? Math.Min(1, interest / threshold) : interest / threshold * 0.5,
            RuleOperator.GreaterOrEqual => interest >= threshold ? Math.Min(1, interest / threshold) : interest / threshold * 0.5,
            RuleOperator.LessThan => interest < threshold ? 1 - (interest / threshold) : 0,
            _ => interest >= threshold ? 1 : interest / threshold
        };

        return (Math.Max(0, Math.Min(1, score)), interest.ToString("F2"));
    }

    private (double, string?) EvaluateBrandAffinity(ProfileData profile, SegmentRule rule)
    {
        var brand = rule.Field.Replace("brands.", "");
        if (!profile.BrandAffinities.TryGetValue(brand, out var affinity))
            return (0, "0");

        var threshold = Convert.ToDouble(rule.Value ?? 0.5);
        var score = affinity >= threshold ? 1 : affinity / threshold;
        return (score, affinity.ToString("F2"));
    }

    private (double, string?) EvaluatePriceRange(ProfileData profile, SegmentRule rule)
    {
        if (profile.PricePreferences == null)
            return (0, "unknown");

        var (min, max) = rule.Value switch
        {
            string s when s.Contains('-') => ParseRange(s),
            JsonElement je when je.ValueKind == JsonValueKind.String => ParseRange(je.GetString() ?? "0-1000"),
            _ => (0m, 1000m)
        };

        var avgPrice = (profile.PricePreferences.MinObserved + profile.PricePreferences.MaxObserved) / 2 ?? 0;
        
        // Score based on how well the profile's price range overlaps
        var inRange = avgPrice >= min && avgPrice <= max;
        var score = inRange ? 1.0 : 
            avgPrice < min ? Math.Max(0, 1 - (double)((min - avgPrice) / min)) :
            Math.Max(0, 1 - (double)((avgPrice - max) / max));

        return (score, $"${avgPrice:F0}");
    }

    private static (decimal min, decimal max) ParseRange(string range)
    {
        var parts = range.Split('-');
        return (
            decimal.TryParse(parts[0], out var min) ? min : 0,
            decimal.TryParse(parts.Length > 1 ? parts[1] : parts[0], out var max) ? max : 1000
        );
    }

    private (double, string?) EvaluateTrait(ProfileData profile, SegmentRule rule)
    {
        var trait = rule.Field.Replace("traits.", "");
        if (!profile.Traits.TryGetValue(trait, out var hasTrait))
            return (0, "false");

        var expectedValue = rule.Value switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => true
        };

        var matches = hasTrait == expectedValue;
        return (matches ? 1 : 0, hasTrait.ToString());
    }

    private (double, string?) EvaluateStatistic(ProfileData profile, SegmentRule rule)
    {
        var statValue = rule.Field switch
        {
            "stats.totalPurchases" or "totalPurchases" => profile.TotalPurchases,
            "stats.totalSessions" or "totalSessions" => profile.TotalSessions,
            "stats.totalSignals" or "totalSignals" => profile.TotalSignals,
            "stats.totalCartAdds" or "totalCartAdds" => profile.TotalCartAdds,
            _ => 0
        };

        var threshold = Convert.ToDouble(rule.Value ?? 0);
        var score = rule.Operator switch
        {
            RuleOperator.GreaterThan => statValue > threshold ? 1 : statValue / Math.Max(1, threshold),
            RuleOperator.GreaterOrEqual => statValue >= threshold ? 1 : statValue / Math.Max(1, threshold),
            RuleOperator.LessThan => statValue < threshold ? 1 : Math.Max(0, 1 - (statValue - threshold) / threshold),
            RuleOperator.LessOrEqual => statValue <= threshold ? 1 : Math.Max(0, 1 - (statValue - threshold) / threshold),
            RuleOperator.Equal => statValue == threshold ? 1 : 0,
            _ => 0
        };

        return (Math.Max(0, Math.Min(1, score)), statValue.ToString());
    }

    private (double, string?) EvaluateTagAffinity(ProfileData profile, SegmentRule rule)
    {
        var tag = rule.Field.Replace("affinities.", "");
        if (!profile.Affinities.TryGetValue(tag, out var affinity))
            return (0, "0");

        var threshold = Convert.ToDouble(rule.Value ?? 0.3);
        var score = affinity >= threshold ? Math.Min(1, affinity) : affinity / threshold;
        return (score, affinity.ToString("F2"));
    }

    private (double, string?) EvaluateRecency(ProfileData profile, SegmentRule rule)
    {
        var daysSinceActive = (DateTime.UtcNow - profile.LastSeenAt).TotalDays;
        var threshold = Convert.ToDouble(rule.Value ?? 7);

        var score = rule.Operator switch
        {
            RuleOperator.LessThan => daysSinceActive < threshold ? 1 : Math.Max(0, 1 - (daysSinceActive - threshold) / threshold),
            RuleOperator.GreaterThan => daysSinceActive > threshold ? 1 : daysSinceActive / threshold,
            _ => Math.Max(0, 1 - daysSinceActive / Math.Max(1, threshold * 2))
        };

        return (score, $"{daysSinceActive:F0} days");
    }

    private void InitializeDefaultSegments()
    {
        // High-Value Customers
        _segments.Add(new SegmentDefinition
        {
            Id = "high-value",
            Name = "High-Value Customers",
            Description = "Customers who make frequent purchases and spend above average",
            Icon = "💎",
            Color = "#8b5cf6",
            MembershipThreshold = 0.4,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.GreaterOrEqual, Value = 3, Weight = 0.4, Description = "3+ purchases" },
                new() { Type = RuleType.PriceRange, Field = "priceRange", Value = "100-10000", Weight = 0.3, Description = "High price range" },
                new() { Type = RuleType.Recency, Field = "lastSeen", Operator = RuleOperator.LessThan, Value = 30, Weight = 0.3, Description = "Active in last 30 days" }
            ],
            Tags = ["purchase-behavior", "value"]
        });

        // Tech Enthusiasts
        _segments.Add(new SegmentDefinition
        {
            Id = "tech-enthusiast",
            Name = "Tech Enthusiasts",
            Description = "Users with strong interest in technology products",
            Icon = "🔧",
            Color = "#3b82f6",
            MembershipThreshold = 0.35,
            Rules =
            [
                new() { Type = RuleType.CategoryInterest, Field = "interests.tech", Operator = RuleOperator.GreaterOrEqual, Value = 0.4, Weight = 0.6, Description = "Tech interest > 40%" },
                new() { Type = RuleType.TagAffinity, Field = "affinities.gadgets", Operator = RuleOperator.GreaterOrEqual, Value = 0.2, Weight = 0.2, Description = "Likes gadgets" },
                new() { Type = RuleType.TagAffinity, Field = "affinities.electronics", Operator = RuleOperator.GreaterOrEqual, Value = 0.2, Weight = 0.2, Description = "Likes electronics" }
            ],
            Tags = ["category", "tech"]
        });

        // Fashion Forward
        _segments.Add(new SegmentDefinition
        {
            Id = "fashion-forward",
            Name = "Fashion Forward",
            Description = "Style-conscious shoppers interested in clothing and accessories",
            Icon = "👗",
            Color = "#ec4899",
            MembershipThreshold = 0.35,
            Rules =
            [
                new() { Type = RuleType.CategoryInterest, Field = "interests.fashion", Operator = RuleOperator.GreaterOrEqual, Value = 0.4, Weight = 0.7, Description = "Fashion interest > 40%" },
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.GreaterOrEqual, Value = 2, Weight = 0.3, Description = "Multiple visits" }
            ],
            Tags = ["category", "fashion"]
        });

        // Bargain Hunters
        _segments.Add(new SegmentDefinition
        {
            Id = "bargain-hunter",
            Name = "Bargain Hunters",
            Description = "Price-sensitive shoppers who love deals and discounts",
            Icon = "🏷️",
            Color = "#22c55e",
            MembershipThreshold = 0.3,
            Rules =
            [
                new() { Type = RuleType.PriceRange, Field = "priceRange", Value = "0-75", Weight = 0.5, Description = "Low price preference" },
                new() { Type = RuleType.Trait, Field = "traits.prefersDeals", Value = true, Weight = 0.3, Description = "Prefers deals" },
                new() { Type = RuleType.Statistic, Field = "totalCartAdds", Operator = RuleOperator.GreaterThan, Value = 5, Weight = 0.2, Description = "Shops around" }
            ],
            Tags = ["purchase-behavior", "price-sensitive"]
        });

        // New Visitors
        _segments.Add(new SegmentDefinition
        {
            Id = "new-visitor",
            Name = "New Visitors",
            Description = "First-time or recent visitors exploring the store",
            Icon = "👋",
            Color = "#f59e0b",
            MembershipThreshold = 0.5,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.LessOrEqual, Value = 2, Weight = 0.5, Description = "Few sessions" },
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.Equal, Value = 0, Weight = 0.5, Description = "No purchases yet" }
            ],
            Tags = ["lifecycle", "acquisition"]
        });

        // Cart Abandoners
        _segments.Add(new SegmentDefinition
        {
            Id = "cart-abandoner",
            Name = "Cart Abandoners",
            Description = "Users who add items to cart but don't complete purchase",
            Icon = "🛒",
            Color = "#ef4444",
            MembershipThreshold = 0.4,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalCartAdds", Operator = RuleOperator.GreaterOrEqual, Value = 3, Weight = 0.5, Description = "3+ cart adds" },
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.LessThan, Value = 2, Weight = 0.5, Description = "Few purchases" }
            ],
            Tags = ["purchase-behavior", "recovery"]
        });

        // Home & Living Enthusiasts
        _segments.Add(new SegmentDefinition
        {
            Id = "home-enthusiast",
            Name = "Home & Living Enthusiasts",
            Description = "Users interested in home decor and improvement",
            Icon = "🏠",
            Color = "#14b8a6",
            MembershipThreshold = 0.35,
            Rules =
            [
                new() { Type = RuleType.CategoryInterest, Field = "interests.home", Operator = RuleOperator.GreaterOrEqual, Value = 0.4, Weight = 0.7, Description = "Home interest > 40%" },
                new() { Type = RuleType.Recency, Field = "lastSeen", Operator = RuleOperator.LessThan, Value = 14, Weight = 0.3, Description = "Recently active" }
            ],
            Tags = ["category", "home"]
        });

        // Fitness & Sports Active
        _segments.Add(new SegmentDefinition
        {
            Id = "fitness-active",
            Name = "Fitness & Sports Active",
            Description = "Health and fitness focused shoppers",
            Icon = "🏃",
            Color = "#06b6d4",
            MembershipThreshold = 0.35,
            Rules =
            [
                new() { Type = RuleType.CategoryInterest, Field = "interests.sport", Operator = RuleOperator.GreaterOrEqual, Value = 0.4, Weight = 0.7, Description = "Sport interest > 40%" },
                new() { Type = RuleType.Trait, Field = "traits.healthConscious", Value = true, Weight = 0.3, Description = "Health conscious" }
            ],
            Tags = ["category", "sport", "lifestyle"]
        });

        // Loyal Customers
        _segments.Add(new SegmentDefinition
        {
            Id = "loyal-customer",
            Name = "Loyal Customers",
            Description = "Repeat customers who consistently engage with the brand",
            Icon = "⭐",
            Color = "#eab308",
            MembershipThreshold = 0.45,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.GreaterOrEqual, Value = 5, Weight = 0.4, Description = "5+ purchases" },
                new() { Type = RuleType.Statistic, Field = "totalSessions", Operator = RuleOperator.GreaterOrEqual, Value = 10, Weight = 0.3, Description = "10+ sessions" },
                new() { Type = RuleType.Recency, Field = "lastSeen", Operator = RuleOperator.LessThan, Value = 14, Weight = 0.3, Description = "Active recently" }
            ],
            Tags = ["lifecycle", "retention", "value"]
        });

        // Researchers
        _segments.Add(new SegmentDefinition
        {
            Id = "researcher",
            Name = "Researchers",
            Description = "Thorough shoppers who browse extensively before buying",
            Icon = "🔍",
            Color = "#6366f1",
            MembershipThreshold = 0.35,
            Rules =
            [
                new() { Type = RuleType.Statistic, Field = "totalSignals", Operator = RuleOperator.GreaterOrEqual, Value = 20, Weight = 0.5, Description = "High engagement" },
                new() { Type = RuleType.Trait, Field = "traits.browsesExtensively", Value = true, Weight = 0.3, Description = "Browses a lot" },
                new() { Type = RuleType.Statistic, Field = "totalPurchases", Operator = RuleOperator.LessOrEqual, Value = 3, Weight = 0.2, Description = "Careful buyer" }
            ],
            Tags = ["purchase-behavior", "engagement"]
        });
    }
}

/// <summary>
/// Profile data for segment evaluation.
/// Extracted from PersistentProfileEntity or generated for demo.
/// </summary>
public class ProfileData
{
    public Guid Id { get; set; }
    public string ProfileKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    
    public Dictionary<string, double> Interests { get; set; } = new();
    public Dictionary<string, double> Affinities { get; set; } = new();
    public Dictionary<string, double> BrandAffinities { get; set; } = new();
    public Dictionary<string, bool> Traits { get; set; } = new();
    public PricePreferences? PricePreferences { get; set; }
    
    public int TotalSessions { get; set; }
    public int TotalSignals { get; set; }
    public int TotalPurchases { get; set; }
    public int TotalCartAdds { get; set; }
    
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create ProfileData from a PersistentProfileEntity.
    /// </summary>
    public static ProfileData FromEntity(PersistentProfileEntity entity)
    {
        return new ProfileData
        {
            Id = entity.Id,
            ProfileKey = entity.ProfileKey,
            Interests = entity.Interests,
            Affinities = entity.Affinities,
            BrandAffinities = entity.BrandAffinities,
            Traits = entity.Traits,
            PricePreferences = entity.PricePreferences,
            TotalSessions = entity.TotalSessions,
            TotalSignals = entity.TotalSignals,
            TotalPurchases = entity.TotalPurchases,
            TotalCartAdds = entity.TotalCartAdds,
            LastSeenAt = entity.LastSeenAt,
            CreatedAt = entity.CreatedAt
        };
    }
}
