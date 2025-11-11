using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Mostlylucid.SecureChat.Demo.Hubs;

public class SecureChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, ChatSession> Sessions = new();
    private readonly ILogger<SecureChatHub> _logger;

    public SecureChatHub(ILogger<SecureChatHub> logger)
    {
        _logger = logger;
    }

    public async Task<AuthResult> AuthenticateClient(string codeword)
    {
        _logger.LogInformation("Authentication attempt for connection {ConnectionId}", Context.ConnectionId);

        // In a real system, this would check against a database or secure store
        // For demo purposes, we use a simple check
        var validCodeword = "SAFE2025"; // In production, this would be dynamic and time-limited

        if (codeword == validCodeword)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new ChatSession
            {
                SessionId = sessionId,
                ClientConnectionId = Context.ConnectionId,
                StartTime = DateTime.UtcNow,
                IsAuthenticated = true
            };

            Sessions.TryAdd(Context.ConnectionId, session);

            // Add to support group so support staff can see active sessions
            await Groups.AddToGroupAsync(Context.ConnectionId, "authenticated-users");

            _logger.LogInformation("Authentication successful for session {SessionId}", sessionId);

            // Notify support that new session is available
            await Clients.Group("support-staff").SendAsync("NewSessionAvailable", sessionId, DateTime.UtcNow);

            return new AuthResult { Success = true, SessionId = sessionId };
        }

        _logger.LogWarning("Authentication failed for connection {ConnectionId}", Context.ConnectionId);
        return new AuthResult { Success = false };
    }

    public async Task SendMessage(string sessionId, string message)
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session) || !session.IsAuthenticated)
        {
            _logger.LogWarning("Unauthorized message attempt from {ConnectionId}", Context.ConnectionId);
            return;
        }

        _logger.LogInformation("Message from client in session {SessionId}", sessionId);

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            Message = message,
            Timestamp = DateTime.UtcNow,
            FromSupport = false
        };

        // Send to support staff assigned to this session
        await Clients.Group($"session-{sessionId}").SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task<JoinSessionResult> JoinSessionAsSupport(string sessionId)
    {
        _logger.LogInformation("Support joining session {SessionId}", sessionId);

        // Find the client connection for this session
        var clientSession = Sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
        if (clientSession == null)
        {
            return new JoinSessionResult { Success = false, Message = "Session not found" };
        }

        // Add support to session group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "support-staff");

        clientSession.SupportConnectionId = Context.ConnectionId;

        // Notify client that support has joined
        await Clients.Client(clientSession.ClientConnectionId).SendAsync("SupportJoined");

        _logger.LogInformation("Support {ConnectionId} joined session {SessionId}", Context.ConnectionId, sessionId);

        return new JoinSessionResult { Success = true, Message = "Joined session" };
    }

    public async Task SendMessageAsSupport(string sessionId, string message)
    {
        _logger.LogInformation("Support message for session {SessionId}", sessionId);

        var clientSession = Sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
        if (clientSession == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return;
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            Message = message,
            Timestamp = DateTime.UtcNow,
            FromSupport = true
        };

        // Send to client
        await Clients.Client(clientSession.ClientConnectionId).SendAsync("ReceiveMessage", chatMessage);

        // Echo back to all support staff in this session
        await Clients.Group($"session-{sessionId}").SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task EndSession(string sessionId)
    {
        _logger.LogInformation("Ending session {SessionId}", sessionId);

        var clientSession = Sessions.Values.FirstOrDefault(s => s.SessionId == sessionId);
        if (clientSession != null)
        {
            // Notify client
            await Clients.Client(clientSession.ClientConnectionId).SendAsync("SessionEnded");

            // Notify support
            await Clients.Group($"session-{sessionId}").SendAsync("SessionEnded");

            // Cleanup
            Sessions.TryRemove(clientSession.ClientConnectionId, out _);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Sessions.TryRemove(Context.ConnectionId, out var session))
        {
            _logger.LogInformation("Client disconnected from session {SessionId}", session.SessionId);

            // Notify support
            await Clients.Group($"session-{session.SessionId}").SendAsync("ClientDisconnected", session.SessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

public class ChatSession
{
    public string SessionId { get; set; } = string.Empty;
    public string ClientConnectionId { get; set; } = string.Empty;
    public string? SupportConnectionId { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsAuthenticated { get; set; }
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? SessionId { get; set; }
}

public class JoinSessionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool FromSupport { get; set; }
}
