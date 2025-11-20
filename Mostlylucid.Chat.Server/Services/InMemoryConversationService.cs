using System.Collections.Concurrent;
using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Services;

/// <summary>
/// In-memory conversation service for simple deployment
/// For production, replace with database-backed implementation
/// </summary>
public class InMemoryConversationService : IConversationService
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();
    private readonly ConcurrentDictionary<string, string> _userConversationMap = new();
    private readonly ILogger<InMemoryConversationService> _logger;

    public InMemoryConversationService(ILogger<InMemoryConversationService> logger)
    {
        _logger = logger;
    }

    public Task<Conversation> CreateOrGetConversation(string userName, string email, string sourceUrl)
    {
        var userId = GetUserId(userName, email);

        if (_userConversationMap.TryGetValue(userId, out var conversationId) &&
            _conversations.TryGetValue(conversationId, out var existingConversation))
        {
            existingConversation.LastMessageAt = DateTime.UtcNow;
            existingConversation.IsActive = true;
            return Task.FromResult(existingConversation);
        }

        var conversation = new Conversation
        {
            UserId = userId,
            UserName = userName,
            UserEmail = email,
            SourceUrl = sourceUrl
        };

        _conversations[conversation.Id] = conversation;
        _userConversationMap[userId] = conversation.Id;

        _logger.LogInformation("Created new conversation {ConversationId} for user {UserName}",
            conversation.Id, userName);

        return Task.FromResult(conversation);
    }

    public Conversation? GetConversation(string conversationId)
    {
        _conversations.TryGetValue(conversationId, out var conversation);
        return conversation;
    }

    public List<Conversation> GetActiveConversations()
    {
        return _conversations.Values
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    public Task AddMessage(ChatMessage message)
    {
        if (_conversations.TryGetValue(message.ConversationId, out var conversation))
        {
            conversation.Messages.Add(message);
            conversation.LastMessageAt = message.Timestamp;

            // Increment unread count if message is from user
            if (message.SenderType == "user")
            {
                conversation.UnreadCount++;
            }

            _logger.LogDebug("Added message to conversation {ConversationId}", message.ConversationId);
        }
        else
        {
            _logger.LogWarning("Attempted to add message to non-existent conversation {ConversationId}",
                message.ConversationId);
        }

        return Task.CompletedTask;
    }

    public Task MarkMessagesAsRead(string conversationId)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            foreach (var message in conversation.Messages.Where(m => !m.IsRead))
            {
                message.IsRead = true;
            }
            conversation.UnreadCount = 0;
        }

        return Task.CompletedTask;
    }

    private static string GetUserId(string userName, string email)
    {
        return !string.IsNullOrEmpty(email)
            ? $"{email.ToLowerInvariant()}"
            : $"anon_{userName}_{Guid.NewGuid():N}";
    }
}
