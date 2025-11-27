namespace Mostlylucid.Chat.Server.Services;

/// <summary>
/// Service for tracking user presence (online/offline status)
/// </summary>
public interface IPresenceService
{
    /// <summary>
    /// Mark a user as online
    /// </summary>
    Task SetUserOnline(string userId, string userName, string userType, string connectionId);

    /// <summary>
    /// Mark a user as offline
    /// </summary>
    Task SetUserOffline(string userId);

    /// <summary>
    /// Check if any admins are online
    /// </summary>
    Task<bool> IsAnyAdminOnline();

    /// <summary>
    /// Get count of online admins
    /// </summary>
    Task<int> GetOnlineAdminCount();

    /// <summary>
    /// Get count of online users
    /// </summary>
    Task<int> GetOnlineUserCount();

    /// <summary>
    /// Update user's last seen timestamp
    /// </summary>
    Task UpdateLastSeen(string userId);
}
