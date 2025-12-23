namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Interface for segment computation and management.
/// </summary>
public interface ISegmentService
{
    /// <summary>
    /// Get all defined segments.
    /// </summary>
    IReadOnlyList<SegmentDefinition> GetSegments();

    /// <summary>
    /// Get a segment by ID.
    /// </summary>
    SegmentDefinition? GetSegment(string id);

    /// <summary>
    /// Add a new segment definition.
    /// </summary>
    void AddSegment(SegmentDefinition segment);

    /// <summary>
    /// Compute segment memberships for a profile.
    /// Returns all segments with their membership scores.
    /// </summary>
    List<SegmentMembership> ComputeMemberships(ProfileData profile);

    /// <summary>
    /// Get segments where profile is a member (score >= threshold).
    /// </summary>
    List<SegmentMembership> GetMemberSegments(ProfileData profile);

    /// <summary>
    /// Evaluate a profile against a single segment.
    /// </summary>
    SegmentMembership EvaluateSegment(ProfileData profile, SegmentDefinition segment);
}
