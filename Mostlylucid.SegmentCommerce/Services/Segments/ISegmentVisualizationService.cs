namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Interface for segment visualization data generation.
/// </summary>
public interface ISegmentVisualizationService
{
    /// <summary>
    /// Get visualization data for all profiles with segment memberships.
    /// </summary>
    Task<VisualizationData> GetVisualizationDataAsync(int? limit = 500);

    /// <summary>
    /// Get a single profile with full segment analysis for detail view.
    /// </summary>
    Task<ProfileDetailView?> GetProfileDetailAsync(Guid profileId);
}
