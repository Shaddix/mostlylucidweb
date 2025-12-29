using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// A dynamically defined segment stored in the database.
/// Segments are discovered from data patterns and named by LLM or admin.
/// </summary>
[Table("segments")]
public class SegmentEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// URL-friendly slug for the segment.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name (LLM-generated or admin-set).
    /// </summary>
    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of who belongs to this segment (LLM-generated or admin-set).
    /// </summary>
    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Icon or emoji for visual representation.
    /// </summary>
    [MaxLength(50)]
    [Column("icon")]
    public string Icon { get; set; } = "👤";

    /// <summary>
    /// CSS color for visualization.
    /// </summary>
    [MaxLength(20)]
    [Column("color")]
    public string Color { get; set; } = "#6366f1";

    /// <summary>
    /// Segment type for categorization.
    /// </summary>
    [Column("segment_type")]
    public SegmentType Type { get; set; } = SegmentType.CategoryBased;

    /// <summary>
    /// Rules that determine segment membership (stored as JSONB).
    /// </summary>
    [Column("rules", TypeName = "jsonb")]
    public List<SegmentRuleData> Rules { get; set; } = [];

    /// <summary>
    /// How rules are combined: All (AND), Any (OR), Weighted (sum).
    /// </summary>
    [Column("rule_combination")]
    public RuleCombinationType RuleCombination { get; set; } = RuleCombinationType.Weighted;

    /// <summary>
    /// Minimum score to be considered a member (0-1).
    /// </summary>
    [Column("membership_threshold")]
    public double MembershipThreshold { get; set; } = 0.3;

    /// <summary>
    /// Tags for filtering/grouping segments.
    /// </summary>
    [Column("tags", TypeName = "jsonb")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Whether this is a system-generated or user-created segment.
    /// </summary>
    [Column("is_system")]
    public bool IsSystem { get; set; } = true;

    /// <summary>
    /// Whether this segment is active.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order for UI.
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// LLM model used to generate name/description.
    /// </summary>
    [MaxLength(100)]
    [Column("llm_model")]
    public string? LlmModel { get; set; }

    /// <summary>
    /// Prompt used to generate this segment (for regeneration).
    /// </summary>
    [Column("generation_prompt")]
    public string? GenerationPrompt { get; set; }

    /// <summary>
    /// Approximate member count (cached, updated periodically).
    /// </summary>
    [Column("member_count")]
    public int MemberCount { get; set; }

    /// <summary>
    /// When member count was last computed.
    /// </summary>
    [Column("member_count_updated_at")]
    public DateTime? MemberCountUpdatedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Rule data stored as JSONB.
/// </summary>
public class SegmentRuleData
{
    /// <summary>
    /// Type of rule.
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Field to evaluate (e.g., "interests.tech", "stats.totalPurchases").
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Operator for comparison.
    /// </summary>
    public string Operator { get; set; } = "gte";

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

public enum SegmentType
{
    /// <summary>
    /// Based on category/interest patterns.
    /// </summary>
    CategoryBased = 0,

    /// <summary>
    /// Based on purchase behavior.
    /// </summary>
    Behavioral = 1,

    /// <summary>
    /// Based on lifecycle stage (new, returning, churning).
    /// </summary>
    Lifecycle = 2,

    /// <summary>
    /// Based on price sensitivity.
    /// </summary>
    PriceBased = 3,

    /// <summary>
    /// Custom segment defined by admin.
    /// </summary>
    Custom = 10
}

public enum RuleCombinationType
{
    /// <summary>
    /// All rules must match (AND). Score = min(scores).
    /// </summary>
    All = 0,

    /// <summary>
    /// Any rule must match (OR). Score = max(scores).
    /// </summary>
    Any = 1,

    /// <summary>
    /// Rules are weighted and summed. Score = sum(rule.weight * rule.score) / sum(rule.weight).
    /// </summary>
    Weighted = 2
}
