namespace Mostlylucid.SegmentCommerce.Services.Queue;

/// <summary>
/// Interface for publishing events to the outbox.
/// Events are stored transactionally with business data.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Publish an event to the outbox.
    /// Call SaveChanges to commit both the event and any business data.
    /// </summary>
    void Publish<T>(
        string eventType,
        T payload,
        string? aggregateType = null,
        string? aggregateId = null);

    /// <summary>
    /// Publish an event and save immediately.
    /// </summary>
    Task PublishAndSaveAsync<T>(
        string eventType,
        T payload,
        string? aggregateType = null,
        string? aggregateId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for processing outbox messages.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// Process pending outbox messages.
    /// </summary>
    Task ProcessPendingAsync(int batchSize = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry failed messages.
    /// </summary>
    Task RetryFailedAsync(int batchSize = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for a specific event type.
/// </summary>
public interface IOutboxEventHandler<in T>
{
    Task HandleAsync(T payload, CancellationToken cancellationToken = default);
}
