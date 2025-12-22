using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Services.Queue;

public class PostgresOutbox : IOutbox
{
    private readonly SegmentCommerceDbContext _context;
    private readonly ILogger<PostgresOutbox> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PostgresOutbox(SegmentCommerceDbContext context, ILogger<PostgresOutbox> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void Publish<T>(
        string eventType,
        T payload,
        string? aggregateType = null,
        string? aggregateId = null)
    {
        var message = new OutboxMessageEntity
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            AggregateType = aggregateType,
            AggregateId = aggregateId
        };

        _context.OutboxMessages.Add(message);

        _logger.LogDebug("Added outbox message {MessageId} of type {EventType}",
            message.Id, eventType);
    }

    public async Task PublishAndSaveAsync<T>(
        string eventType,
        T payload,
        string? aggregateType = null,
        string? aggregateId = null,
        CancellationToken cancellationToken = default)
    {
        Publish(eventType, payload, aggregateType, aggregateId);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class OutboxProcessor : IOutboxProcessor
{
    private readonly SegmentCommerceDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        SegmentCommerceDbContext context,
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var messages = await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.NextRetryAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken);
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                message.Attempts++;
                message.Error = ex.Message;

                if (message.Attempts >= 5)
                {
                    message.ProcessedAt = DateTime.UtcNow;
                }
                else
                {
                    var backoffSeconds = Math.Pow(2, message.Attempts) * 30;
                    message.NextRetryAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RetryFailedAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var messages = await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null &&
                        m.NextRetryAt != null &&
                        m.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(m => m.NextRetryAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.NextRetryAt = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await ProcessPendingAsync(batchSize, cancellationToken);
    }

    private async Task ProcessMessageAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing outbox message {MessageId} of type {EventType}",
            message.Id, message.EventType);

        switch (message.EventType)
        {
            case OutboxEventTypes.ProductCreated:
            case OutboxEventTypes.ProductUpdated:
                await EnqueueEmbeddingJobAsync(message, cancellationToken);
                break;

            case OutboxEventTypes.ProductViewed:
            case OutboxEventTypes.ProductAddedToCart:
                _logger.LogInformation("Processed {EventType} event", message.EventType);
                break;

            default:
                _logger.LogWarning("No handler for event type {EventType}", message.EventType);
                break;
        }
    }

    private async Task EnqueueEmbeddingJobAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(message.Payload);
        if (doc.RootElement.TryGetProperty("productId", out var productIdElement))
        {
            var productId = productIdElement.GetInt32();

            var job = new JobQueueEntity
            {
                JobType = JobTypes.GenerateProductEmbedding,
                Payload = JsonSerializer.Serialize(new { productId }),
                Queue = "embeddings",
                Priority = 50
            };

            _context.JobQueue.Add(job);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Enqueued embedding job for product {ProductId}", productId);
        }
    }
}
