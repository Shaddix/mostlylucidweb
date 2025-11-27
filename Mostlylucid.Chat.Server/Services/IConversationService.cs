using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Services;

public interface IConversationService
{
    Task<Conversation> CreateOrGetConversation(string userName, string email, string sourceUrl);
    Conversation? GetConversation(string conversationId);
    List<Conversation> GetActiveConversations();
    Task AddMessage(ChatMessage message);
    Task MarkMessagesAsRead(string conversationId);
}
