using System.Collections.Concurrent;
using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Services;

public class InMemoryConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();

    public void AddConnection(ConnectionInfo connectionInfo)
    {
        _connections[connectionInfo.ConnectionId] = connectionInfo;
    }

    public void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public ConnectionInfo? GetConnection(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connectionInfo);
        return connectionInfo;
    }

    public string? GetUserConnectionId(string conversationId)
    {
        var connection = _connections.Values
            .FirstOrDefault(c => c.ConversationId == conversationId && c.ConnectionType == "user");
        return connection?.ConnectionId;
    }

    public List<ConnectionInfo> GetAllConnections()
    {
        return _connections.Values.ToList();
    }
}
