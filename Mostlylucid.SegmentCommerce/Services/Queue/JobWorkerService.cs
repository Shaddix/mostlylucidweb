using System.Text.Json;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Embeddings;

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

    public JobWorkerService(
        IServiceProvider serviceProvider,
        ILogger<JobWorkerService> logger,
        string[]? queues = null,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
        _queues = queues ?? new[] { "default", "embeddings", "events" };
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
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

                // If no jobs were found, wait before polling again
                if (!processedAny)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
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
                await HandleProcessInteractionEventAsync(serviceProvider, job, cancellationToken);
                break;

            case JobTypes.DecayInterests:
                await HandleDecayInterestsAsync(serviceProvider, job, cancellationToken);
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

    private Task HandleProcessInteractionEventAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        // Process interaction events for analytics
        // This could update aggregate counters, trigger recommendations, etc.
        _logger.LogDebug("Processing interaction event: {Payload}", job.Payload);
        return Task.CompletedTask;
    }

    private Task HandleDecayInterestsAsync(
        IServiceProvider serviceProvider,
        JobQueueEntity job,
        CancellationToken cancellationToken)
    {
        // Apply interest decay to visitor profiles
        // This would run periodically to reduce stale interests
        _logger.LogDebug("Decaying interests");
        return Task.CompletedTask;
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
