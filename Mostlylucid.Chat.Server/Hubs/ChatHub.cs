using Microsoft.AspNetCore.SignalR;
using Mostlylucid.Chat.Shared.Models;
using Mostlylucid.Chat.Server.Services;

namespace Mostlylucid.Chat.Server.Hubs;

public class ChatHub : Hub
{
    private readonly IConversationService _conversationService;
    private readonly IConnectionTracker _connectionTracker;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IConversationService conversationService,
        IConnectionTracker connectionTracker,
        ILogger<ChatHub> logger)
    {
        _conversationService = conversationService;
        _connectionTracker = connectionTracker;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionInfo = _connectionTracker.GetConnection(Context.ConnectionId);
        if (connectionInfo != null)
        {
            _connectionTracker.RemoveConnection(Context.ConnectionId);

            // Notify admin clients if this was a user disconnecting
            if (connectionInfo.ConnectionType == "user")
            {
                await Clients.Group("admins").SendAsync("UserDisconnected", connectionInfo);
            }
        }

        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Register a user connection
    /// </summary>
    public async Task RegisterUser(string userName, string email, string sourceUrl)
    {
        var conversation = await _conversationService.CreateOrGetConversation(userName, email, sourceUrl);

        var connectionInfo = new ConnectionInfo
        {
            ConnectionId = Context.ConnectionId,
            UserId = conversation.UserId,
            UserName = userName,
            ConnectionType = "user",
            ConversationId = conversation.Id
        };

        _connectionTracker.AddConnection(connectionInfo);

        // Send conversation history to the user
        await Clients.Caller.SendAsync("ConversationHistory", conversation.Messages);

        // Notify all admin clients about new user
        await Clients.Group("admins").SendAsync("NewUserConnected", connectionInfo, conversation);

        _logger.LogInformation("User registered: {UserName} ({Email}) - Conversation: {ConversationId}",
            userName, email, conversation.Id);
    }

    /// <summary>
    /// Register an admin connection
    /// </summary>
    public async Task RegisterAdmin(string adminName)
    {
        var connectionInfo = new ConnectionInfo
        {
            ConnectionId = Context.ConnectionId,
            UserId = adminName,
            UserName = adminName,
            ConnectionType = "admin"
        };

        _connectionTracker.AddConnection(connectionInfo);
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

        // Send all active conversations to admin
        var activeConversations = _conversationService.GetActiveConversations();
        await Clients.Caller.SendAsync("ActiveConversations", activeConversations);

        _logger.LogInformation("Admin registered: {AdminName}", adminName);
    }

    /// <summary>
    /// Send a message from user
    /// </summary>
    public async Task SendMessage(string content)
    {
        var connectionInfo = _connectionTracker.GetConnection(Context.ConnectionId);
        if (connectionInfo == null || string.IsNullOrEmpty(connectionInfo.ConversationId))
        {
            _logger.LogWarning("Message from unknown connection: {ConnectionId}", Context.ConnectionId);
            return;
        }

        var message = new ChatMessage
        {
            ConversationId = connectionInfo.ConversationId,
            SenderId = connectionInfo.UserId,
            SenderName = connectionInfo.UserName,
            SenderType = connectionInfo.ConnectionType,
            Content = content
        };

        await _conversationService.AddMessage(message);

        // Send to the sender (for confirmation)
        await Clients.Caller.SendAsync("MessageReceived", message);

        if (connectionInfo.ConnectionType == "user")
        {
            // Notify all admin clients
            await Clients.Group("admins").SendAsync("UserMessage", message);
        }
        else if (connectionInfo.ConnectionType == "admin")
        {
            // Send to specific user
            var userConnectionId = _connectionTracker.GetUserConnectionId(message.ConversationId);
            if (userConnectionId != null)
            {
                await Clients.Client(userConnectionId).SendAsync("AdminMessage", message);
            }
        }

        _logger.LogInformation("Message sent: {SenderType} {SenderId} in conversation {ConversationId}",
            connectionInfo.ConnectionType, connectionInfo.UserId, connectionInfo.ConversationId);
    }

    /// <summary>
    /// Admin joins a specific conversation
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        var connectionInfo = _connectionTracker.GetConnection(Context.ConnectionId);
        if (connectionInfo == null || connectionInfo.ConnectionType != "admin")
        {
            _logger.LogWarning("Non-admin attempted to join conversation: {ConnectionId}", Context.ConnectionId);
            return;
        }

        connectionInfo.ConversationId = conversationId;

        var conversation = _conversationService.GetConversation(conversationId);
        if (conversation != null)
        {
            await Clients.Caller.SendAsync("ConversationHistory", conversation.Messages);
            _logger.LogInformation("Admin {AdminName} joined conversation {ConversationId}",
                connectionInfo.UserName, conversationId);
        }
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    public async Task MarkAsRead(string conversationId)
    {
        await _conversationService.MarkMessagesAsRead(conversationId);
        _logger.LogInformation("Messages marked as read in conversation {ConversationId}", conversationId);
    }

    /// <summary>
    /// User is typing indicator
    /// </summary>
    public async Task UserTyping(bool isTyping)
    {
        var connectionInfo = _connectionTracker.GetConnection(Context.ConnectionId);
        if (connectionInfo == null || string.IsNullOrEmpty(connectionInfo.ConversationId))
            return;

        if (connectionInfo.ConnectionType == "user")
        {
            await Clients.Group("admins").SendAsync("UserTyping", connectionInfo.ConversationId, isTyping);
        }
        else if (connectionInfo.ConnectionType == "admin")
        {
            var userConnectionId = _connectionTracker.GetUserConnectionId(connectionInfo.ConversationId);
            if (userConnectionId != null)
            {
                await Clients.Client(userConnectionId).SendAsync("AdminTyping", isTyping);
            }
        }
    }
}
