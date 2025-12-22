using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Services.Queue;

/// <summary>
/// Interface for enqueueing background jobs.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a job to be processed by a background worker.
    /// </summary>
    Task<long> EnqueueAsync<T>(
        string jobType,
        T payload,
        string queue = "default",
        int priority = 100,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueue a job to run at a specific time.
    /// </summary>
    Task<long> ScheduleAsync<T>(
        string jobType,
        T payload,
        DateTime scheduledAt,
        string queue = "default",
        int priority = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the next available job for processing (uses SKIP LOCKED).
    /// </summary>
    Task<JobQueueEntity?> DequeueAsync(
        string queue = "default",
        string workerId = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a job as completed.
    /// </summary>
    Task CompleteAsync(long jobId, string? result = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a job as failed (will retry if attempts < maxAttempts).
    /// </summary>
    Task FailAsync(long jobId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get queue statistics.
    /// </summary>
    Task<QueueStats> GetStatsAsync(string? queue = null, CancellationToken cancellationToken = default);
}

public record QueueStats
{
    public int Pending { get; init; }
    public int Processing { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public Dictionary<string, int> ByQueue { get; init; } = new();
    public Dictionary<string, int> ByJobType { get; init; } = new();
}
