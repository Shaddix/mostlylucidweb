using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Demo user with pre-built profile for demonstrating segmentation.
/// These are NOT real users - just personas for testing personalization.
/// </summary>
[Table("demo_users")]
public class DemoUserEntity
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Short persona description (e.g., "Tech Enthusiast", "Budget Shopper")
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("persona")]
    public string Persona { get; set; } = string.Empty;
    
    /// <summary>
    /// Longer description of the user's shopping behavior
    /// </summary>
    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Avatar color for UI display
    /// </summary>
    [MaxLength(20)]
    [Column("avatar_color")]
    public string AvatarColor { get; set; } = "blue";
    
    /// <summary>
    /// Link to pre-built persistent profile
    /// </summary>
    [Column("profile_id")]
    public Guid? ProfileId { get; set; }
    
    [ForeignKey(nameof(ProfileId))]
    public PersistentProfileEntity? Profile { get; set; }
    
    /// <summary>
    /// Category interests (for seeding profile)
    /// </summary>
    [Column("interests", TypeName = "jsonb")]
    public Dictionary<string, double> Interests { get; set; } = new();
    
    /// <summary>
    /// Brand affinities (for seeding profile)
    /// </summary>
    [Column("brand_affinities", TypeName = "jsonb")]
    public Dictionary<string, double> BrandAffinities { get; set; } = new();
    
    /// <summary>
    /// Price preferences
    /// </summary>
    [Column("price_min")]
    public decimal? PriceMin { get; set; }
    
    [Column("price_max")]
    public decimal? PriceMax { get; set; }
    
    /// <summary>
    /// Preferred tags for product recommendations
    /// </summary>
    [Column("preferred_tags", TypeName = "jsonb")]
    public List<string> PreferredTags { get; set; } = new();
    
    /// <summary>
    /// Segment flags for this demo user
    /// </summary>
    [Column("segments")]
    public ProfileSegments Segments { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Column("sort_order")]
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for demo user list in UI
/// </summary>
public record DemoUserDto(
    string Id,
    string Name,
    string Persona,
    string? Description,
    string AvatarColor,
    Dictionary<string, double> Interests,
    List<string> TopCategories
);
