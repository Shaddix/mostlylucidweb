# Mostlylucid.Ephemeral

A lightweight .NET library for **bounded, observable, self-cleaning async execution** with signal-based coordination.

**Fire... and Don't *Quite* Forget.**

## What Is This?

Ephemeral execution sits between two extremes:

```csharp
// Fire-and-forget: No visibility, no debugging
_ = Task.Run(() => ProcessAsync(item));

// Blocking: User waits for everything
await ProcessAsync(item);
```

Ephemeral execution gives you:

```csharp
// Trackable, bounded, debuggable
await coordinator.EnqueueAsync(item);

// Instant visibility
Console.WriteLine($"Pending: {coordinator.PendingCount}");
Console.WriteLine($"Active: {coordinator.ActiveCount}");
Console.WriteLine($"Failed: {coordinator.TotalFailed}");
```

Same async execution. Complete observability. No user data retained.

## Features

- **Bounded execution** - Configurable concurrency limits
- **Operation tracking** - Know what's running, pending, completed, failed
- **Self-cleaning** - Old operations automatically evict from the window
- **Per-key ordering** - Sequential execution within an entity, parallel across entities
- **Signal infrastructure** - Ambient awareness without coupling
- **Dynamic concurrency** - Adjust parallelism at runtime
- **Fair scheduling** - Prevent hot entities from starving others
- **Privacy-safe** - Only metadata retained, never payloads

## Blog Posts

For detailed explanations of the design and implementation:

- [Fire and Don't *Quite* Forget - Ephemeral Execution](https://www.mostlylucid.net/blog/fire-and-dont-quite-forget-ephemeral-execution) - The theory and pattern
- [Building a Reusable Ephemeral Execution Library](https://www.mostlylucid.net/blog/ephemeral-execution-library) - The full implementation
- [Ephemeral Signals - Turning Atoms into a Sensing Network](https://www.mostlylucid.net/blog/ephemeral-signals) - Signal-based coordination

## Installation

Copy the `Helpers/Ephemeral` folder into your project, or reference it from your solution. 

> NUGET PACKAGE SHORTLY! Sorry I want to ge the core, samples testing etc perfect before release.  Worth it if you want machine and the start of distributed coordination! Poll a signal the same way in process as for a machine half way across the world. Coordinated systems with MINIMAL config (a url of ANY member plus a key) and you get a dynamic cluster!

## Quick Start

### Simple Parallel Processing

```csharp
// Process a collection with bounded concurrency
await items.EphemeralForEachAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new EphemeralOptions { MaxConcurrency = 8 });
```

### Long-Lived Work Queue

```csharp
// Create a coordinator for background processing
await using var coordinator = new EphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) => await TranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 8 });

// Enqueue work over time
await coordinator.EnqueueAsync(new TranslationRequest("Hello", "es"));

// Check status anytime
Console.WriteLine($"Pending: {coordinator.PendingCount}");
Console.WriteLine($"Active: {coordinator.ActiveCount}");

// When done
coordinator.Complete();
await coordinator.DrainAsync();
```

### Dependency Injection

```csharp
// Program.cs
services.AddEphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) => await TranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 8 });

// Your service
public class TranslationService(EphemeralWorkCoordinator<TranslationRequest> coordinator)
{
    public async Task TranslateAsync(TranslationRequest request)
    {
        await coordinator.EnqueueAsync(request);
    }

    public object GetStatus() => new
    {
        pending = coordinator.PendingCount,
        active = coordinator.ActiveCount,
        completed = coordinator.TotalCompleted,
        failed = coordinator.TotalFailed
    };
}
```

## Which Coordinator Do I Need?

| Scenario | Coordinator |
|----------|-------------|
| Process a collection once | `EphemeralForEachAsync<T>` |
| Long-lived queue accepting items over time | `EphemeralWorkCoordinator<T>` |
| Per-entity ordering (user commands, tenant jobs) | `EphemeralKeyedWorkCoordinator<TKey, T>` |
| Capture results (fingerprints, summaries) | `EphemeralResultCoordinator<TInput, TResult>` |
| Multiple coordinators with different configs | `IEphemeralCoordinatorFactory<T>` |
| Runtime concurrency adjustment | Set `EnableDynamicConcurrency = true` |

## Source Files

| File | Purpose |
|------|---------|
| `EphemeralOptions.cs` | Configuration (concurrency, window size, lifetime, signals) |
| `EphemeralOperation.cs` | Internal operation tracking with signal support |
| `Snapshots.cs` | Immutable snapshot records exposed to consumers |
| `Signals.cs` | Signal events, propagation, constraints, and SignalSink |
| `EphemeralIdGenerator.cs` | Fast XxHash64-based ID generation |
| `ConcurrencyGates.cs` | Fixed and adjustable concurrency limiting |
| `StringPatternMatcher.cs` | Glob-style pattern matching for signal filtering |
| `ParallelEphemeral.cs` | Static extension methods (`EphemeralForEachAsync`) |
| `EphemeralWorkCoordinator.cs` | Long-lived work queue coordinator |
| `EphemeralKeyedWorkCoordinator.cs` | Per-key sequential execution with fair scheduling |
| `EphemeralResultCoordinator.cs` | Result-capturing coordinator variant |
| `SignalDispatcher.cs` | Async signal routing with pattern matching |
| `DependencyInjection.cs` | DI extension methods and factory implementations |

---

## EphemeralForEachAsync

One-shot parallel processing with operation tracking.

### Basic Usage

```csharp
await items.EphemeralForEachAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new EphemeralOptions { MaxConcurrency = 8 });
```

### Keyed Execution

Per-entity sequential, globally parallel:

```csharp
await commands.EphemeralForEachAsync(
    cmd => cmd.UserId,  // Key selector
    async (cmd, ct) => await ExecuteCommandAsync(cmd, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1  // Sequential per user
    });
```

This ensures:
- User A's commands execute in order: 1, 2, 3
- User B's commands execute in order: 4, 5, 6
- But A and B run in parallel

---

## EphemeralWorkCoordinator

Long-lived work queue for continuous processing.

### Creating a Coordinator

```csharp
await using var coordinator = new EphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) => await TranslateAsync(request, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 8,
        MaxTrackedOperations = 500,
        EnableDynamicConcurrency = true
    });
```

### Enqueueing Work

```csharp
// Enqueue and continue (fire-and-don't-quite-forget)
await coordinator.EnqueueAsync(request);

// Check if queue is accepting work
if (coordinator.IsPaused)
{
    // Handle backpressure
}
```

### Querying Status

```csharp
// Counters
int pending = coordinator.PendingCount;
int active = coordinator.ActiveCount;
long completed = coordinator.TotalCompleted;
long failed = coordinator.TotalFailed;

// Snapshots
var all = coordinator.GetSnapshot();
var running = coordinator.GetRunning();
var failed = coordinator.GetFailed();
var completed = coordinator.GetCompleted();
```

### Flow Control

```csharp
// Pause processing (stop pulling new work)
coordinator.Pause();

// Resume processing
coordinator.Resume();

// Complete (no more work will be accepted)
coordinator.Complete();

// Wait for all work to finish
await coordinator.DrainAsync();
```

### Dynamic Concurrency

```csharp
// Enable at construction time
var coordinator = new EphemeralWorkCoordinator<T>(body,
    new EphemeralOptions
    {
        MaxConcurrency = 4,
        EnableDynamicConcurrency = true
    });

// Adjust at runtime based on system load
coordinator.SetMaxConcurrency(16);  // Scale up
coordinator.SetMaxConcurrency(2);   // Scale down
```

### Pinning Operations

Keep important operations from being evicted:

```csharp
// Pin an operation (survives window cleanup)
coordinator.Pin(operationId);

// Unpin when no longer needed
coordinator.Unpin(operationId);

// Force immediate eviction
coordinator.Evict(operationId);
```

### From IAsyncEnumerable

```csharp
await using var coordinator = EphemeralWorkCoordinator<Message>.FromAsyncEnumerable(
    messageStream,  // IAsyncEnumerable<Message>
    async (msg, ct) => await ProcessMessageAsync(msg, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

await coordinator.DrainAsync();
```

---

## EphemeralKeyedWorkCoordinator

Per-entity sequential execution with fair scheduling.

### Creating a Keyed Coordinator

```csharp
await using var coordinator = new EphemeralKeyedWorkCoordinator<string, Command>(
    cmd => cmd.UserId,  // Key selector
    async (cmd, ct) => await ExecuteCommandAsync(cmd, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1,      // Per-user sequential
        EnableFairScheduling = true,   // Prevent hot user starvation
        FairSchedulingThreshold = 10   // Reject if user has 10+ pending
    });
```

### Fair Scheduling

Prevents a single hot entity from consuming all capacity:

```csharp
// TryEnqueue returns false if fair scheduling rejects
if (!coordinator.TryEnqueue(hotUserCommand))
{
    // User has too many pending commands
    await DeferCommandAsync(hotUserCommand);
}
```

### Per-Key Visibility

```csharp
// Get pending count for a specific key
var pendingForUser = coordinator.GetPendingCountForKey("user-123");

// Get operations for a specific key
var opsForUser = coordinator.GetSnapshotForKey("user-123");
```

---

## EphemeralResultCoordinator

Captures results alongside operation metadata.

### Basic Usage

```csharp
await using var coordinator = new EphemeralResultCoordinator<SessionInput, SessionResult>(
    async (input, ct) =>
    {
        var fingerprint = await ComputeFingerprintAsync(input.Events, ct);
        return new SessionResult(fingerprint, input.Events.Length);
    },
    new EphemeralOptions { MaxConcurrency = 16 });

await coordinator.EnqueueAsync(session);
coordinator.Complete();
await coordinator.DrainAsync();
```

### Querying Results

```csharp
// Get just the results (no metadata)
IReadOnlyCollection<SessionResult> results = coordinator.GetResults();

// Get snapshots with results + metadata
var snapshots = coordinator.GetSnapshot();

// Get base snapshots without results (privacy-safe)
var baseSnapshots = coordinator.GetBaseSnapshot();

// Filter by success/failure
var successful = coordinator.GetSuccessful();
var failed = coordinator.GetFailed();
```

---

## Factory Pattern

Like `IHttpClientFactory`, create named coordinator configurations:

### Registration

```csharp
// Register named configurations
services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (request, ct) => await FastTranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 32 });

services.AddEphemeralWorkCoordinator<TranslationRequest>("accurate",
    async (request, ct) => await AccurateTranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 4 });
```

### Usage

```csharp
public class TranslationService(IEphemeralCoordinatorFactory<TranslationRequest> factory)
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _fast =
        factory.CreateCoordinator("fast");
    private readonly EphemeralWorkCoordinator<TranslationRequest> _accurate =
        factory.CreateCoordinator("accurate");

    public async Task TranslateAsync(TranslationRequest request, bool preferAccuracy)
    {
        var coordinator = preferAccuracy ? _accurate : _fast;
        await coordinator.EnqueueAsync(request);
    }
}
```

### Factory Guarantees

- Same name = same instance (calling `CreateCoordinator("fast")` twice returns the same coordinator)
- Different names = different instances
- Lazy creation (coordinators only created when first requested)
- Configuration validation (requesting unregistered name throws helpful error)

---

## Signal Infrastructure

Signals provide ambient awareness without coupling.

### Raising Signals

Inside your work body:

```csharp
await coordinator.ProcessAsync(async (item, op, ct) =>
{
    try
    {
        var result = await CallExternalApiAsync(item, ct);

        if (result.WasCached)
            op.Signal("cache-hit");

        if (result.Duration > TimeSpan.FromSeconds(2))
            op.Signal("slow-response");
    }
    catch (RateLimitException ex)
    {
        op.Signal("rate-limit");
        op.Signal($"rate-limit:{ex.RetryAfterMs}ms");
        throw;
    }
    catch (TimeoutException)
    {
        op.Signal("timeout");
        throw;
    }
});
```

### Querying Signals

```csharp
// Check if any recent operation hit a rate limit
if (coordinator.HasSignal("rate-limit"))
{
    await Task.Delay(1000);
}

// Count signals
var slowCount = coordinator.CountSignals("slow-response");
var totalSignals = coordinator.CountSignals();

// Get signals by pattern (supports * and ?)
var httpErrors = coordinator.GetSignalsByPattern("http.error.*");

// Get signals by time range
var recentSignals = coordinator.GetSignalsSince(DateTimeOffset.UtcNow.AddMinutes(-5));
var rangeSignals = coordinator.GetSignalsByTimeRange(from, to);

// Get signals for a specific key
var userSignals = coordinator.GetSignalsByKey("user-123");

// Get signals by exact name
var rateSignals = coordinator.GetSignalsByName("rate-limit");

// Pattern matching check (short-circuits on first match)
if (coordinator.HasSignalMatching("error.*"))
    await AlertAsync();
```

### Signal-Reactive Processing

Coordinators can automatically react to signals:

```csharp
var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        // Cancel new work if these signals are present
        CancelOnSignals = new HashSet<string> { "system-overload", "circuit-open" },

        // Defer new work while these signals are present
        DeferOnSignals = new HashSet<string> { "rate-limit" },
        MaxDeferAttempts = 10,
        DeferCheckInterval = TimeSpan.FromMilliseconds(100)
    });
```

### Cross-Coordinator Awareness with SignalSink

Share signals across multiple coordinators:

```csharp
// Create a shared sink
var sharedSignals = new SignalSink(maxCapacity: 1000);

// Configure coordinators to use it
var options = new EphemeralOptions { Signals = sharedSignals };

var orderProcessor = new EphemeralWorkCoordinator<Order>(ProcessOrderAsync, options);
var paymentProcessor = new EphemeralWorkCoordinator<Payment>(ProcessPaymentAsync, options);

// Raise signals directly
sharedSignals.Raise("system-maintenance");

// Sense from anywhere
if (sharedSignals.Detect("system-maintenance"))
{
    await DeferWorkAsync();
}
```

### Signal Constraints

Prevent infinite loops when signals cause other signals:

```csharp
var options = new EphemeralOptions
{
    SignalConstraints = new SignalConstraints
    {
        MaxDepth = 10,           // Max propagation depth
        BlockCycles = true,      // Prevent A -> B -> A cycles

        // Signals that end propagation chains
        TerminalSignals = new HashSet<string> { "completed", "failed", "resolved" },

        // Signals that emit but don't propagate
        LeafSignals = new HashSet<string> { "logged", "metric" },

        OnBlocked = (signal, reason) =>
        {
            logger.LogWarning("Signal {Signal} blocked: {Reason}", signal.Signal, reason);
        }
    }
};
```

### Async Signal Handling

For I/O-bound signal processing (logging to external services, sending notifications, etc.), use the async signal handler:

```csharp
var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        // Async handler - non-blocking, processed in background
        OnSignalAsync = async (signal, ct) =>
        {
            await _telemetry.TrackSignalAsync(signal.Signal, signal.Key, ct);

            if (signal.Is("error"))
            {
                await _alertService.SendAlertAsync(signal, ct);
            }
        },

        // Control concurrency of async handlers
        MaxConcurrentSignalHandlers = 4,

        // Max queued signals before dropping
        MaxQueuedSignals = 1000,

        // Synchronous handler still available for fast, in-memory operations
        OnSignal = signal =>
        {
            _metrics.IncrementCounter(signal.Signal);
        }
    });
```

The async signal processor:
- Signals are enqueued immediately (non-blocking)
- Processing happens in a background queue
- Bounded concurrency prevents resource exhaustion
- Oldest signals are dropped when queue is full
- Exceptions in handlers are swallowed (handlers should manage their own errors)

### AsyncSignalProcessor

For standalone async signal processing:

```csharp
await using var processor = new AsyncSignalProcessor(
    async (signal, ct) =>
    {
        await _externalService.LogAsync(signal, ct);
    },
    maxConcurrency: 4,
    maxQueueSize: 1000);

// Enqueue signals (returns immediately)
processor.Enqueue(new SignalEvent("rate-limit", opId, key, DateTimeOffset.UtcNow));

// Check stats
Console.WriteLine($"Queued: {processor.QueuedCount}");
Console.WriteLine($"Processed: {processor.ProcessedCount}");
Console.WriteLine($"Dropped: {processor.DroppedCount}");
```

### Signal Dispatcher

Route signals to handlers with pattern matching:

```csharp
await using var dispatcher = new SignalDispatcher();

// Register handlers
dispatcher.Register("rate-limit", async evt =>
{
    await ThrottleApiAsync(evt.Key);
});

dispatcher.Register("error.*", async evt =>
{
    await LogErrorAsync(evt);
});

// Dispatch signals
dispatcher.Dispatch(new SignalEvent("rate-limit", operationId, key, DateTimeOffset.UtcNow));
```

---

## Configuration Reference

### EphemeralOptions

```csharp
public sealed class EphemeralOptions
{
    // Concurrency control
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public int MaxConcurrencyPerKey { get; init; } = 1;
    public bool EnableDynamicConcurrency { get; init; } = false;

    // Window management
    public int MaxTrackedOperations { get; init; } = 200;
    public TimeSpan? MaxOperationLifetime { get; init; } = TimeSpan.FromMinutes(5);

    // Fair scheduling (keyed coordinator)
    public bool EnableFairScheduling { get; init; } = false;
    public int FairSchedulingThreshold { get; init; } = 10;

    // Signal-reactive processing
    public IReadOnlySet<string>? CancelOnSignals { get; init; }
    public IReadOnlySet<string>? DeferOnSignals { get; init; }
    public int MaxDeferAttempts { get; init; } = 10;
    public TimeSpan DeferCheckInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    // Signal infrastructure
    public SignalSink? Signals { get; init; }
    public SignalConstraints? SignalConstraints { get; init; }
    public Action<SignalEvent>? OnSignal { get; init; }

    // Observability
    public Action<IReadOnlyCollection<EphemeralOperationSnapshot>>? OnSample { get; init; }
}
```

### Configuration Guidelines

| Option | Default | Guidance |
|--------|---------|----------|
| `MaxConcurrency` | CPU count | Increase for I/O-bound work |
| `MaxConcurrencyPerKey` | 1 | Set higher if per-key parallelism is safe |
| `MaxTrackedOperations` | 200 | Balance visibility vs. memory |
| `MaxOperationLifetime` | 5 minutes | How long completed operations stay visible |
| `EnableFairScheduling` | false | Enable to prevent hot entity starvation |
| `FairSchedulingThreshold` | 10 | Max pending items per key before rejection |

---

## Snapshots

Operation snapshots contain only metadata, never payloads:

```csharp
public sealed record EphemeralOperationSnapshot(
    long Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string? Key,
    bool IsFaulted,
    Exception? Error,
    TimeSpan? Duration,
    IReadOnlyList<string>? Signals = null,
    bool IsPinned = false)
{
    public bool HasSignal(string signal) => Signals?.Contains(signal) == true;
}
```

For result-capturing coordinators:

```csharp
public sealed record EphemeralOperationSnapshot<TResult>(
    long Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string? Key,
    bool IsFaulted,
    Exception? Error,
    TimeSpan? Duration,
    TResult? Result,
    bool HasResult,
    IReadOnlyList<string>? Signals = null,
    bool IsPinned = false);
```

---

## Complete Example

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Named work coordinator
builder.Services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (req, ct) => await FastTranslateAsync(req, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

// Keyed coordinator with fair scheduling
builder.Services.AddEphemeralKeyedWorkCoordinator<string, UserCommand>("commands",
    cmd => cmd.UserId,
    sp =>
    {
        var handler = sp.GetRequiredService<ICommandHandler>();
        return async (cmd, ct) => await handler.HandleAsync(cmd, ct);
    },
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1,
        EnableFairScheduling = true,
        FairSchedulingThreshold = 10,
        CancelOnSignals = new HashSet<string> { "system-overload" }
    });

var app = builder.Build();
```

### Controller

```csharp
[ApiController]
[Route("api")]
public class WorkController : ControllerBase
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _translator;
    private readonly EphemeralKeyedWorkCoordinator<string, UserCommand> _commands;

    public WorkController(
        IEphemeralCoordinatorFactory<TranslationRequest> translationFactory,
        IEphemeralKeyedCoordinatorFactory<string, UserCommand> commandFactory)
    {
        _translator = translationFactory.CreateCoordinator("fast");
        _commands = commandFactory.CreateCoordinator("commands");
    }

    [HttpPost("translate")]
    public async Task<IActionResult> Translate([FromBody] TranslationRequest request)
    {
        await _translator.EnqueueAsync(request);
        return Ok(new { pending = _translator.PendingCount });
    }

    [HttpPost("command")]
    public IActionResult SubmitCommand([FromBody] UserCommand command)
    {
        if (!_commands.TryEnqueue(command))
            return StatusCode(429, "Too many pending commands for this user");
        return Ok();
    }

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new
    {
        translator = new
        {
            pending = _translator.PendingCount,
            active = _translator.ActiveCount,
            completed = _translator.TotalCompleted,
            failed = _translator.TotalFailed,
            hasRateLimit = _translator.HasSignal("rate-limit")
        },
        commands = new
        {
            pending = _commands.PendingCount,
            active = _commands.ActiveCount,
            errorCount = _commands.CountSignalsMatching("error.*")
        }
    });
}
```

### Health Endpoint

```csharp
[HttpGet("/health/detailed")]
public IActionResult GetDetailedHealth()
{
    return Ok(new
    {
        translation = new
        {
            pending = _translationCoordinator.PendingCount,
            active = _translationCoordinator.ActiveCount,
            recentRateLimits = _translationCoordinator.CountSignals("rate-limit"),
            recentTimeouts = _translationCoordinator.CountSignals("timeout"),
            recentSuccess = _translationCoordinator.CountSignals("success"),
            hasErrors = _translationCoordinator.HasSignalMatching("error.*")
        },
        payment = new
        {
            pending = _paymentCoordinator.PendingCount,
            gatewayErrors = _paymentCoordinator.CountSignals("gateway-error"),
            declines = _paymentCoordinator.CountSignals("declined"),
            approvals = _paymentCoordinator.CountSignals("approved")
        }
    });
}
```

### Signal-Based Circuit Breaker

```csharp
public class SignalBasedCircuitBreaker
{
    private readonly string _failureSignal;
    private readonly int _threshold;
    private readonly TimeSpan _windowSize;

    public SignalBasedCircuitBreaker(
        string failureSignal = "failure",
        int threshold = 5,
        TimeSpan? windowSize = null)
    {
        _failureSignal = failureSignal;
        _threshold = threshold;
        _windowSize = windowSize ?? TimeSpan.FromSeconds(30);
    }

    public bool IsOpen<T>(EphemeralWorkCoordinator<T> coordinator)
    {
        var recentFailures = coordinator.GetSignalsSince(
            DateTimeOffset.UtcNow - _windowSize);

        return recentFailures.Count(s => s.Signal == _failureSignal) >= _threshold;
    }
}

// Usage
var circuitBreaker = new SignalBasedCircuitBreaker("api-error", threshold: 3);

if (circuitBreaker.IsOpen(_coordinator))
{
    throw new CircuitOpenException("Too many recent API errors");
}

await _coordinator.EnqueueAsync(request);
```

### Adaptive Rate Limiting

```csharp
public class AdaptiveTranslationService
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _coordinator;

    public AdaptiveTranslationService()
    {
        _coordinator = new EphemeralWorkCoordinator<TranslationRequest>(
            ProcessTranslationAsync,
            new EphemeralOptions
            {
                MaxConcurrency = 8,
                MaxTrackedOperations = 100,
                DeferOnSignals = new HashSet<string> { "rate-limit" }
            });
    }

    public async Task TranslateAsync(TranslationRequest request)
    {
        // Check for rate limit signals with retry-after info
        var rateLimitSignals = _coordinator.GetSignalsByPattern("rate-limit:*");
        if (rateLimitSignals.Count > 0)
        {
            // Parse "rate-limit:5000ms" -> delay 5000ms
            var signal = rateLimitSignals.First().Signal;
            var delayMs = int.Parse(signal.Split(':')[1].TrimEnd('m', 's'));
            await Task.Delay(delayMs);
        }

        await _coordinator.EnqueueAsync(request);
    }
}
```

---

## Comparison with Alternatives

| Approach | Bounded | Tracking | Per-Key | Signals | Self-Cleaning | Complexity |
|----------|:-------:|:--------:|:-------:|:-------:|:-------------:|:----------:|
| `Parallel.ForEachAsync` | Yes | No | No | No | N/A | Low |
| TPL Dataflow | Yes | No | No | No | No | High |
| System.Threading.Channels | Yes | No | No | No | No | Medium |
| Polly | N/A | No | N/A | No | N/A | Low |
| MassTransit/NServiceBus | Yes | Yes | Yes | No | No | High |
| **Ephemeral Library** | Yes | Yes | Yes | Yes | Yes | Low |

### When to Use Alternatives

- **Parallel.ForEachAsync**: Simple parallel processing, no visibility needed
- **TPL Dataflow**: Complex pipeline topologies (fan-out, fan-in, conditional routing)
- **Channels**: Producer-consumer with maximum control
- **Polly**: Per-operation resilience (retry, circuit breaker)
- **MassTransit**: Distributed messaging with durability

### When to Use Ephemeral

- Need operation tracking and observability
- Need per-key sequential execution
- Need signal-based ambient awareness
- Need self-cleaning memory management
- Want a simple, in-process solution

---

## Performance Notes

### ID Generation

Uses XxHash64 for fast, allocation-free ID generation:

```csharp
// Allocation-free (stackalloc)
// Thread-safe (Interlocked.Increment)
// Unique across processes (includes process ID)
// Non-sequential (hash diffuses counter)
```

### Memory Management

- Operations don't store Task references - just counters
- Per-key locks are automatically cleaned up when idle > 60 seconds
- Cleanup is throttled to avoid lock contention on read paths

### Concurrency Gates

- `FixedConcurrencyGate`: SemaphoreSlim-backed, optimal hot-path performance
- `AdjustableConcurrencyGate`: Custom implementation for runtime adjustment

---

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions welcome! Please read the contributing guidelines first.
