namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// A dynamically defined segment with rules and LLM-generated descriptions.
/// Segments are fuzzy - profiles have a membership score (0-1) rather than binary membership.
/// </summary>
public class SegmentDefinition
{
    /// <summary>
    /// Unique identifier for the segment.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Human-readable name (can be LLM-generated).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short description of who belongs to this segment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon or emoji for visual representation.
    /// </summary>
    public string Icon { get; set; } = "👤";

    /// <summary>
    /// CSS color for visualization.
    /// </summary>
    public string Color { get; set; } = "#6366f1";

    /// <summary>
    /// Rules that determine segment membership.
    /// </summary>
    public List<SegmentRule> Rules { get; set; } = [];

    /// <summary>
    /// How rules are combined: All (AND), Any (OR), Weighted (sum).
    /// </summary>
    public RuleCombination Combination { get; set; } = RuleCombination.Weighted;

    /// <summary>
    /// Minimum score to be considered a member (0-1).
    /// Lower = more members, fuzzier boundary.
    /// </summary>
    public double MembershipThreshold { get; set; } = 0.3;

    /// <summary>
    /// Whether this is a system-defined or user-created segment.
    /// </summary>
    public bool IsSystemSegment { get; set; } = true;

    /// <summary>
    /// When the segment definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tags for categorizing segments.
    /// </summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// A rule that contributes to segment membership score.
/// </summary>
public class SegmentRule
{
    /// <summary>
    /// Type of rule.
    /// </summary>
    public RuleType Type { get; set; }

    /// <summary>
    /// Field/dimension to evaluate (e.g., "interests.tech", "stats.totalPurchases").
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Operator for comparison.
    /// </summary>
    public RuleOperator Operator { get; set; }

    /// <summary>
    /// Value to compare against.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Weight of this rule in the final score (0-1).
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Human-readable description of this rule.
    /// </summary>
    public string? Description { get; set; }
}

public enum RuleType
{
    /// <summary>
    /// Check interest score for a category.
    /// </summary>
    CategoryInterest,

    /// <summary>
    /// Check brand affinity score.
    /// </summary>
    BrandAffinity,

    /// <summary>
    /// Check price preference range.
    /// </summary>
    PriceRange,

    /// <summary>
    /// Check behavioral trait (boolean).
    /// </summary>
    Trait,

    /// <summary>
    /// Check profile statistics (purchases, sessions, etc).
    /// </summary>
    Statistic,

    /// <summary>
    /// Check tag/subcategory affinity.
    /// </summary>
    TagAffinity,

    /// <summary>
    /// Check recency (days since last activity).
    /// </summary>
    Recency,

    /// <summary>
    /// Custom expression (advanced).
    /// </summary>
    Expression
}

public enum RuleOperator
{
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Equal,
    NotEqual,
    Contains,
    Between,
    In,
    NotIn
}

public enum RuleCombination
{
    /// <summary>
    /// All rules must match (AND). Score = min(scores).
    /// </summary>
    All,

    /// <summary>
    /// Any rule must match (OR). Score = max(scores).
    /// </summary>
    Any,

    /// <summary>
    /// Rules are weighted and summed. Score = sum(rule.weight * rule.score) / sum(rule.weight).
    /// </summary>
    Weighted
}

/// <summary>
/// Result of evaluating a profile against a segment.
/// </summary>
public class SegmentMembership
{
    public string SegmentId { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string SegmentColor { get; set; } = string.Empty;
    public string SegmentIcon { get; set; } = string.Empty;

    /// <summary>
    /// Membership score (0-1). Higher = stronger fit.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Whether score exceeds threshold (member vs non-member).
    /// </summary>
    public bool IsMember { get; set; }

    /// <summary>
    /// Individual rule scores for explanation.
    /// </summary>
    public List<RuleScore> RuleScores { get; set; } = [];

    /// <summary>
    /// Confidence level: Low, Medium, High, VeryHigh.
    /// </summary>
    public string Confidence => Score switch
    {
        >= 0.8 => "Very High",
        >= 0.6 => "High",
        >= 0.4 => "Medium",
        >= 0.2 => "Low",
        _ => "Very Low"
    };
}

public class RuleScore
{
    public string RuleDescription { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Weight { get; set; }
    public string? ActualValue { get; set; }
}
