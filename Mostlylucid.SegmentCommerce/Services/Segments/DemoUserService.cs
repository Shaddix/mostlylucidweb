using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Service for managing demo user selection and "login as" functionality.
/// Allows exploring the site as a generated profile to see personalization in action.
/// </summary>
public class DemoUserService : IDemoUserService
{
    private const string DemoUserSessionKey = "DemoUserProfileId";
    private readonly SegmentCommerceDbContext _db;
    private readonly ISegmentService _segmentService;
    private readonly ILogger<DemoUserService>? _logger;

    public DemoUserService(
        SegmentCommerceDbContext db,
        ISegmentService segmentService,
        ILogger<DemoUserService>? logger = null)
    {
        _db = db;
        _segmentService = segmentService;
        _logger = logger;
    }

    /// <summary>
    /// Get a list of demo users with their segment summaries.
    /// Returns diverse profiles from different segments for exploration.
    /// </summary>
    public async Task<List<DemoUserSummary>> GetDemoUsersAsync(int count = 10)
    {
        // Get diverse profiles - try to get representatives from different segments
        var profiles = await _db.PersistentProfiles
            .OrderByDescending(p => p.TotalSignals) // More active profiles first
            .Take(count * 3) // Get extra to select diverse ones
            .ToListAsync();

        if (profiles.Count == 0)
            return [];

        // Compute segments and select diverse profiles
        var profilesWithSegments = new List<(PersistentProfileEntity Profile, List<SegmentMembership> Memberships)>();
        
        foreach (var profile in profiles)
        {
            var profileData = ProfileData.FromEntity(profile);
            var memberships = _segmentService.ComputeMemberships(profileData);
            profilesWithSegments.Add((Profile: profile, Memberships: memberships));
        }

        // Select diverse profiles - prioritize different primary segments
        var selectedProfiles = SelectDiverseProfiles(profilesWithSegments, count);

        return selectedProfiles.Select(p => CreateDemoUserSummary(p.Profile, p.Memberships)).ToList();
    }

    /// <summary>
    /// Get demo users filtered by a specific segment.
    /// </summary>
    public async Task<List<DemoUserSummary>> GetDemoUsersBySegmentAsync(string segmentId, int count = 5)
    {
        var segment = _segmentService.GetSegment(segmentId);
        if (segment == null)
            return [];

        var profiles = await _db.PersistentProfiles
            .OrderByDescending(p => p.TotalSignals)
            .Take(count * 5) // Get extra to filter
            .ToListAsync();

        var result = new List<DemoUserSummary>();

        foreach (var profile in profiles)
        {
            if (result.Count >= count) break;

            var profileData = ProfileData.FromEntity(profile);
            var memberships = _segmentService.ComputeMemberships(profileData);
            
            // Check if this profile is a member of the requested segment
            var segmentMembership = memberships.FirstOrDefault(m => m.SegmentId == segmentId);
            if (segmentMembership?.IsMember == true)
            {
                result.Add(CreateDemoUserSummary(profile, memberships));
            }
        }

        return result;
    }

    /// <summary>
    /// Login as a demo user - stores profile ID in session.
    /// </summary>
    public async Task<DemoLoginResult> LoginAsDemoUserAsync(Guid profileId, HttpContext context)
    {
        var profile = await _db.PersistentProfiles.FindAsync(profileId);
        if (profile == null)
        {
            return new DemoLoginResult
            {
                Success = false,
                Error = "Profile not found"
            };
        }

        // Store in session
        context.Session.SetString(DemoUserSessionKey, profileId.ToString());

        var profileData = ProfileData.FromEntity(profile);
        var memberships = _segmentService.ComputeMemberships(profileData);

        _logger?.LogInformation("Demo login as profile {ProfileId} ({ProfileKey})", 
            profileId, profile.ProfileKey[..Math.Min(8, profile.ProfileKey.Length)]);

        return new DemoLoginResult
        {
            Success = true,
            Profile = CreateDemoUserSummary(profile, memberships),
            Segments = memberships.Where(m => m.IsMember).ToList()
        };
    }

    /// <summary>
    /// Logout from demo user mode.
    /// </summary>
    public void LogoutDemoUser(HttpContext context)
    {
        context.Session.Remove(DemoUserSessionKey);
        _logger?.LogInformation("Demo user logged out");
    }

    /// <summary>
    /// Get current demo user if logged in.
    /// </summary>
    public async Task<DemoUserContext?> GetCurrentDemoUserAsync(HttpContext context)
    {
        var profileIdStr = context.Session.GetString(DemoUserSessionKey);
        if (string.IsNullOrEmpty(profileIdStr) || !Guid.TryParse(profileIdStr, out var profileId))
            return null;

        var profile = await _db.PersistentProfiles.FindAsync(profileId);
        if (profile == null)
        {
            // Profile was deleted, clear session
            context.Session.Remove(DemoUserSessionKey);
            return null;
        }

        var profileData = ProfileData.FromEntity(profile);
        var memberships = _segmentService.ComputeMemberships(profileData);

        return new DemoUserContext
        {
            Profile = CreateDemoUserSummary(profile, memberships),
            Segments = memberships.Where(m => m.IsMember).ToList()
        };
    }

    /// <summary>
    /// Get the current demo user's profile ID (for use by other services).
    /// </summary>
    public Guid? GetCurrentDemoProfileId(HttpContext context)
    {
        var profileIdStr = context.Session.GetString(DemoUserSessionKey);
        if (string.IsNullOrEmpty(profileIdStr) || !Guid.TryParse(profileIdStr, out var profileId))
            return null;
        return profileId;
    }

    /// <summary>
    /// Check if currently browsing as a demo user.
    /// </summary>
    public bool IsDemoMode(HttpContext context)
    {
        return !string.IsNullOrEmpty(context.Session.GetString(DemoUserSessionKey));
    }

    private static List<(PersistentProfileEntity Profile, List<SegmentMembership> Memberships)> SelectDiverseProfiles(
        List<(PersistentProfileEntity Profile, List<SegmentMembership> Memberships)> candidates,
        int count)
    {
        var selected = new List<(PersistentProfileEntity, List<SegmentMembership>)>();
        var usedSegments = new HashSet<string>();

        // First pass: select one profile from each unique primary segment
        foreach (var candidate in candidates)
        {
            if (selected.Count >= count) break;

            var primarySegment = candidate.Memberships.FirstOrDefault(m => m.IsMember)?.SegmentId ?? "none";
            if (!usedSegments.Contains(primarySegment))
            {
                selected.Add(candidate);
                usedSegments.Add(primarySegment);
            }
        }

        // Second pass: fill remaining slots with any profiles
        foreach (var candidate in candidates)
        {
            if (selected.Count >= count) break;
            if (!selected.Contains(candidate))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static DemoUserSummary CreateDemoUserSummary(
        PersistentProfileEntity profile,
        List<SegmentMembership> memberships)
    {
        var primarySegment = memberships.FirstOrDefault(m => m.IsMember);
        var topInterests = profile.Interests
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => kv.Key)
            .ToList();

        return new DemoUserSummary
        {
            Id = profile.Id,
            ProfileKey = profile.ProfileKey[..Math.Min(8, profile.ProfileKey.Length)] + "...",
            DisplayName = GenerateDisplayName(profile, primarySegment),
            PrimarySegment = primarySegment != null ? new SegmentBadge
            {
                Id = primarySegment.SegmentId,
                Name = primarySegment.SegmentName,
                Icon = primarySegment.SegmentIcon,
                Color = primarySegment.SegmentColor,
                Score = primarySegment.Score
            } : null,
            SegmentCount = memberships.Count(m => m.IsMember),
            TopInterests = topInterests,
            Stats = new DemoUserStats
            {
                TotalPurchases = profile.TotalPurchases,
                TotalSessions = profile.TotalSessions,
                TotalSignals = profile.TotalSignals,
                DaysSinceLastSeen = (int)(DateTime.UtcNow - profile.LastSeenAt).TotalDays
            }
        };
    }

    private static string GenerateDisplayName(PersistentProfileEntity profile, SegmentMembership? primarySegment)
    {
        // Generate a friendly display name based on segment and interests
        var topInterest = profile.Interests.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        var segmentIcon = primarySegment?.SegmentIcon ?? "";
        
        return primarySegment?.SegmentName switch
        {
            "High-Value Customers" => $"{segmentIcon} VIP Shopper",
            "Tech Enthusiasts" => $"{segmentIcon} Tech Lover",
            "Fashion Forward" => $"{segmentIcon} Style Maven",
            "Bargain Hunters" => $"{segmentIcon} Deal Seeker",
            "New Visitors" => $"{segmentIcon} New Explorer",
            "Cart Abandoners" => $"{segmentIcon} Window Shopper",
            "Home & Living Enthusiasts" => $"{segmentIcon} Home Designer",
            "Fitness & Sports Active" => $"{segmentIcon} Fitness Fan",
            "Loyal Customers" => $"{segmentIcon} Loyal Regular",
            "Researchers" => $"{segmentIcon} Careful Buyer",
            _ => $"User {profile.Id.ToString()[..6]}"
        };
    }
}

#region View Models

/// <summary>
/// Summary of a demo user for selection UI.
/// </summary>
public class DemoUserSummary
{
    public Guid Id { get; set; }
    public string ProfileKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public SegmentBadge? PrimarySegment { get; set; }
    public int SegmentCount { get; set; }
    public List<string> TopInterests { get; set; } = [];
    public DemoUserStats Stats { get; set; } = new();
}

/// <summary>
/// Segment badge for display.
/// </summary>
public class SegmentBadge
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>
/// Stats for demo user display.
/// </summary>
public class DemoUserStats
{
    public int TotalPurchases { get; set; }
    public int TotalSessions { get; set; }
    public int TotalSignals { get; set; }
    public int DaysSinceLastSeen { get; set; }
}

/// <summary>
/// Result of demo login attempt.
/// </summary>
public class DemoLoginResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DemoUserSummary? Profile { get; set; }
    public List<SegmentMembership>? Segments { get; set; }
}

/// <summary>
/// Current demo user context.
/// </summary>
public class DemoUserContext
{
    public DemoUserSummary Profile { get; set; } = new();
    public List<SegmentMembership> Segments { get; set; } = [];
}

#endregion
