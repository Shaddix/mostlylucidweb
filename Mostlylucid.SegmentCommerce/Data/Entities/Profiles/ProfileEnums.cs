namespace Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

/// <summary>
/// How a persistent profile is identified.
/// </summary>
public enum ProfileIdentificationMode
{
    /// <summary>
    /// No persistent identification - session only.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Identified by client-side browser fingerprint hash.
    /// Zero-cookie, privacy-preserving.
    /// </summary>
    Fingerprint = 1,
    
    /// <summary>
    /// Identified by optional tracking cookie.
    /// Requires user consent.
    /// </summary>
    Cookie = 2,
    
    /// <summary>
    /// Identified by logged-in user ID.
    /// Most reliable, highest trust.
    /// </summary>
    Identity = 3
}

/// <summary>
/// Segments a profile can belong to.
/// Segments are computed from profile signals and attributes.
/// </summary>
[Flags]
public enum ProfileSegments : long
{
    None = 0,
    
    // Purchase behaviour
    NewVisitor = 1 << 0,
    ReturningVisitor = 1 << 1,
    HighValue = 1 << 2,
    Bargain = 1 << 3,
    
    // Engagement level
    LowEngagement = 1 << 4,
    MediumEngagement = 1 << 5,
    HighEngagement = 1 << 6,
    
    // Category affinity (top interests)
    TechEnthusiast = 1 << 10,
    FashionFocused = 1 << 11,
    HomeInterested = 1 << 12,
    SportActive = 1 << 13,
    BookLover = 1 << 14,
    FoodFocused = 1 << 15,
    
    // Shopping patterns
    BrowseOnly = 1 << 20,
    CartAbandoner = 1 << 21,
    QuickBuyer = 1 << 22,
    Researcher = 1 << 23,
    
    // Time patterns
    WeekdayShopper = 1 << 25,
    WeekendShopper = 1 << 26,
    MorningActive = 1 << 27,
    EveningActive = 1 << 28,
    
    // Device/context
    MobileUser = 1 << 30,
    DesktopUser = 1 << 31
}

/// <summary>
/// Signal types that can be elevated from session to persistent profile.
/// </summary>
public static class ElevatableSignals
{
    /// <summary>
    /// Signals with weight above this threshold get elevated.
    /// </summary>
    public const double ElevationThreshold = 0.15;
    
    /// <summary>
    /// Signal types that should always be elevated regardless of weight.
    /// </summary>
    public static readonly HashSet<string> AlwaysElevate = new()
    {
        SignalTypes.Purchase,
        SignalTypes.AddToCart,
        SignalTypes.AddToWishlist,
        SignalTypes.Review
    };
    
    /// <summary>
    /// Check if a signal should be elevated to persistent profile.
    /// </summary>
    public static bool ShouldElevate(string signalType, double weight)
    {
        return AlwaysElevate.Contains(signalType) || weight >= ElevationThreshold;
    }
}
