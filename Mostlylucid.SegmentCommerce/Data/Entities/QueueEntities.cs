using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mostlylucid.SegmentCommerce.Data.Entities;

/// <summary>
/// Outbox table for reliable event publishing.
/// Events are written here transactionally with business data,
/// then processed by a background worker.
/// </summary>
[Table("outbox_messages")]
public class OutboxMessageEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The event type (e.g., "ProductViewed", "ProductAddedToCart", "OrderPlaced").
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The event payload as JSON.
    /// </summary>
    [Required]
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Optional aggregate ID for ordering/deduplication.
    /// </summary>
    [MaxLength(100)]
    [Column("aggregate_id")]
    public string? AggregateId { get; set; }

    /// <summary>
    /// Optional aggregate type.
    /// </summary>
    [MaxLength(100)]
    [Column("aggregate_type")]
    public string? AggregateType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this message was processed (null if pending).
    /// </summary>
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    [Column("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    [Column("attempts")]
    public int Attempts { get; set; } = 0;

    /// <summary>
    /// Next retry time (for failed messages).
    /// </summary>
    [Column("next_retry_at")]
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Background job queue using PostgreSQL SKIP LOCKED for distributed processing.
/// </summary>
[Table("job_queue")]
public class JobQueueEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// Job type identifier.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("job_type")]
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Job payload as JSON.
    /// </summary>
    [Required]
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Job priority (lower = higher priority).
    /// </summary>
    [Column("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Queue name for routing to specific workers.
    /// </summary>
    [MaxLength(50)]
    [Column("queue")]
    public string Queue { get; set; } = "default";

    [Column("status")]
    public JobStatus Status { get; set; } = JobStatus.Pending;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When to run this job (for delayed jobs).
    /// </summary>
    [Column("scheduled_at")]
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When processing started.
    /// </summary>
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When processing completed.
    /// </summary>
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Worker ID that picked up this job.
    /// </summary>
    [MaxLength(100)]
    [Column("worker_id")]
    public string? WorkerId { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [Column("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Number of attempts.
    /// </summary>
    [Column("attempts")]
    public int Attempts { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts.
    /// </summary>
    [Column("max_attempts")]
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Result data as JSON (for completed jobs).
    /// </summary>
    [Column("result", TypeName = "jsonb")]
    public string? Result { get; set; }
}

public enum JobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// Known job types in the system.
/// </summary>
public static class JobTypes
{
    public const string GenerateProductEmbedding = "generate_product_embedding";
    public const string GenerateInterestEmbedding = "generate_interest_embedding";
    public const string ProcessInteractionEvent = "process_interaction_event";
    public const string DecayInterests = "decay_interests";
    public const string SendEmail = "send_email";
    public const string GenerateProductImage = "generate_product_image";
    public const string SyncToExternalSystem = "sync_to_external";
}

/// <summary>
/// Known event types for the outbox.
/// </summary>
public static class OutboxEventTypes
{
    public const string ProductCreated = "product.created";
    public const string ProductUpdated = "product.updated";
    public const string ProductViewed = "product.viewed";
    public const string ProductAddedToCart = "cart.item_added";
    public const string ProductRemovedFromCart = "cart.item_removed";
    public const string OrderPlaced = "order.placed";
    public const string InterestUpdated = "interest.updated";
    public const string ProfileCreated = "profile.created";
}
