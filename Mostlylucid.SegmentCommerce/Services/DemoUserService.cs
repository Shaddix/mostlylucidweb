using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// Service for demo persona authentication and profile linking.
/// Demo personas are pre-built user profiles for demonstrating segmentation.
/// Different from the Segments.IDemoUserService which handles generated profiles.
/// </summary>
public interface IDemoPersonaService
{
    /// <summary>
    /// Get all demo users for display.
    /// </summary>
    Task<List<DemoUserDto>> GetDemoUsersAsync();
    
    /// <summary>
    /// Get a specific demo user by ID.
    /// </summary>
    Task<DemoUserEntity?> GetDemoUserAsync(string demoUserId);
    
    /// <summary>
    /// Login as a demo user - links current session to demo user's profile.
    /// Creates the profile if it doesn't exist, populating with demo user's interests.
    /// </summary>
    Task<DemoLoginResult> LoginAsync(string sessionKey, string demoUserId);
    
    /// <summary>
    /// Logout demo user - unlinks session from profile but keeps session data.
    /// </summary>
    Task LogoutAsync(string sessionKey);
    
    /// <summary>
    /// Get the current demo user for a session (if logged in).
    /// </summary>
    Task<DemoUserEntity?> GetCurrentDemoUserAsync(string sessionKey);
}

public record DemoLoginResult(
    bool Success,
    string? Error,
    DemoUserEntity? DemoUser,
    PersistentProfileEntity? Profile
);

public class DemoPersonaService : IDemoPersonaService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ISessionProfileCache _sessionCache;
    private readonly ILogger<DemoPersonaService> _logger;

    public DemoPersonaService(
        SegmentCommerceDbContext db, 
        ISessionProfileCache sessionCache,
        ILogger<DemoPersonaService> logger)
    {
        _db = db;
        _sessionCache = sessionCache;
        _logger = logger;
    }

    public async Task<List<DemoUserDto>> GetDemoUsersAsync()
    {
        // Fetch entities first, then project in memory (LINQ-to-Objects)
        // The OrderByDescending(kv => kv.Value) cannot be translated to SQL
        var entities = await _db.DemoUsers
            .OrderBy(u => u.SortOrder)
            .ToListAsync();

        // Return default demo users if none in database
        if (!entities.Any())
        {
            return GetDefaultDemoUsers();
        }

        // Project to DTOs in memory
        return entities.Select(u => new DemoUserDto(
            u.Id,
            u.Name,
            u.Persona,
            u.Description,
            u.AvatarColor,
            u.Interests,
            u.Interests.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key).ToList()
        )).ToList();
    }

    public async Task<DemoUserEntity?> GetDemoUserAsync(string demoUserId)
    {
        // Try database first
        var user = await _db.DemoUsers
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == demoUserId);

        if (user != null) return user;

        // Fall back to creating from defaults
        var defaults = GetDefaultDemoUsers();
        var defaultDto = defaults.FirstOrDefault(d => d.Id == demoUserId);
        
        if (defaultDto == null) return null;

        // Create entity from default
        user = new DemoUserEntity
        {
            Id = defaultDto.Id,
            Name = defaultDto.Name,
            Persona = defaultDto.Persona,
            Description = defaultDto.Description,
            AvatarColor = defaultDto.AvatarColor,
            Interests = defaultDto.Interests,
            SortOrder = defaults.IndexOf(defaultDto)
        };

        // Add default brand affinities and tags based on interests
        SetDefaultAffinities(user);

        _db.DemoUsers.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<DemoLoginResult> LoginAsync(string sessionKey, string demoUserId)
    {
        try
        {
            // Get or create demo user
            var demoUser = await GetDemoUserAsync(demoUserId);
            if (demoUser == null)
            {
                return new DemoLoginResult(false, $"Demo user '{demoUserId}' not found", null, null);
            }

            // Get or create persistent profile for this demo user
            var profile = await GetOrCreateDemoProfileAsync(demoUser);

            // Get session from in-memory cache
            var session = _sessionCache.Get(sessionKey);

            if (session == null)
            {
                return new DemoLoginResult(false, "Session not found", null, null);
            }

            // Link session to demo user's profile (in cache only)
            session.PersistentProfileId = profile.Id;
            session.IdentificationMode = ProfileIdentificationMode.Identity;
            _sessionCache.Set(sessionKey, session);

            // Update demo user's profile link if not set (this goes to DB)
            if (!demoUser.ProfileId.HasValue)
            {
                demoUser.ProfileId = profile.Id;
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Demo user {DemoUserId} logged in, session {SessionKey} linked to profile {ProfileId}",
                demoUserId, sessionKey, profile.Id);

            return new DemoLoginResult(true, null, demoUser, profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login demo user {DemoUserId}", demoUserId);
            return new DemoLoginResult(false, ex.Message, null, null);
        }
    }

    public async Task LogoutAsync(string sessionKey)
    {
        var session = _sessionCache.Get(sessionKey);

        if (session == null) return;

        // Unlink from profile but keep session (in cache only)
        session.PersistentProfileId = null;
        session.IdentificationMode = ProfileIdentificationMode.None;
        _sessionCache.Set(sessionKey, session);

        _logger.LogInformation("Demo user logged out from session {SessionKey}", sessionKey);
        await Task.CompletedTask; // Keep async signature
    }

    public async Task<DemoUserEntity?> GetCurrentDemoUserAsync(string sessionKey)
    {
        var session = _sessionCache.Get(sessionKey);

        if (session?.PersistentProfileId == null) return null;

        // Find demo user with this profile (from DB)
        return await _db.DemoUsers
            .FirstOrDefaultAsync(u => u.ProfileId == session.PersistentProfileId);
    }

    /// <summary>
    /// Get or create persistent profile for demo user, populated with their interests.
    /// </summary>
    private async Task<PersistentProfileEntity> GetOrCreateDemoProfileAsync(DemoUserEntity demoUser)
    {
        // Check if demo user already has a profile
        if (demoUser.ProfileId.HasValue)
        {
            var existing = await _db.PersistentProfiles.FindAsync(demoUser.ProfileId.Value);
            if (existing != null) return existing;
        }

        // Check if profile exists by key
        var profileKey = $"demo:{demoUser.Id}";
        var profile = await _db.PersistentProfiles
            .FirstOrDefaultAsync(p => p.ProfileKey == profileKey);

        if (profile != null) return profile;

        // Create new profile with demo user's preferences
        profile = new PersistentProfileEntity
        {
            ProfileKey = profileKey,
            IdentificationMode = ProfileIdentificationMode.Identity,
            Interests = new Dictionary<string, double>(demoUser.Interests),
            BrandAffinities = new Dictionary<string, double>(demoUser.BrandAffinities),
            Affinities = BuildAffinitiesFromInterests(demoUser),
            Segments = demoUser.Segments,
            PricePreferences = new PricePreferences
            {
                MinObserved = demoUser.PriceMin,
                MaxObserved = demoUser.PriceMax,
                AveragePurchase = (demoUser.PriceMin + demoUser.PriceMax) / 2,
                PrefersDeals = demoUser.Persona == "Budget Hunter",
                PrefersLuxury = demoUser.Persona == "Tech Enthusiast" || demoUser.Persona == "Fashionista"
            },
            TotalSessions = 10, // Simulate some history
            TotalSignals = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-30), // Simulate established user
            LastSeenAt = DateTime.UtcNow
        };

        _db.PersistentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // Update demo user with profile link
        demoUser.ProfileId = profile.Id;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created persistent profile {ProfileId} for demo user {DemoUserId}",
            profile.Id, demoUser.Id);

        return profile;
    }

    /// <summary>
    /// Build subcategory affinities from main interests.
    /// </summary>
    private static Dictionary<string, double> BuildAffinitiesFromInterests(DemoUserEntity demoUser)
    {
        var affinities = new Dictionary<string, double>();

        // Map interests to subcategories
        var subcategoryMap = new Dictionary<string, string[]>
        {
            ["tech"] = ["audio", "computing", "mobile", "wearables", "smart-home"],
            ["fashion"] = ["accessories", "footwear", "watches", "sunglasses"],
            ["home"] = ["office", "kitchen", "lighting", "furniture"],
            ["sport"] = ["fitness-equipment", "hydration", "outdoor"],
            ["books"] = ["e-readers", "reading-accessories"],
            ["food"] = ["coffee-tea", "food-storage", "gourmet"]
        };

        foreach (var (category, score) in demoUser.Interests)
        {
            if (subcategoryMap.TryGetValue(category, out var subcats))
            {
                foreach (var subcat in subcats)
                {
                    // Distribute interest across subcategories with some variation
                    affinities[subcat] = score * (0.5 + Random.Shared.NextDouble() * 0.5);
                }
            }
        }

        return affinities;
    }

    /// <summary>
    /// Set default brand affinities and tags based on interests.
    /// </summary>
    private static void SetDefaultAffinities(DemoUserEntity user)
    {
        var brandsByCategory = new Dictionary<string, string[]>
        {
            ["tech"] = ["SoundCore", "AudioTech", "KeyForge", "FitPulse", "LumiSmart"],
            ["fashion"] = ["TimeCraft", "SunStyle", "LeatherCraft", "StridePro"],
            ["home"] = ["DeskPro", "ErgoSeat", "LightWorks", "BrewMaster"],
            ["sport"] = ["YogaPro", "ResistPro", "HydroFlow", "IronGrip"],
            ["books"] = ["ReadEasy", "PageTurn", "BookLight"],
            ["food"] = ["GrindMaster", "TeaTime", "FreshKeep"]
        };

        var tagsByCategory = new Dictionary<string, string[]>
        {
            ["tech"] = ["wireless", "bluetooth", "smart-home", "premium"],
            ["fashion"] = ["classic", "luxury", "minimalist"],
            ["home"] = ["ergonomic", "modern", "eco-friendly"],
            ["sport"] = ["fitness", "outdoor", "hydration"],
            ["books"] = ["portable", "reading"],
            ["food"] = ["organic", "gourmet"]
        };

        // Set brand affinities from top interests
        foreach (var (category, score) in user.Interests.OrderByDescending(kv => kv.Value).Take(2))
        {
            if (brandsByCategory.TryGetValue(category, out var brands))
            {
                foreach (var brand in brands.Take(3))
                {
                    user.BrandAffinities[brand] = score * (0.6 + Random.Shared.NextDouble() * 0.4);
                }
            }

            if (tagsByCategory.TryGetValue(category, out var tags))
            {
                user.PreferredTags.AddRange(tags.Take(3));
            }
        }

        // Set price range based on persona
        (user.PriceMin, user.PriceMax) = user.Persona switch
        {
            "Tech Enthusiast" => (50m, 500m),
            "Fashionista" => (30m, 300m),
            "Home Improver" => (20m, 200m),
            "Fitness Fanatic" => (15m, 150m),
            "Budget Hunter" => (5m, 50m),
            "Bookworm" => (10m, 100m),
            _ => (20m, 150m)
        };

        // Set segment flags
        user.Segments = user.Persona switch
        {
            "Tech Enthusiast" => ProfileSegments.TechEnthusiast | ProfileSegments.HighEngagement | ProfileSegments.DesktopUser,
            "Fashionista" => ProfileSegments.FashionFocused | ProfileSegments.HighEngagement | ProfileSegments.MobileUser,
            "Home Improver" => ProfileSegments.HomeInterested | ProfileSegments.MediumEngagement | ProfileSegments.WeekendShopper,
            "Fitness Fanatic" => ProfileSegments.SportActive | ProfileSegments.HighEngagement | ProfileSegments.MorningActive,
            "Budget Hunter" => ProfileSegments.Bargain | ProfileSegments.MediumEngagement | ProfileSegments.Researcher,
            "Bookworm" => ProfileSegments.BookLover | ProfileSegments.LowEngagement | ProfileSegments.EveningActive,
            _ => ProfileSegments.None
        };
    }

    /// <summary>
    /// Default demo users if none exist in database.
    /// </summary>
    private static List<DemoUserDto> GetDefaultDemoUsers() =>
    [
        new("tech-enthusiast", "Alex", "Tech Enthusiast",
            "Early adopter who loves the latest gadgets and smart home tech.",
            "blue",
            new() { { "tech", 0.9 }, { "home", 0.3 }, { "books", 0.2 } },
            ["tech", "home", "books"]),

        new("fashionista", "Jordan", "Fashionista",
            "Trend-conscious shopper focused on style and accessories.",
            "pink",
            new() { { "fashion", 0.85 }, { "sport", 0.3 }, { "home", 0.2 } },
            ["fashion", "sport", "home"]),

        new("home-improver", "Sam", "Home Improver",
            "Always working on home projects and kitchen upgrades.",
            "green",
            new() { { "home", 0.8 }, { "tech", 0.35 }, { "food", 0.3 } },
            ["home", "tech", "food"]),

        new("fitness-fanatic", "Riley", "Fitness Fanatic",
            "Dedicated to health, wellness, and staying active.",
            "orange",
            new() { { "sport", 0.85 }, { "food", 0.4 }, { "tech", 0.25 } },
            ["sport", "food", "tech"]),

        new("budget-hunter", "Casey", "Budget Hunter",
            "Always looking for the best deals and value purchases.",
            "yellow",
            new() { { "home", 0.4 }, { "tech", 0.3 }, { "fashion", 0.3 } },
            ["home", "tech", "fashion"]),

        new("bookworm", "Morgan", "Bookworm",
            "Avid reader who loves e-readers and reading accessories.",
            "purple",
            new() { { "books", 0.9 }, { "tech", 0.3 }, { "home", 0.2 } },
            ["books", "tech", "home"])
    ];
}
