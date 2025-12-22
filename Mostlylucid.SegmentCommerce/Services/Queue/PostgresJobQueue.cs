using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Npgsql;

namespace Mostlylucid.SegmentCommerce.Services.Queue;

/// <summary>
/// PostgreSQL-based job queue using SKIP LOCKED for distributed processing.
/// </summary>
public class PostgresJobQueue : IJobQueue
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<PostgresJobQueue> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PostgresJobQueue(SegmentCommerceDbContext context, ILogger<PostgresJobQueue> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<long> EnqueueAsync<T>(
        string jobType,
        T payload,
        string queue = "default",
        int priority = 100,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var scheduledAt = delay.HasValue
            ? DateTime.UtcNow.Add(delay.Value)
            : DateTime.UtcNow;

        return await ScheduleAsync(jobType, payload, scheduledAt, queue, priority, cancellationToken);
    }

    public async Task<long> ScheduleAsync<T>(
        string jobType,
        T payload,
        DateTime scheduledAt,
        string queue = "default",
        int priority = 100,
        CancellationToken cancellationToken = default)
    {
        var job = new JobQueueEntity
        {
            JobType = jobType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Queue = queue,
            Priority = priority,
            ScheduledAt = scheduledAt,
            Status = JobStatus.Pending
        };

        _context.JobQueue.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        await NotifyAsync(queue, cancellationToken);

        _logger.LogDebug("Enqueued job {JobId} of type {JobType} to queue {Queue}",
            job.Id, jobType, queue);

        return job.Id;
    }



    private async Task NotifyAsync(string queue, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT pg_notify('job_queue_notify', {0});", new object[] { queue }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Notify failed (non-fatal)");
        }
    }

    public async Task<JobQueueEntity?> DequeueAsync(
        string queue = "default",
        string workerId = "",
        CancellationToken cancellationToken = default)
    {
        // Use raw SQL with SKIP LOCKED for proper distributed locking
        // This allows multiple workers to poll concurrently without blocking
        var sql = """
            UPDATE job_queue
            SET status = 1, -- Processing
                started_at = NOW(),
                worker_id = {0},
                attempts = attempts + 1
            WHERE id = (
                SELECT id FROM job_queue
                WHERE queue = {1}
                  AND status = 0  -- Pending
                  AND scheduled_at <= NOW()
                ORDER BY priority ASC, scheduled_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """;

        var jobs = await _context.JobQueue
            .FromSqlRaw(sql, workerId, queue)
            .ToListAsync(cancellationToken);

        var job = jobs.FirstOrDefault();

        if (job != null)
        {
            _logger.LogDebug("Dequeued job {JobId} of type {JobType} for worker {WorkerId}",
                job.Id, job.JobType, workerId);
        }

        return job;
    }

    public async Task CompleteAsync(long jobId, string? result = null, CancellationToken cancellationToken = default)
    {
        var job = await _context.JobQueue.FindAsync(new object[] { jobId }, cancellationToken);
        if (job == null) return;

        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.Result = result;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Completed job {JobId} of type {JobType}", jobId, job.JobType);
    }

    public async Task FailAsync(long jobId, string error, CancellationToken cancellationToken = default)
    {
        var job = await _context.JobQueue.FindAsync(new object[] { jobId }, cancellationToken);
        if (job == null) return;

        job.Error = error;

        if (job.Attempts >= job.MaxAttempts)
        {
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Job {JobId} failed permanently after {Attempts} attempts: {Error}",
                jobId, job.Attempts, error);
        }
        else
        {
            // Exponential backoff: 30s, 2m, 8m
            var backoffSeconds = Math.Pow(4, job.Attempts) * 30;
            job.Status = JobStatus.Pending;
            job.ScheduledAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
            job.StartedAt = null;
            job.WorkerId = null;

            _logger.LogWarning("Job {JobId} failed (attempt {Attempts}/{MaxAttempts}), retrying at {RetryAt}: {Error}",
                jobId, job.Attempts, job.MaxAttempts, job.ScheduledAt, error);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<QueueStats> GetStatsAsync(string? queue = null, CancellationToken cancellationToken = default)
    {
        var query = _context.JobQueue.AsQueryable();

        if (!string.IsNullOrEmpty(queue))
        {
            query = query.Where(j => j.Queue == queue);
        }

        var stats = await query
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byQueue = await query
            .Where(j => j.Status == JobStatus.Pending)
            .GroupBy(j => j.Queue)
            .Select(g => new { Queue = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Queue, x => x.Count, cancellationToken);

        var byJobType = await query
            .Where(j => j.Status == JobStatus.Pending)
            .GroupBy(j => j.JobType)
            .Select(g => new { JobType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JobType, x => x.Count, cancellationToken);

        return new QueueStats
        {
            Pending = stats.FirstOrDefault(s => s.Status == JobStatus.Pending)?.Count ?? 0,
            Processing = stats.FirstOrDefault(s => s.Status == JobStatus.Processing)?.Count ?? 0,
            Completed = stats.FirstOrDefault(s => s.Status == JobStatus.Completed)?.Count ?? 0,
            Failed = stats.FirstOrDefault(s => s.Status == JobStatus.Failed)?.Count ?? 0,
            ByQueue = byQueue,
            ByJobType = byJobType
        };
    }
}
