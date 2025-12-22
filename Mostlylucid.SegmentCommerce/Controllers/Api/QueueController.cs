using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Queue;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/[controller]")]
public class QueueController : ControllerBase
{
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<QueueController> _logger;

    public QueueController(IJobQueue jobQueue, ILogger<QueueController> logger)
    {
        _jobQueue = jobQueue;
        _logger = logger;
    }

    /// <summary>
    /// Get queue statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(
        [FromQuery] string? queue = null,
        CancellationToken cancellationToken = default)
    {
        var stats = await _jobQueue.GetStatsAsync(queue, cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Enqueue a job manually (for testing).
    /// </summary>
    [HttpPost("enqueue")]
    public async Task<IActionResult> Enqueue(
        [FromBody] EnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        var jobId = await _jobQueue.EnqueueAsync(
            request.JobType,
            request.Payload,
            request.Queue ?? "default",
            request.Priority ?? 100,
            request.DelaySeconds.HasValue ? TimeSpan.FromSeconds(request.DelaySeconds.Value) : null,
            cancellationToken);

        return Ok(new { jobId });
    }

    /// <summary>
    /// Schedule a job for later (for testing).
    /// </summary>
    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule(
        [FromBody] ScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var jobId = await _jobQueue.ScheduleAsync(
            request.JobType,
            request.Payload,
            request.ScheduledAt,
            request.Queue ?? "default",
            request.Priority ?? 100,
            cancellationToken);

        return Ok(new { jobId });
    }

    /// <summary>
    /// Enqueue embedding generation for a product.
    /// </summary>
    [HttpPost("embed-product/{productId:int}")]
    public async Task<IActionResult> EnqueueProductEmbedding(
        int productId,
        CancellationToken cancellationToken = default)
    {
        var jobId = await _jobQueue.EnqueueAsync(
            JobTypes.GenerateProductEmbedding,
            new { productId },
            queue: "embeddings",
            priority: 50,
            cancellationToken: cancellationToken);

        return Ok(new { jobId });
    }

    /// <summary>
    /// Enqueue interest decay job.
    /// </summary>
    [HttpPost("decay-interests")]
    public async Task<IActionResult> EnqueueDecayInterests(
        CancellationToken cancellationToken = default)
    {
        var jobId = await _jobQueue.EnqueueAsync(
            JobTypes.DecayInterests,
            new { triggeredAt = DateTime.UtcNow },
            queue: "default",
            priority: 200, // Low priority
            cancellationToken: cancellationToken);

        return Ok(new { jobId });
    }
}

public record EnqueueRequest
{
    public string JobType { get; init; } = string.Empty;
    public object Payload { get; init; } = new { };
    public string? Queue { get; init; }
    public int? Priority { get; init; }
    public int? DelaySeconds { get; init; }
}

public record ScheduleRequest
{
    public string JobType { get; init; } = string.Empty;
    public object Payload { get; init; } = new { };
    public DateTime ScheduledAt { get; init; }
    public string? Queue { get; init; }
    public int? Priority { get; init; }
}
