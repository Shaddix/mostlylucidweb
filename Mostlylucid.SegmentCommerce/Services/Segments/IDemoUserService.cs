namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Interface for demo user management and "login as" functionality.
/// </summary>
public interface IDemoUserService
{
    /// <summary>
    /// Get a list of demo users with their segment summaries.
    /// </summary>
    Task<List<DemoUserSummary>> GetDemoUsersAsync(int count = 10);

    /// <summary>
    /// Get demo users filtered by a specific segment.
    /// </summary>
    Task<List<DemoUserSummary>> GetDemoUsersBySegmentAsync(string segmentId, int count = 5);

    /// <summary>
    /// Login as a demo user - stores profile ID in session.
    /// </summary>
    Task<DemoLoginResult> LoginAsDemoUserAsync(Guid profileId, HttpContext context);

    /// <summary>
    /// Logout from demo user mode.
    /// </summary>
    void LogoutDemoUser(HttpContext context);

    /// <summary>
    /// Get current demo user if logged in.
    /// </summary>
    Task<DemoUserContext?> GetCurrentDemoUserAsync(HttpContext context);

    /// <summary>
    /// Get the current demo user's profile ID (for use by other services).
    /// </summary>
    Guid? GetCurrentDemoProfileId(HttpContext context);

    /// <summary>
    /// Check if currently browsing as a demo user.
    /// </summary>
    bool IsDemoMode(HttpContext context);
}
