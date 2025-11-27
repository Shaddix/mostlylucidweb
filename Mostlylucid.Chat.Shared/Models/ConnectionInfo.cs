namespace Mostlylucid.Chat.Shared.Models;

public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "user"; // "user" or "admin"
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public string? ConversationId { get; set; }
}
