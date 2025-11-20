using Microsoft.EntityFrameworkCore;
using Mostlylucid.Chat.Server.Data;
using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Services;

/// <summary>
/// SQLite-backed conversation service
/// Demonstrates how to persist chat data to a database
/// </summary>
public class SqliteConversationService : IConversationService
{
    private readonly ChatDbContext _context;
    private readonly ILogger<SqliteConversationService> _logger;

    public SqliteConversationService(ChatDbContext context, ILogger<SqliteConversationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Conversation> CreateOrGetConversation(string userName, string email, string sourceUrl)
    {
        var userId = GetUserId(userName, email);

        // Try to find existing conversation
        var entity = await _context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (entity != null)
        {
            // Reactivate existing conversation
            entity.LastMessageAt = DateTime.UtcNow;
            entity.IsActive = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reactivated conversation {ConversationId} for user {UserName}",
                entity.Id, userName);

            return entity.ToModel();
        }

        // Create new conversation
        entity = new ConversationEntity
        {
            UserId = userId,
            UserName = userName,
            UserEmail = email,
            SourceUrl = sourceUrl
        };

        _context.Conversations.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new conversation {ConversationId} for user {UserName}",
            entity.Id, userName);

        return entity.ToModel();
    }

    public Conversation? GetConversation(string conversationId)
    {
        var entity = _context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefault(c => c.Id == conversationId);

        return entity?.ToModel();
    }

    public List<Conversation> GetActiveConversations()
    {
        var entities = _context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        return entities.Select(e => e.ToModel()).ToList();
    }

    public async Task AddMessage(ChatMessage message)
    {
        var entity = ChatMessageEntity.FromModel(message);
        _context.Messages.Add(entity);

        // Update conversation last message time
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == message.ConversationId);

        if (conversation != null)
        {
            conversation.LastMessageAt = message.Timestamp;
            await _context.SaveChangesAsync();
            _logger.LogDebug("Added message to conversation {ConversationId}", message.ConversationId);
        }
        else
        {
            _logger.LogWarning("Attempted to add message to non-existent conversation {ConversationId}",
                message.ConversationId);
        }
    }

    public async Task MarkMessagesAsRead(string conversationId)
    {
        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsRead)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsRead = true;
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Marked {Count} messages as read in conversation {ConversationId}",
            messages.Count, conversationId);
    }

    private static string GetUserId(string userName, string email)
    {
        return !string.IsNullOrEmpty(email)
            ? email.ToLowerInvariant()
            : $"anon_{userName}_{Guid.NewGuid():N}";
    }
}
