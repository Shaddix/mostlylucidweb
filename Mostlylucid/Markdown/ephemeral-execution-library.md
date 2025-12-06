# Building a Reusable Ephemeral Execution Library

<!--category-- ASP.NET, Architecture, Systems Design, Async, DI -->
<datetime class="hidden">2025-12-12T14:00</datetime>

In **[Part 1: Fire and Don't *Quite* Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution)**, we explored the theory behind ephemeral execution - bounded, private, debuggable async workflows that remember just enough to be useful and then evaporate.

This article turns that pattern into a reusable library you can drop into any .NET project.

## Source Files

The library is split into well-factored files:

| File | Purpose |
|------|---------|
| [EphemeralOptions.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOptions.cs) | Configuration (concurrency, window size, lifetime, signals) |
| [EphemeralOperation.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOperation.cs) | Internal operation tracking with signal support |
| [Snapshots.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Snapshots.cs) | Immutable snapshot records exposed to consumers |
| [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs) | Signal events, propagation, constraints, and the global SignalSink |
| [EphemeralIdGenerator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralIdGenerator.cs) | Fast XxHash64-based ID generation |
| [ConcurrencyGates.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/ConcurrencyGates.cs) | Fixed and adjustable concurrency limiting |
| [StringPatternMatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/StringPatternMatcher.cs) | Glob-style pattern matching for signal filtering |
| [ParallelEphemeral.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/ParallelEphemeral.cs) | Static extension methods (`EphemeralForEachAsync`) |
| [EphemeralWorkCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralWorkCoordinator.cs) | Long-lived work queue coordinator |
| [EphemeralKeyedWorkCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralKeyedWorkCoordinator.cs) | Per-key sequential execution with fair scheduling |
| [EphemeralResultCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralResultCoordinator.cs) | Result-capturing coordinator variant |
| [SignalDispatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/SignalDispatcher.cs) | Async signal routing with pattern matching |
| [DependencyInjection.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/DependencyInjection.cs) | DI extension methods and factory implementations |
| [Examples/SignalingHttpClient.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Examples/SignalingHttpClient.cs) | Sample fine-grained signal emission for HTTP calls |

And [comprehensive tests](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid.Test/ParallelEphemeralTests.cs) covering all edge cases.

[TOC]

---

## Before and After

Here's what we're replacing:

```csharp
// ❌ Before: Fire-and-forget black hole
_ = Task.Run(() => ProcessAsync(item));
// No visibility. No debugging. No idea if it worked.

// ❌ Or: Blocking everything
await ProcessAsync(item);  // Hope you like waiting...
```

And what we're building:

```csharp
// ✅ After: Trackable, bounded, debuggable
await coordinator.EnqueueAsync(item);

// Instant visibility
Console.WriteLine($"Pending: {coordinator.PendingCount}");
Console.WriteLine($"Active: {coordinator.ActiveCount}");
Console.WriteLine($"Failed: {coordinator.TotalFailed}");

// Full operation history
var snapshot = coordinator.GetSnapshot();
var failures = coordinator.GetFailed();
```

Same async execution. Complete observability. No user data retained.

---

## Quick Start

The most common pattern - register a coordinator in DI and inject it:

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
        // Returns immediately - work happens in background
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

---

## Which Variant Do I Need?

```text
┌─────────────────────────────────────────────────────────────────┐
│                    DECISION TREE                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Processing a collection once?                                  │
│  └─► EphemeralForEachAsync<T> (ParallelEphemeral.cs)            │
│                                                                 │
│  Need a long-lived queue that accepts items over time?          │
│  └─► EphemeralWorkCoordinator<T>                                │
│                                                                 │
│  Need per-entity ordering (user commands, tenant jobs)?         │
│  └─► EphemeralKeyedWorkCoordinator<TKey, T>                     │
│                                                                 │
│  Need to capture results (fingerprints, summaries)?             │
│  └─► EphemeralResultCoordinator<TInput, TResult>                │
│                                                                 │
│  Need multiple coordinators with different configs?             │
│  └─► IEphemeralCoordinatorFactory<T> (like IHttpClientFactory)  │
│                                                                 │
│  Need dynamic concurrency adjustment at runtime?                │
│  └─► Set EnableDynamicConcurrency = true, call SetMaxConcurrency│
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## The Configuration Object

From [EphemeralOptions.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOptions.cs):

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

    // Async signal handling
    public Func<SignalEvent, CancellationToken, Task>? OnSignalAsync { get; init; }
    public int MaxConcurrentSignalHandlers { get; init; } = 4;
    public int MaxQueuedSignals { get; init; } = 1000;

    // Observability
    public Action<IReadOnlyCollection<EphemeralOperationSnapshot>>? OnSample { get; init; }
}
```

### Key Design Decisions

- **MaxConcurrency** defaults to CPU count - sensible for CPU-bound work. For I/O-bound work, increase it.
- **EnableDynamicConcurrency** enables runtime adjustment via `SetMaxConcurrency()` - uses a custom gate instead of `SemaphoreSlim`.
- **CancelOnSignals/DeferOnSignals** make coordinators signal-reactive - they respond to ambient system state (pattern matching supports `*`/`?`/comma lists).
- **OnSignal** is synchronous; for async fan-out use `SignalDispatcher` or `AsyncSignalProcessor` inside the handler.
- **SignalConstraints** prevents infinite signal loops with cycle detection and depth limits.

---

## The Snapshot Records

From [Snapshots.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Snapshots.cs):

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

// For result-capturing coordinators
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

This is **metadata only**. Notice what's *not* here:
- No payload
- No input data
- No user content

Just enough to answer "what happened, when, and did it work?" - nothing more.

---

## How This Compares to Other Approaches

.NET gives you several ways to do parallel work. Here's how the Ephemeral library compares:

### Parallel.ForEachAsync (.NET 6+)

```csharp
await Parallel.ForEachAsync(items,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (item, ct) => await ProcessAsync(item, ct));
```

**Best for**: Simple parallel processing of collections where you don't need visibility.

**What it lacks**:
- No operation tracking
- No per-key sequential execution
- No visibility into what's running

**Use Ephemeral when**: You need debugging/observability, per-key ordering, or signal-reactive processing.

### TPL Dataflow

```csharp
var block = new ActionBlock<T>(
    async item => await ProcessAsync(item),
    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

foreach (var item in items)
    block.Post(item);

block.Complete();
await block.Completion;
```

**Best for**: Complex dataflow pipelines with branching, merging, batching.

**What it does well**:
- Rich pipeline composition (link blocks together)
- Built-in batching, transforming, broadcasting
- Bounded capacity with back-pressure

**Use TPL Dataflow when**: You need complex pipeline topologies (fan-out, fan-in, conditional routing).

**Use Ephemeral when**: You need operation tracking, simpler API, or signal-reactive coordination.

### System.Threading.Channels

```csharp
var channel = Channel.CreateBounded<T>(100);

// Producer
foreach (var item in items)
    await channel.Writer.WriteAsync(item);
channel.Writer.Complete();

// Consumer (multiple workers)
var workers = Enumerable.Range(0, 4).Select(async _ =>
{
    await foreach (var item in channel.Reader.ReadAllAsync())
        await ProcessAsync(item);
});
await Task.WhenAll(workers);
```

**Best for**: Producer-consumer patterns where you control both sides.

**What it does well**:
- Excellent performance
- Back-pressure via bounded channels
- Separation of producers and consumers

**Use Channels when**: You're building custom infrastructure and need maximum control.

**Use Ephemeral when**: You want operation tracking and observability without the boilerplate.

### Polly

```csharp
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

await policy.ExecuteAsync(() => ProcessAsync(item));
```

**Best for**: Resilience policies (retry, circuit breaker, timeout) for individual operations.

**Use Polly when**: You need resilience around individual calls.

**Use Ephemeral when**: You need coordination across many operations with ambient awareness.

**Combine them**: Use Polly inside your Ephemeral work body for per-operation resilience.

### MassTransit / NServiceBus

**Best for**: Distributed messaging across services with durable queues.

**Use message buses when**: Work must survive process restarts, span multiple services, or require guaranteed delivery.

**Use Ephemeral when**: Work is in-process, doesn't need durability, and you want lightweight observability.

### Comparison Table

| Approach | Bounded | Tracking | Per-Key | Signals | Self-Cleaning | Complexity |
|----------|:-------:|:--------:|:-------:|:-------:|:-------------:|:----------:|
| `Parallel.ForEachAsync` | ✅ | ❌ | ❌ | ❌ | N/A | Low |
| TPL Dataflow | ✅ | ❌ | ❌ | ❌ | ❌ | High |
| Channels | ✅ | ❌ | ❌ | ❌ | ❌ | Medium |
| Polly | N/A | ❌ | N/A | ❌ | N/A | Low |
| Background Services | ❌ | ❌ | ❌ | ❌ | ❌ | Medium |
| MassTransit/NServiceBus | ✅ | ✅ | ✅ | ❌ | ❌ | High |
| **Ephemeral Library** | ✅ | ✅ | ✅ | ✅ | ✅ | Low |

---

## EphemeralForEachAsync: The One-Shot Version

From [ParallelEphemeral.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/ParallelEphemeral.cs):

```csharp
// Simple parallel processing with tracking
await items.EphemeralForEachAsync(
    async (item, ct) => await ProcessAsync(item, ct),
    new EphemeralOptions { MaxConcurrency = 8 });

// With keyed execution (per-user sequential)
await commands.EphemeralForEachAsync(
    cmd => cmd.UserId,  // Key selector
    async (cmd, ct) => await ExecuteCommandAsync(cmd, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1  // Sequential per user
    });
```

### Why Keyed Pipelines Matter

Imagine processing user commands:
- User A sends commands 1, 2, 3
- User B sends commands 4, 5, 6

Without keying, these might execute as: 1, 4, 2, 5, 3, 6 - interleaved.

With `MaxConcurrencyPerKey = 1`:
- User A's commands execute in order: 1 → 2 → 3
- User B's commands execute in order: 4 → 5 → 6
- But A and B can run in parallel

This is **per-entity sequential, globally parallel** - critical for systems where order matters within an entity.

---

## The Work Coordinator: A Long-Lived Queue

From [EphemeralWorkCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralWorkCoordinator.cs):

```csharp
await using var coordinator = new EphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) => await TranslateAsync(request, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 8,
        MaxTrackedOperations = 500,
        EnableDynamicConcurrency = true  // Allow runtime adjustment
    });

// Enqueue items over time
await coordinator.EnqueueAsync(new TranslationRequest("Hello", "es"));

// Check status anytime
Console.WriteLine($"Pending: {coordinator.PendingCount}");
Console.WriteLine($"Active: {coordinator.ActiveCount}");

// Get snapshots
var snapshot = coordinator.GetSnapshot();
var running = coordinator.GetRunning();
var failed = coordinator.GetFailed();
var completed = coordinator.GetCompleted();

// Control flow
coordinator.Pause();   // Stop pulling new work
coordinator.Resume();  // Continue

// Adjust concurrency at runtime (requires EnableDynamicConcurrency)
coordinator.SetMaxConcurrency(16);

// Pin important operations to survive eviction
coordinator.Pin(operationId);
coordinator.Unpin(operationId);
coordinator.Evict(operationId);

// When done
coordinator.Complete();
await coordinator.DrainAsync();
```

### Continuous Streams with IAsyncEnumerable

```csharp
await using var coordinator = EphemeralWorkCoordinator<Message>.FromAsyncEnumerable(
    messageStream,  // IAsyncEnumerable<Message>
    async (msg, ct) => await ProcessMessageAsync(msg, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

await coordinator.DrainAsync();
```

---

## The Keyed Coordinator: Per-Entity Pipelines

From [EphemeralKeyedWorkCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralKeyedWorkCoordinator.cs):

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

// TryEnqueue returns false if fair scheduling rejects
if (!coordinator.TryEnqueue(hotUserCommand))
{
    await DeferCommandAsync(hotUserCommand);
}

// Per-key visibility
var pendingForUser = coordinator.GetPendingCountForKey("user-123");
var opsForUser = coordinator.GetSnapshotForKey("user-123");
```

---

## Result-Capturing Coordinators

From [EphemeralResultCoordinator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralResultCoordinator.cs):

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

// Get just the results (no metadata)
var results = coordinator.GetResults();

// Get snapshots with results + metadata
var snapshots = coordinator.GetSnapshot();

// Get base snapshots without results (privacy-safe)
var baseSnapshots = coordinator.GetBaseSnapshot();

// Filter by success/failure
var successful = coordinator.GetSuccessful();
var failed = coordinator.GetFailed();
```

---

## Concurrency Control

From [ConcurrencyGates.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/ConcurrencyGates.cs):

The library provides two concurrency control mechanisms:

### FixedConcurrencyGate (Default)
- Backed by `SemaphoreSlim`
- Optimal hot-path performance
- Cannot be adjusted at runtime

### AdjustableConcurrencyGate
- Custom implementation with `Queue<WaiterEntry>`
- Supports `UpdateLimit()` at runtime
- Enabled via `EnableDynamicConcurrency = true`

```csharp
// Dynamic concurrency adjustment
var coordinator = new EphemeralWorkCoordinator<T>(body,
    new EphemeralOptions
    {
        MaxConcurrency = 4,
        EnableDynamicConcurrency = true
    });

// Later, based on system load:
coordinator.SetMaxConcurrency(16);  // Scale up
coordinator.SetMaxConcurrency(2);   // Scale down
```

---

## The Factory Pattern: Named Coordinators

From [DependencyInjection.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/DependencyInjection.cs):

Like `IHttpClientFactory`, you can register named configurations:

```csharp
// Registration
services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (request, ct) => await FastTranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 32 });

services.AddEphemeralWorkCoordinator<TranslationRequest>("accurate",
    async (request, ct) => await AccurateTranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 4 });

// Usage
public class TranslationService(IEphemeralCoordinatorFactory<TranslationRequest> factory)
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _fast =
        factory.CreateCoordinator("fast");
    private readonly EphemeralWorkCoordinator<TranslationRequest> _accurate =
        factory.CreateCoordinator("accurate");
}
```

### Factory Guarantees

1. **Same name = same instance** - Calling `CreateCoordinator("fast")` twice returns the same coordinator
2. **Different names = different instances** - `"fast"` and `"accurate"` get separate coordinators
3. **Lazy creation** - Coordinators are only created when first requested
4. **Configuration validation** - Requesting an unregistered name throws a helpful error

---

## Signal Querying API

All coordinators provide optimised signal querying methods:

```csharp
// Get all signals
var signals = coordinator.GetSignals();

// Filter by key (zero-allocation)
var userSignals = coordinator.GetSignalsByKey("user-123");

// Filter by time range
var recentSignals = coordinator.GetSignalsSince(DateTimeOffset.UtcNow.AddMinutes(-5));
var rangeSignals = coordinator.GetSignalsByTimeRange(from, to);

// Filter by signal name or pattern
var rateSignals = coordinator.GetSignalsByName("rate-limit");
var httpSignals = coordinator.GetSignalsByPattern("http.*");

// Check existence (short-circuits on first match)
if (coordinator.HasSignal("rate-limit"))
    await ThrottleAsync();

if (coordinator.HasSignalMatching("error.*"))
    await AlertAsync();

// Count signals efficiently (no allocation)
var totalSignals = coordinator.CountSignals();
var errorCount = coordinator.CountSignals("error");
var httpCount = coordinator.CountSignalsMatching("http.*");
```

---

## Production Optimisations

### Fast ID Generation

From [EphemeralIdGenerator.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralIdGenerator.cs):

```csharp
internal static class EphemeralIdGenerator
{
    private static long _counter;
    private static readonly long _processStart = Environment.TickCount64;
    private static readonly int _processId = Environment.ProcessId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NextId()
    {
        var counter = Interlocked.Increment(ref _counter);

        // Combine counter with process-unique seed
        Span<byte> buffer = stackalloc byte[24];
        BitConverter.TryWriteBytes(buffer, _processStart);
        BitConverter.TryWriteBytes(buffer.Slice(8), _processId);
        BitConverter.TryWriteBytes(buffer.Slice(16), counter);

        return unchecked((long)XxHash64.HashToUInt64(buffer));
    }
}
```

- **Allocation-free** (uses `stackalloc`)
- **Thread-safe** (uses `Interlocked.Increment`)
- **Unique across processes** (includes process ID)
- **Non-sequential** (hash diffuses the counter)

### Memory-Safe Long-Lived Operation

The coordinators don't store `Task` references - just counters:

```csharp
private int _activeTaskCount;
private readonly TaskCompletionSource _drainTcs;

// In ExecuteItemAsync:
finally
{
    // Signal drain when last task completes AND channel iteration is done
    if (Interlocked.Decrement(ref _activeTaskCount) == 0 &&
        Volatile.Read(ref _channelIterationComplete))
    {
        _drainTcs.TrySetResult();
    }
}
```

### Per-Key Lock Cleanup

The keyed coordinator automatically cleans up idle per-key semaphores:

```csharp
private sealed class KeyLock(SemaphoreSlim gate, int maxCount)
{
    public SemaphoreSlim Gate { get; } = gate;
    public int MaxCount { get; } = maxCount;
    public long LastUsedTicks = Environment.TickCount64;
}

// Cleanup runs periodically, removes locks idle > 60 seconds
```

---

## Complete Example

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Named coordinators
builder.Services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (req, ct) => await FastTranslateAsync(req, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

// Keyed coordinator for per-user commands
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
        CancelOnSignals = new HashSet<string> { "system-overload" }
    });

var app = builder.Build();
```

```csharp
// Controller
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

---

## Conclusion

We've built a complete ephemeral execution library with:

1. **`EphemeralForEachAsync`** - One-shot parallel processing with tracking
2. **`EphemeralWorkCoordinator`** - Long-lived observable queues
3. **`EphemeralKeyedWorkCoordinator`** - Per-entity sequential execution with fair scheduling
4. **`EphemeralResultCoordinator`** - Result-capturing variant
5. **Factory pattern** - Named configurations like `IHttpClientFactory`
6. **Dynamic concurrency** - Runtime adjustment of parallelism
7. **Signal infrastructure** - Built-in signal emission and querying

The pattern sits in a sweet spot:
- More observable than `Parallel.ForEachAsync`
- Simpler than TPL Dataflow
- More integrated than raw Channels
- Privacy-safe by design

**Fire... and Don't Quite Forget.**

---

## Links

- [Part 1: Fire and Don't *Quite* Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution) - the theory and pattern
- [Part 3: Ephemeral Signals](/blog/ephemeral-signals) - turning atoms into a sensing network
- [SemaphoreSlim documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim)
- [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [TPL Dataflow](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)
- [IHttpClientFactory pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests)
