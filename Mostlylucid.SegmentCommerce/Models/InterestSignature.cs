namespace Mostlylucid.SegmentCommerce.Models;

/// <summary>
/// Represents a user's interest profile - the core of zero-PII personalisation.
/// Each interest has a weight (0-1) that decays over time unless reinforced.
/// </summary>
public class InterestSignature
{
    public Dictionary<string, InterestWeight> Interests { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this signature is from an anonymous session or a persistent profile.
    /// </summary>
    public bool IsPersistent { get; set; }
    
    /// <summary>
    /// If the user has explicitly "unmasked" (linked their profile to an identity).
    /// </summary>
    public bool IsUnmasked { get; set; }
}

public class InterestWeight
{
    public string Category { get; set; } = string.Empty;
    public double Weight { get; set; }
    public DateTime LastReinforced { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// How many times this interest has been reinforced (views, clicks, purchases).
    /// </summary>
    public int ReinforcementCount { get; set; }
    
    /// <summary>
    /// The decay rate per day (0-1). Higher = faster decay.
    /// </summary>
    public double DecayRate { get; set; } = 0.1;
    
    /// <summary>
    /// Calculate the current effective weight after decay.
    /// </summary>
    public double EffectiveWeight
    {
        get
        {
            var daysSinceReinforced = (DateTime.UtcNow - LastReinforced).TotalDays;
            var decayFactor = Math.Pow(1 - DecayRate, daysSinceReinforced);
            return Weight * decayFactor;
        }
    }
    
    /// <summary>
    /// Whether this interest is actively decaying (not recently reinforced).
    /// </summary>
    public bool IsDecaying => (DateTime.UtcNow - LastReinforced).TotalHours > 1;
}

/// <summary>
/// Available interest categories for the demo.
/// </summary>
public static class InterestCategories
{
    public const string Tech = "tech";
    public const string Fashion = "fashion";
    public const string Home = "home";
    public const string Sport = "sport";
    public const string Books = "books";
    public const string Food = "food";
    
    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { Tech, "Technology" },
        { Fashion, "Fashion" },
        { Home, "Home & Garden" },
        { Sport, "Sports" },
        { Books, "Books" },
        { Food, "Food & Drink" }
    };
    
    public static readonly Dictionary<string, string> CssClasses = new()
    {
        { Tech, "interest-tech" },
        { Fashion, "interest-fashion" },
        { Home, "interest-home" },
        { Sport, "interest-sport" },
        { Books, "interest-books" },
        { Food, "interest-food" }
    };
}
