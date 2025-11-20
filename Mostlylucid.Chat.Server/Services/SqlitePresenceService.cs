using Microsoft.EntityFrameworkCore;
using Mostlylucid.Chat.Server.Data;

namespace Mostlylucid.Chat.Server.Services;

/// <summary>
/// SQLite-backed presence service
/// Tracks which users and admins are currently online
/// </summary>
public class SqlitePresenceService : IPresenceService
{
    private readonly ChatDbContext _context;
    private readonly ILogger<SqlitePresenceService> _logger;

    public SqlitePresenceService(ChatDbContext context, ILogger<SqlitePresenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SetUserOnline(string userId, string userName, string userType, string connectionId)
    {
        var presence = await _context.Presence.FindAsync(userId);

        if (presence == null)
        {
            presence = new PresenceEntity
            {
                UserId = userId,
                UserName = userName,
                UserType = userType,
                IsOnline = true,
                ConnectionId = connectionId,
                LastSeen = DateTime.UtcNow
            };
            _context.Presence.Add(presence);
        }
        else
        {
            presence.IsOnline = true;
            presence.ConnectionId = connectionId;
            presence.LastSeen = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("{UserType} {UserName} is now online", userType, userName);
    }

    public async Task SetUserOffline(string userId)
    {
        var presence = await _context.Presence.FindAsync(userId);

        if (presence != null)
        {
            presence.IsOnline = false;
            presence.ConnectionId = null;
            presence.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("{UserType} {UserName} is now offline",
                presence.UserType, presence.UserName);
        }
    }

    public async Task<bool> IsAnyAdminOnline()
    {
        return await _context.Presence
            .AnyAsync(p => p.UserType == "admin" && p.IsOnline);
    }

    public async Task<int> GetOnlineAdminCount()
    {
        return await _context.Presence
            .CountAsync(p => p.UserType == "admin" && p.IsOnline);
    }

    public async Task<int> GetOnlineUserCount()
    {
        return await _context.Presence
            .CountAsync(p => p.UserType == "user" && p.IsOnline);
    }

    public async Task UpdateLastSeen(string userId)
    {
        var presence = await _context.Presence.FindAsync(userId);

        if (presence != null)
        {
            presence.LastSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
