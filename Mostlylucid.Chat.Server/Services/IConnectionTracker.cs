using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Services;

public interface IConnectionTracker
{
    void AddConnection(ConnectionInfo connectionInfo);
    void RemoveConnection(string connectionId);
    ConnectionInfo? GetConnection(string connectionId);
    string? GetUserConnectionId(string conversationId);
    List<ConnectionInfo> GetAllConnections();
}
