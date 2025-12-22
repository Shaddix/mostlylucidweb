using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Embeddings;
using Npgsql;

namespace Mostlylucid.SegmentCommerce.Services.Queue;

/// <summary>
/// Background service that processes jobs from the PostgreSQL queue.
/// </summary>
public class JobWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobWorkerService> _logger;
    private readonly string _workerId;
    private readonly string[] _queues;
    private readonly TimeSpan _pollInterval;
    private readonly string? _connectionString;
    private readonly TimeSpan _notifyWait = TimeSpan.FromSeconds(5);

    public JobWorkerService(
        IServiceProvider serviceProvider,
        ILogger<JobWorkerService> logger,
        IConfiguration configuration,
        string[]? queues = null,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
        _queues = queues ?? ["default", "embeddings", "events"];
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job worker {WorkerId} started, polling queues: {Queues}",
            _workerId, string.Join(", ", _queues));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = false;

                foreach (var queue in _queues)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();

                    var job = await jobQueue.DequeueAsync(queue, _workerId, stoppingToken);

                    if (job != null)
                    {
                        processedAny = true;
                        await ProcessJobAsync(scope.ServiceProvider, job, stoppingToken);
                    }
                }

                if (!processedAny)
                {
                    var notified = await WaitForNotifyAsync(stoppingToken);
                    if (!notified)
                    {
                        await Task.Delay(_pollInterval, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job worker loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Job worker {WorkerId} stopped", _workerId);
    }

    private async Task ProcessJobAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        var jobQueue = serviceProvider.GetRequiredService<IJobQueue>();

        try
        {
            _logger.LogDebug("Processing job {JobId} of type {JobType}", job.Id, job.JobType);

            await ExecuteJobAsync(serviceProvider, job, cancellationToken);

            await jobQueue.CompleteAsync(job.Id, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed: {Error}", job.Id, ex.Message);
            await jobQueue.FailAsync(job.Id, ex.Message, cancellationToken);
        }
    }

    private async Task ExecuteJobAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        switch (job.JobType)
        {
            case JobTypes.GenerateProductEmbedding:
                await HandleGenerateProductEmbeddingAsync(serviceProvider, job, cancellationToken);
                break;

            case JobTypes.ProcessInteractionEvent:
                await HandleProcessInteractionEventAsync(job);
                break;

            case JobTypes.DecayInterests:
            case JobTypes.DecayProfileInterests:
                await HandleDecayInterestsAsync(serviceProvider, cancellationToken);
                break;

            case JobTypes.CollectExpiredSessions:
                await HandleCollectExpiredSessionsAsync(serviceProvider, cancellationToken);
                break;

            case JobTypes.PromoteSessionProfile:
                await HandleElevateSessionAsync(serviceProvider, job, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown job type: {JobType}", job.JobType);
                break;
        }
    }

    private async Task HandleGenerateProductEmbeddingAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.Payload);
        var productId = doc.RootElement.GetProperty("productId").GetInt32();

        var embeddingService = serviceProvider.GetRequiredService<IEmbeddingService>();
        var success = await embeddingService.IndexProductAsync(productId, cancellationToken);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to generate embedding for product {productId}");
        }
    }

    private Task HandleProcessInteractionEventAsync(JobQueueEntity job)
    {
        _logger.LogDebug("Processing interaction event: {Payload}", job.Payload);
        return Task.CompletedTask;
    }

    private async Task HandleDecayInterestsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var db = serviceProvider.GetRequiredService<SegmentCommerceDbContext>();
        var now = DateTime.UtcNow;
        const double decayRate = 0.02;

        // Decay persistent profile interests
        var profiles = await db.PersistentProfiles
            .Where(p => p.UpdatedAt < now.AddDays(-1))
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            var elapsedDays = (now - profile.UpdatedAt).TotalDays;
            var decayFactor = Math.Exp(-decayRate * elapsedDays);

            foreach (var key in profile.Interests.Keys.ToList())
            {
                profile.Interests[key] *= decayFactor;
                if (profile.Interests[key] < 0.01)
                    profile.Interests.Remove(key);
            }

            profile.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Decayed interests for {Count} profiles", profiles.Count);
    }

    private async Task HandleCollectExpiredSessionsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var db = serviceProvider.GetRequiredService<SegmentCommerceDbContext>();
        var collector = serviceProvider.GetRequiredService<Profiles.ISessionCollector>();
        var now = DateTime.UtcNow;

        // Find expired sessions that haven't been elevated
        var expiredSessions = await db.SessionProfiles
            .Where(s => s.ExpiresAt <= now && !s.IsElevated && s.PersistentProfileId != null)
            .Include(s => s.PersistentProfile)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            if (session.PersistentProfile != null)
            {
                await collector.ElevateToProfileAsync(session, session.PersistentProfile, cancellationToken);
            }
        }

        _logger.LogDebug("Elevated {Count} expired sessions", expiredSessions.Count);
    }

    private async Task HandleElevateSessionAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.Payload);
        var sessionId = doc.RootElement.GetProperty("sessionId").GetGuid();

        var db = serviceProvider.GetRequiredService<SegmentCommerceDbContext>();
        var collector = serviceProvider.GetRequiredService<Profiles.ISessionCollector>();

        var session = await db.SessionProfiles
            .Include(s => s.PersistentProfile)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session?.PersistentProfile != null && !session.IsElevated)
        {
            await collector.ElevateToProfileAsync(session, session.PersistentProfile, cancellationToken);
        }
    }

    private async Task<bool> WaitForNotifyAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_connectionString))
            return false;

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using (var listen = new NpgsqlCommand("LISTEN job_queue_notify;", conn))
            {
                await listen.ExecuteNonQueryAsync(cancellationToken);
            }

            return await conn.WaitAsync(_notifyWait, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Listen failed; falling back to polling");
            return false;
        }
    }
}

/// <summary>
/// Background service that processes outbox messages.
/// </summary>
public class OutboxWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxWorkerService> _logger;
    private readonly TimeSpan _pollInterval;

    public OutboxWorkerService(
        IServiceProvider serviceProvider,
        ILogger<OutboxWorkerService> logger,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                await processor.ProcessPendingAsync(100, stoppingToken);
                await processor.RetryFailedAsync(50, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox worker loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox worker stopped");
    }
}
