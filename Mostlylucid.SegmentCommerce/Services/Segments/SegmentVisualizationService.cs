using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Service for generating visualization data for segment exploration.
/// Produces 2D/3D projections of profile embeddings for interactive scatter plots.
/// </summary>
public class SegmentVisualizationService : ISegmentVisualizationService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ISegmentService _segmentService;
    private readonly ILogger<SegmentVisualizationService>? _logger;

    public SegmentVisualizationService(
        SegmentCommerceDbContext db,
        ISegmentService segmentService,
        ILogger<SegmentVisualizationService>? logger = null)
    {
        _db = db;
        _segmentService = segmentService;
        _logger = logger;
    }

    /// <summary>
    /// Get visualization data for all profiles with segment memberships.
    /// Uses dimensionality reduction (PCA-like) on interest vectors for 2D positioning.
    /// </summary>
    public async Task<VisualizationData> GetVisualizationDataAsync(int? limit = 500)
    {
        // Get profiles with their data
        var profiles = await _db.PersistentProfiles
            .OrderByDescending(p => p.LastSeenAt)
            .Take(limit ?? 500)
            .ToListAsync();

        if (profiles.Count == 0)
        {
            return new VisualizationData { Points = [], Segments = [] };
        }

        // Get all segment definitions
        var segments = _segmentService.GetSegments();

        // Generate 2D positions based on interest vectors
        var points = new List<ProfilePoint>();
        
        foreach (var profile in profiles)
        {
            var profileData = ProfileData.FromEntity(profile);
            var memberships = _segmentService.ComputeMemberships(profileData);
            var primarySegment = memberships.FirstOrDefault(m => m.IsMember);

            // Compute 2D position from interests (simple PCA-like projection)
            var (x, y) = ProjectToXY(profile.Interests, profile.Affinities);

            points.Add(new ProfilePoint
            {
                Id = profile.Id.ToString(),
                ProfileKey = profile.ProfileKey[..Math.Min(8, profile.ProfileKey.Length)] + "...",
                DisplayName = $"User {profile.Id.ToString()[..6]}",
                X = x,
                Y = y,
                PrimarySegmentId = primarySegment?.SegmentId,
                PrimarySegmentName = primarySegment?.SegmentName ?? "Unclassified",
                PrimarySegmentColor = primarySegment?.SegmentColor ?? "#94a3b8",
                Memberships = memberships.Where(m => m.Score > 0.1).Select(m => new MembershipSummary
                {
                    SegmentId = m.SegmentId,
                    SegmentName = m.SegmentName,
                    Score = m.Score,
                    IsMember = m.IsMember,
                    Color = m.SegmentColor
                }).ToList(),
                Stats = new ProfileStatsSummary
                {
                    TotalPurchases = profile.TotalPurchases,
                    TotalSessions = profile.TotalSessions,
                    TotalSignals = profile.TotalSignals,
                    LastSeenDaysAgo = (int)(DateTime.UtcNow - profile.LastSeenAt).TotalDays
                },
                TopInterests = profile.Interests
                    .OrderByDescending(kv => kv.Value)
                    .Take(3)
                    .Select(kv => new InterestSummary { Category = kv.Key, Score = kv.Value })
                    .ToList()
            });
        }

        // Normalize positions to [-1, 1] range for visualization
        NormalizePositions(points);

        // Compute segment stats
        var segmentStats = segments.Select(seg => new SegmentSummary
        {
            Id = seg.Id,
            Name = seg.Name,
            Description = seg.Description,
            Icon = seg.Icon,
            Color = seg.Color,
            MemberCount = points.Count(p => p.Memberships.Any(m => m.SegmentId == seg.Id && m.IsMember)),
            AverageScore = points
                .SelectMany(p => p.Memberships.Where(m => m.SegmentId == seg.Id))
                .Select(m => m.Score)
                .DefaultIfEmpty(0)
                .Average(),
            // Compute centroid for segment cluster
            Centroid = ComputeCentroid(points, seg.Id)
        }).ToList();

        return new VisualizationData
        {
            Points = points,
            Segments = segmentStats,
            TotalProfiles = profiles.Count,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Get a single profile with full segment analysis for detail view.
    /// </summary>
    public async Task<ProfileDetailView?> GetProfileDetailAsync(Guid profileId)
    {
        var profile = await _db.PersistentProfiles.FindAsync(profileId);
        if (profile == null) return null;

        var profileData = ProfileData.FromEntity(profile);
        var memberships = _segmentService.ComputeMemberships(profileData);

        return new ProfileDetailView
        {
            Id = profile.Id,
            ProfileKey = profile.ProfileKey,
            CreatedAt = profile.CreatedAt,
            LastSeenAt = profile.LastSeenAt,
            Interests = profile.Interests,
            Affinities = profile.Affinities.Take(10).ToDictionary(kv => kv.Key, kv => kv.Value),
            BrandAffinities = profile.BrandAffinities.Take(5).ToDictionary(kv => kv.Key, kv => kv.Value),
            Stats = new ProfileStatsSummary
            {
                TotalPurchases = profile.TotalPurchases,
                TotalSessions = profile.TotalSessions,
                TotalSignals = profile.TotalSignals,
                TotalCartAdds = profile.TotalCartAdds,
                LastSeenDaysAgo = (int)(DateTime.UtcNow - profile.LastSeenAt).TotalDays
            },
            Memberships = memberships
        };
    }

    /// <summary>
    /// Project interests to 2D space using weighted category axes.
    /// Categories are mapped to angular positions, intensity is radius.
    /// </summary>
    private static (double X, double Y) ProjectToXY(
        Dictionary<string, double> interests,
        Dictionary<string, double> affinities)
    {
        // Define category angles (radians) - spread evenly around circle
        var categoryAngles = new Dictionary<string, double>
        {
            ["tech"] = 0,
            ["fashion"] = Math.PI / 3,
            ["home"] = 2 * Math.PI / 3,
            ["sport"] = Math.PI,
            ["books"] = 4 * Math.PI / 3,
            ["food"] = 5 * Math.PI / 3
        };

        double x = 0, y = 0;
        double totalWeight = 0;

        foreach (var (category, score) in interests)
        {
            if (!categoryAngles.TryGetValue(category.ToLower(), out var angle))
            {
                // Hash unknown categories to an angle
                angle = (Math.Abs(category.GetHashCode()) % 360) * Math.PI / 180;
            }

            // Use score as weight/radius
            x += Math.Cos(angle) * score;
            y += Math.Sin(angle) * score;
            totalWeight += score;
        }

        // Add some jitter from affinities for visual separation
        var affinityHash = affinities.GetHashCode();
        x += Math.Sin(affinityHash) * 0.1;
        y += Math.Cos(affinityHash) * 0.1;

        // Normalize if we have weights
        if (totalWeight > 0)
        {
            x /= totalWeight;
            y /= totalWeight;
        }

        return (x, y);
    }

    private static void NormalizePositions(List<ProfilePoint> points)
    {
        if (points.Count == 0) return;

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        var rangeX = Math.Max(0.001, maxX - minX);
        var rangeY = Math.Max(0.001, maxY - minY);

        foreach (var point in points)
        {
            point.X = 2 * (point.X - minX) / rangeX - 1;
            point.Y = 2 * (point.Y - minY) / rangeY - 1;
        }
    }

    private static PointCoordinate? ComputeCentroid(List<ProfilePoint> points, string segmentId)
    {
        var members = points.Where(p => p.Memberships.Any(m => m.SegmentId == segmentId && m.IsMember)).ToList();
        if (members.Count == 0) return null;

        return new PointCoordinate
        {
            X = members.Average(p => p.X),
            Y = members.Average(p => p.Y)
        };
    }
}

#region View Models

public class VisualizationData
{
    public List<ProfilePoint> Points { get; set; } = [];
    public List<SegmentSummary> Segments { get; set; } = [];
    public int TotalProfiles { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class ProfilePoint
{
    public string Id { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string? PrimarySegmentId { get; set; }
    public string PrimarySegmentName { get; set; } = string.Empty;
    public string PrimarySegmentColor { get; set; } = string.Empty;
    public List<MembershipSummary> Memberships { get; set; } = [];
    public ProfileStatsSummary Stats { get; set; } = new();
    public List<InterestSummary> TopInterests { get; set; } = [];
}

public class MembershipSummary
{
    public string SegmentId { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public double Score { get; set; }
    public bool IsMember { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class ProfileStatsSummary
{
    public int TotalPurchases { get; set; }
    public int TotalSessions { get; set; }
    public int TotalSignals { get; set; }
    public int TotalCartAdds { get; set; }
    public int LastSeenDaysAgo { get; set; }
}

public class InterestSummary
{
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class SegmentSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public double AverageScore { get; set; }
    public PointCoordinate? Centroid { get; set; }
}

public class PointCoordinate
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class ProfileDetailView
{
    public Guid Id { get; set; }
    public string ProfileKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public Dictionary<string, double> Interests { get; set; } = new();
    public Dictionary<string, double> Affinities { get; set; } = new();
    public Dictionary<string, double> BrandAffinities { get; set; } = new();
    public ProfileStatsSummary Stats { get; set; } = new();
    public List<SegmentMembership> Memberships { get; set; } = [];
}

#endregion
