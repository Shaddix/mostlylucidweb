using Microsoft.EntityFrameworkCore;
using Mostlylucid.Chat.Shared.Models;

namespace Mostlylucid.Chat.Server.Data;

/// <summary>
/// Entity Framework DbContext for chat data
/// Uses SQLite for simple, portable storage
/// </summary>
public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ConversationEntity> Conversations { get; set; } = null!;
    public DbSet<ChatMessageEntity> Messages { get; set; } = null!;
    public DbSet<PresenceEntity> Presence { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Conversation configuration
        modelBuilder.Entity<ConversationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.LastMessageAt);

            // One-to-many relationship with messages
            entity.HasMany(e => e.Messages)
                .WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Message configuration
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Presence configuration
        modelBuilder.Entity<PresenceEntity>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.UserType);
            entity.HasIndex(e => e.LastSeen);
        });
    }
}

/// <summary>
/// Database entity for conversations
/// Stores user information and conversation metadata
/// </summary>
public class ConversationEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation property
    public List<ChatMessageEntity> Messages { get; set; } = new();

    /// <summary>
    /// Convert to shared model for SignalR transmission
    /// </summary>
    public Conversation ToModel()
    {
        return new Conversation
        {
            Id = Id,
            UserId = UserId,
            UserName = UserName,
            UserEmail = UserEmail,
            SourceUrl = SourceUrl,
            StartedAt = StartedAt,
            LastMessageAt = LastMessageAt,
            IsActive = IsActive,
            UnreadCount = Messages.Count(m => m.SenderType == "user" && !m.IsRead),
            Messages = Messages.Select(m => m.ToModel()).ToList()
        };
    }
}

/// <summary>
/// Database entity for chat messages
/// </summary>
public class ChatMessageEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderType { get; set; } = "user"; // "user" or "admin"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Convert to shared model for SignalR transmission
    /// </summary>
    public ChatMessage ToModel()
    {
        return new ChatMessage
        {
            Id = Id,
            ConversationId = ConversationId,
            SenderId = SenderId,
            SenderName = SenderName,
            SenderType = SenderType,
            Content = Content,
            Timestamp = Timestamp,
            IsRead = IsRead
        };
    }

    /// <summary>
    /// Create entity from shared model
    /// </summary>
    public static ChatMessageEntity FromModel(ChatMessage message)
    {
        return new ChatMessageEntity
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            SenderType = message.SenderType,
            Content = message.Content,
            Timestamp = message.Timestamp,
            IsRead = message.IsRead
        };
    }
}

/// <summary>
/// Database entity for user presence tracking
/// Tracks who is online (users and admins)
/// </summary>
public class PresenceEntity
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserType { get; set; } = "user"; // "user" or "admin"
    public bool IsOnline { get; set; } = false;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? ConnectionId { get; set; }
}
