# Building a Reusable Ephemeral Execution Library

<!--category-- ASP.NET, Architecture, Systems Design, Async, DI -->
<datetime class="hidden">2025-12-12T14:00</datetime>

In **[Part 1: Fire and Don't *Quite* Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution)**, we explored the theory behind ephemeral execution -bounded, private, debuggable async workflows that remember just enough to be useful and then evaporate.

This article turns that pattern into a reusable library you can drop into any .NET project:

[See here for the actual utility](https://github.com/scottgal/mostlylucidweb/blob/features/referrers/Mostlylucid/Helpers/ParallelEphemeral.cs). And [here for its tests](https://github.com/scottgal/mostlylucidweb/blob/features/referrers/Mostlylucid.Test/ParallelEphemeralTests.cs) (78 tests covering all the edge cases).

I'll make this into a NuGet package shortly, but for now...

- `EphemeralForEachAsync<T>` - like `Parallel.ForEachAsync` but with operation tracking
- Keyed pipelines for per-entity sequential execution
- `EphemeralWorkCoordinator<T>` - a long-lived observable work queue
- Named/typed coordinators with `IEphemeralCoordinatorFactory<T>` (like `IHttpClientFactory`)
- Full DI integration with scoped and singleton lifetimes
- Comparison with other approaches (TPL Dataflow, Channels, Background Services)

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
│  └─► EphemeralForEachAsync<T>                                   │
│                                                                 │
│  Need a long-lived queue that accepts items over time?          │
│  └─► EphemeralWorkCoordinator<T>                                │
│                                                                 │
│  Need per-entity ordering (user commands, tenant jobs)?         │
│  └─► EphemeralKeyedWorkCoordinator<T, TKey>                     │
│                                                                 │
│  Need to capture results (fingerprints, summaries)?             │
│  └─► EphemeralWorkCoordinator<TInput, TResult>                  │
│                                                                 │
│  Need multiple coordinators with different configs?             │
│  └─► IEphemeralCoordinatorFactory<T> (like IHttpClientFactory)  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## The Full Implementation

Let's build the complete library piece by piece.

### The Configuration Object

First, we define what's configurable:

```csharp
public sealed class EphemeralOptions
{
    /// <summary>
    /// Max number of operations to run concurrently overall.
    /// Default: number of CPU cores.
    /// </summary>
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Max number of operations to retain in the in-memory window.
    /// Oldest entries are dropped first (LRU-style).
    /// </summary>
    public int MaxTrackedOperations { get; init; } = 200;

    /// <summary>
    /// Optional max age for tracked operations.
    /// Older entries are dropped during cleanup sweeps.
    /// </summary>
    public TimeSpan? MaxOperationLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Optional callback to observe a snapshot of the current window.
    /// Runs on the caller's thread after each operation completes — keep it cheap.
    /// </summary>
    public Action<IReadOnlyCollection<EphemeralOperationSnapshot>>? OnSample { get; init; }

    /// <summary>
    /// Max concurrency per key for keyed pipelines.
    /// Default 1 = strictly sequential execution per key.
    /// Only used by the keyed overload.
    /// </summary>
    public int MaxConcurrencyPerKey { get; init; } = 1;

    /// <summary>
    /// Enable fair scheduling across keys.
    /// When enabled, keys with more pending work are deprioritized
    /// to prevent hot keys from starving cold keys.
    /// Default: false (FIFO ordering).
    /// </summary>
    public bool EnableFairScheduling { get; init; } = false;

    /// <summary>
    /// Maximum pending items per key before new items for that key are deprioritized.
    /// Only used when EnableFairScheduling is true.
    /// Default: 10.
    /// </summary>
    public int FairSchedulingThreshold { get; init; } = 10;
}
```

Key design decisions:

- **MaxConcurrency** defaults to CPU count - a sensible starting point for CPU-bound work. For I/O-bound work (like HTTP calls), you'd typically increase this.
- **MaxTrackedOperations** creates the bounded window. 200 is enough for debugging without eating memory.
- **MaxOperationLifetime** adds time-based eviction on top of size-based. Old entries disappear even if the window isn't full.
- **OnSample** lets you hook into the system for metrics, logging, or debugging without modifying the core.
- **MaxConcurrencyPerKey** enables per-entity sequential pipelines - critical for things like "process all commands for user X in order".
- **EnableFairScheduling** prevents hot keys from starving cold ones.

### The Snapshot Record

What gets exposed to observers:

```csharp
public sealed record EphemeralOperationSnapshot(
    long Id,              // Fast XxHash64-based ID (see optimisation section below)
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string? Key,
    bool IsFaulted,
    Exception? Error,
    TimeSpan? Duration);
```

This is **metadata only**. Notice what's *not* here:
- No payload
- No input data
- No result data
- No user content

Just enough to answer "what happened, when, and did it work?" - nothing more.

### The Internal Operation Tracker

The actual tracking object is internal - callers only see snapshots:

```csharp
internal sealed class EphemeralOperation
{
    public long Id { get; } = EphemeralIdGenerator.NextId();  // Fast XxHash64-based ID
    public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Completed { get; set; }
    public Exception? Error { get; set; }
    public string? Key { get; init; }

    public TimeSpan? Duration =>
        Completed is { } done ? done - Started : null;

    public EphemeralOperationSnapshot ToSnapshot() =>
        new(Id, Started, Completed, Key, IsFaulted: Error != null, Error, Duration);
}
```

The `Duration` property is computed on-demand - we store timestamps, not durations. This means in-progress operations show `null` duration, and completed ones compute it from the timestamps.

Note: We use a custom `EphemeralIdGenerator` instead of `Guid.NewGuid()` for performance - see the [optimisation section](#optimising-for-production-fixing-memory-leaks-and-improving-performance) below.

---

## EphemeralForEachAsync: The One-Shot Version

Here's the main loop for processing a collection:

```csharp
public static class ParallelEphemeral
{
    public static async Task EphemeralForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new EphemeralOptions();

        // Semaphore acts as the "governor" — limits concurrent work
        using var concurrency = new SemaphoreSlim(options.MaxConcurrency);

        // The rolling window of recent operations
        var recent = new ConcurrentQueue<EphemeralOperation>();

        // Track all spawned tasks so we can await them at the end
        var running = new ConcurrentBag<Task>();

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Wait for a slot — this is where back-pressure happens
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);

            // Create the tracking object
            var op = new EphemeralOperation();
            EnqueueEphemeral(op, recent, options);

            // Fire off the work — don't await, just track
            var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, concurrency);
            running.Add(task);
        }

        // Wait for all in-flight work to complete
        await Task.WhenAll(running).ConfigureAwait(false);
    }
}
```

### The Semaphore as Governor

```csharp
using var concurrency = new SemaphoreSlim(options.MaxConcurrency);
// ...
await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
```

A [SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) is a lightweight synchronisation primitive that limits how many threads can access a resource. Here, we're using it to limit concurrent operations.

When you call `WaitAsync()`:
- If there's capacity (count > 0), it decrements and returns immediately
- If there's no capacity (count = 0), it blocks until someone calls `Release()`

This creates **back-pressure**: if you're at max concurrency, new items wait instead of spawning more tasks. The system stays stable under load.

### The Execution Wrapper

Both the keyed and non-keyed versions share a single execution wrapper. The trick? Use `params` to accept any number of semaphores to release:

```csharp
private static async Task ExecuteAsync<T>(
    T item,
    Func<T, CancellationToken, Task> body,
    EphemeralOperation op,
    ConcurrentQueue<EphemeralOperation> recent,
    EphemeralOptions options,
    CancellationToken cancellationToken,
    params SemaphoreSlim[] semaphores)
{
    try
    {
        await body(item, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
    {
        // Capture the error but don't rethrow — we're tracking, not failing
        op.Error = ex;
    }
    finally
    {
        // Always runs: mark complete, release all slots, cleanup
        op.Completed = DateTimeOffset.UtcNow;
        foreach (var semaphore in semaphores)
            semaphore.Release();
        CleanupWindow(recent, options);
        SampleIfRequested(recent, options);
    }
}
```

The non-keyed version calls it with one semaphore:
```csharp
var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, concurrency);
```

The keyed version calls it with two (key gate first, then global):
```csharp
var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, keyGate, globalConcurrency);
```

No duplication. Same guarantees. The `params` pattern lets us handle both cases without separate methods.

### The Cleanup Logic

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void EnqueueEphemeral(
    EphemeralOperation op,
    ConcurrentQueue<EphemeralOperation> recent,
    EphemeralOptions options)
{
    recent.Enqueue(op);
    CleanupWindow(recent, options);
}

private static void CleanupWindow(
    ConcurrentQueue<EphemeralOperation> recent,
    EphemeralOptions options)
{
    // Size-based eviction: drop oldest until we're under the limit
    while (recent.Count > options.MaxTrackedOperations &&
           recent.TryDequeue(out _))
    {
    }

    // Age-based eviction: drop entries older than max lifetime
    if (options.MaxOperationLifetime is { } maxAge &&
        recent.TryPeek(out var head))
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;

        while (head is not null && head.Started < cutoff &&
               recent.TryDequeue(out _))
        {
            if (!recent.TryPeek(out head))
                break;
        }
    }
}
```

The cleanup is deliberately simple:

1. **Size-based**: If we're over capacity, dequeue until we're not. The oldest entries go first (FIFO).
2. **Age-based**: If an entry is older than the max lifetime, drop it.

This is "best effort" - we don't lock or synchronise heavily. In a high-throughput system, slight over-capacity is fine. The important thing is that we don't grow unbounded.

---

## The Keyed Version: Per-Entity Pipelines

Now the interesting one - keyed execution:

```csharp
public static async Task EphemeralForEachAsync<T, TKey>(
    this IEnumerable<T> source,
    Func<T, TKey> keySelector,
    Func<T, CancellationToken, Task> body,
    EphemeralOptions? options = null,
    CancellationToken cancellationToken = default)
    where TKey : notnull
{
    options ??= new EphemeralOptions();

    // Global concurrency limit
    using var globalConcurrency = new SemaphoreSlim(options.MaxConcurrency);

    // Per-key concurrency limits (created lazily)
    var perKeyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();

    var recent = new ConcurrentQueue<EphemeralOperation>();
    var running = new ConcurrentBag<Task>();

    foreach (var item in source)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = keySelector(item);

        // Get or create the per-key semaphore
        var keyGate = perKeyLocks.GetOrAdd(
            key,
            _ => new SemaphoreSlim(options.MaxConcurrencyPerKey));

        // Must acquire BOTH: global slot AND per-key slot
        await globalConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        await keyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var op = new EphemeralOperation { Key = key?.ToString() };
        EnqueueEphemeral(op, recent, options);

        // Same ExecuteAsync, but with two semaphores (key gate + global)
        var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, keyGate, globalConcurrency);
        running.Add(task);
    }

    await Task.WhenAll(running).ConfigureAwait(false);

    // Cleanup: dispose all per-key semaphores
    foreach (var gate in perKeyLocks.Values)
    {
        gate.Dispose();
    }
}
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

This is **per-entity sequential, globally parallel** - critical for systems where order matters within an entity but not across entities.

---

## How This Compares to Other Approaches

.NET gives you several ways to do parallel work. Here's how `EphemeralForEachAsync` stacks up:

### Parallel.ForEachAsync (.NET 6+)

```csharp
await Parallel.ForEachAsync(items,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (item, ct) => await ProcessAsync(item, ct));
```

**What it does well:**
- Built into the framework
- Simple API
- Bounded concurrency

**What it lacks:**
- No operation tracking whatsoever
- No visibility into what's running
- No per-key sequential execution
- Fire-and-forget with no forensics

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

**What it does well:**
- Rich pipeline composition (link blocks together)
- Bounded capacity (back-pressure)
- Built-in batching, transforming, broadcasting

**What it lacks:**
- No built-in operation tracking
- Heavier weight (designed for complex dataflows)
- Steeper learning curve
- Overkill for simple parallel loops

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

**What it does well:**
- Excellent for producer-consumer patterns
- Back-pressure via bounded channels
- Can separate producers and consumers

**What it lacks:**
- No operation tracking built-in
- Requires more boilerplate
- You manage concurrency yourself

### The Comparison Table

| Approach | Bounded Concurrency | Operation Tracking | Per-Key Sequential | Self-Cleaning | Complexity |
|----------|:------------------:|:-----------------:|:------------------:|:-------------:|:----------:|
| `Parallel.ForEachAsync` | ✅ | ❌ | ❌ | N/A | Low |
| TPL Dataflow | ✅ | ❌ | ❌ | ❌ | High |
| Channels | ✅ (manual) | ❌ | ❌ | ❌ | Medium |
| Background + Queue | ❌ | ❌ | ❌ | ❌ | Medium |
| **EphemeralForEachAsync** | ✅ | ✅ | ✅ | ✅ | Low |

---

## The Work Coordinator: A Long-Lived Queue

The `EphemeralForEachAsync` extension is great for processing a collection, but what if you need:
- A **long-lived queue** that accepts items continuously?
- The ability to **inspect operations** at any time?
- **DI integration** so the coordinator lives as a service?

### EphemeralWorkCoordinator: A Persistent Reference

Instead of processing a collection and finishing, the `EphemeralWorkCoordinator` stays alive:

```csharp
// Create a coordinator that processes items as they arrive
await using var coordinator = new EphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) =>
    {
        await TranslateAsync(request, ct);
    },
    new EphemeralOptions
    {
        MaxConcurrency = 8,
        MaxTrackedOperations = 500
    });

// Enqueue items over time
await coordinator.EnqueueAsync(new TranslationRequest("Hello", "es"));
await coordinator.EnqueueAsync(new TranslationRequest("World", "fr"));

// Check status anytime
Console.WriteLine($"Pending: {coordinator.PendingCount}");
Console.WriteLine($"Active: {coordinator.ActiveCount}");
Console.WriteLine($"Completed: {coordinator.TotalCompleted}");
Console.WriteLine($"Failed: {coordinator.TotalFailed}");

// Get a snapshot of recent operations
var snapshot = coordinator.GetSnapshot();
var running = coordinator.GetRunning();
var failed = coordinator.GetFailed();

// When done, signal completion and drain
coordinator.Complete();
await coordinator.DrainAsync();
```

### Continuous Streams with IAsyncEnumerable

For scenarios where work arrives as a stream (message queues, event sources, etc.), use `FromAsyncEnumerable`:

```csharp
// Create a coordinator that consumes from a stream
await using var coordinator = EphemeralWorkCoordinator<Message>.FromAsyncEnumerable(
    messageStream,  // IAsyncEnumerable<Message>
    async (msg, ct) => await ProcessMessageAsync(msg, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

// It runs until the stream completes or cancellation
await coordinator.DrainAsync();
```

### Fair Scheduling for Keyed Workloads

When processing keyed items (per-user, per-tenant), enable fair scheduling to prevent hot keys from starving cold ones:

```csharp
await using var coordinator = new EphemeralKeyedWorkCoordinator<Command, string>(
    cmd => cmd.UserId,
    async (cmd, ct) => await ExecuteCommandAsync(cmd, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1,      // Per-user sequential
        EnableFairScheduling = true,   // Prevent hot user starvation
        FairSchedulingThreshold = 10   // Reject if user has 10+ pending
    });

// TryEnqueue returns false if fair scheduling rejects the item
if (!coordinator.TryEnqueue(hotUserCommand))
{
    // User has too many pending commands - reject or defer
    await DeferCommandAsync(hotUserCommand);
}
```

---

## The Factory Pattern: Named Coordinators (Like AddHttpClient)

If you've used `IHttpClientFactory`, you know the pattern: register named configurations, then inject a factory that creates instances on demand. We can do the same for coordinators.

### Why Use a Factory?

Consider a service that needs multiple coordinators with different configurations:

```csharp
public class TranslationService
{
    // Problem: How do you inject TWO coordinators with different configs?
    // Solution: Use a factory!
}
```

### Registration

```csharp
// In Program.cs
services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (request, ct) => await TranslateFastAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 32 });

services.AddEphemeralWorkCoordinator<TranslationRequest>("accurate",
    async (request, ct) => await TranslateAccurateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = 4 });

// With service provider access for DI
services.AddEphemeralWorkCoordinator<OrderRequest>("orders",
    sp =>
    {
        var db = sp.GetRequiredService<IDatabase>();
        var logger = sp.GetRequiredService<ILogger<OrderProcessor>>();
        return async (order, ct) =>
        {
            await db.SaveOrderAsync(order, ct);
            logger.LogInformation("Processed order {OrderId}", order.Id);
        };
    },
    new EphemeralOptions { MaxConcurrency = 16 });
```

### Usage

```csharp
public class TranslationController : Controller
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _fastCoordinator;
    private readonly EphemeralWorkCoordinator<TranslationRequest> _accurateCoordinator;

    public TranslationController(IEphemeralCoordinatorFactory<TranslationRequest> factory)
    {
        // Get named coordinators from the factory
        _fastCoordinator = factory.CreateCoordinator("fast");
        _accurateCoordinator = factory.CreateCoordinator("accurate");
    }

    [HttpPost("fast")]
    public async Task<IActionResult> TranslateFast([FromBody] TranslationRequest request)
    {
        await _fastCoordinator.EnqueueAsync(request);
        return Ok(new { pending = _fastCoordinator.PendingCount });
    }

    [HttpPost("accurate")]
    public async Task<IActionResult> TranslateAccurate([FromBody] TranslationRequest request)
    {
        await _accurateCoordinator.EnqueueAsync(request);
        return Ok(new { pending = _accurateCoordinator.PendingCount });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            fast = new
            {
                pending = _fastCoordinator.PendingCount,
                active = _fastCoordinator.ActiveCount,
                completed = _fastCoordinator.TotalCompleted
            },
            accurate = new
            {
                pending = _accurateCoordinator.PendingCount,
                active = _accurateCoordinator.ActiveCount,
                completed = _accurateCoordinator.TotalCompleted
            }
        });
    }
}
```

### Factory Behaviour

The factory provides several guarantees:

1. **Same name = same instance**: Calling `CreateCoordinator("fast")` twice returns the same coordinator instance.
2. **Different names = different instances**: `"fast"` and `"accurate"` get separate coordinators.
3. **Lazy creation**: Coordinators are only created when first requested.
4. **Configuration validation**: Requesting an unregistered name throws a helpful error.

### Keyed Factory

For keyed coordinators:

```csharp
services.AddEphemeralKeyedWorkCoordinator<Command, string>("user-commands",
    cmd => cmd.UserId,
    async (cmd, ct) => await ExecuteCommandAsync(cmd, ct),
    new EphemeralOptions
    {
        MaxConcurrency = 32,
        MaxConcurrencyPerKey = 1,
        EnableFairScheduling = true
    });

// Then inject
public class CommandService
{
    private readonly EphemeralKeyedWorkCoordinator<Command, string> _coordinator;

    public CommandService(IEphemeralKeyedCoordinatorFactory<Command, string> factory)
    {
        _coordinator = factory.CreateCoordinator("user-commands");
    }
}
```

---

## Basic DI Integration

For simpler cases where you just need one coordinator, use the direct registration methods:

### Singleton Coordinators

```csharp
// Register a singleton coordinator
services.AddEphemeralWorkCoordinator<TranslationRequest>(
    sp =>
    {
        var translator = sp.GetRequiredService<ITranslator>();
        return async (request, ct) => await translator.TranslateAsync(request, ct);
    },
    new EphemeralOptions { MaxConcurrency = 8 });

// Inject directly
public class TranslationService
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _coordinator;

    public TranslationService(EphemeralWorkCoordinator<TranslationRequest> coordinator)
    {
        _coordinator = coordinator;
    }
}
```

### Scoped Coordinators

For per-request coordinators (useful for batch operations within a request scope):

```csharp
services.AddScopedEphemeralWorkCoordinator<BatchItem>(
    _ => async (item, ct) => await ProcessBatchItemAsync(item, ct),
    new EphemeralOptions { MaxConcurrency = 4 });
```

Each scope gets its own coordinator instance that's disposed when the scope ends.

---

## Result-Capturing Coordinators

Sometimes you don't just want to track *that* work happened - you want to capture *what* it produced. The result-capturing variant stores operation outcomes in the ephemeral window:

```csharp
// Coordinator that captures results
await using var coordinator = new EphemeralWorkCoordinator<SessionInput, SessionResult>(
    async (input, ct) =>
    {
        // Process the session and return a summary
        var fingerprint = await ComputeFingerprintAsync(input.Events, ct);
        return new SessionResult(
            Fingerprint: fingerprint,
            EventCount: input.Events.Length,
            Duration: input.Duration);
    },
    new EphemeralOptions { MaxConcurrency = 16 });

// Enqueue sessions
await coordinator.EnqueueAsync(new SessionInput("sess-1", events, duration));
await coordinator.EnqueueAsync(new SessionInput("sess-2", events2, duration2));

coordinator.Complete();
await coordinator.DrainAsync();

// Get just the results (no metadata)
var results = coordinator.GetResults();
// => [SessionResult { Fingerprint: "abc123", EventCount: 42, ... }, ...]

// Or get full snapshots with results + metadata
var snapshots = coordinator.GetSnapshot();
foreach (var op in snapshots.Where(s => s.HasResult))
{
    Console.WriteLine($"{op.Id}: {op.Result.Fingerprint} ({op.Duration})");
}

// Or strip results for logging (privacy-safe)
var baseSnapshots = coordinator.GetBaseSnapshot();
// => No Result field, just Id, Started, Completed, Duration, etc.
```

### Use Cases

- **Session fingerprinting**: Process user sessions, capture behavioural fingerprints
- **Batch ETL**: Transform records, capture row counts or checksums
- **Scoped → Singleton pipelines**: A scoped coordinator processes request items, captures summaries, then feeds those into a singleton aggregator
- **Logging pipelines**: Track log batches, capture "written X lines to Y destination"

### The Pattern

The result type is yours to define. Keep it lightweight:

```csharp
// Good - lightweight summary
public record OrderResult(string OrderId, decimal Total, bool Shipped);

// Good - just an ID for later lookup
public record ProcessResult(string ResultId);

// Avoid - entire domain object (defeats the purpose)
public record BadResult(Order FullOrder, List<LineItem> Items, Customer Customer);
```

The coordinator still cleans up based on `MaxTrackedOperations` and `MaxOperationLifetime`. Results evaporate with their operations.

---

## The Full Feature Set

| Feature | EphemeralForEachAsync | EphemeralWorkCoordinator&lt;T&gt; | EphemeralWorkCoordinator&lt;T,R&gt; |
|---------|:--------------------:|:------------------------:|:------------------------:|
| Processes a collection | ✅ | ✅ | ✅ |
| Continuous enqueue | ❌ | ✅ | ✅ |
| IAsyncEnumerable source | ❌ | ✅ | ✅ |
| Inspect at runtime | Via OnSample | GetSnapshot() | GetSnapshot() + GetResults() |
| Captures results | ❌ | ❌ | ✅ |
| Per-key sequential | ✅ | Via Keyed variant | ❌ (add if needed) |
| DI integration | Manual | Extension methods | Extension methods |

---

## Putting It All Together

Here's a complete example showing all the patterns:

```csharp
// Program.cs - Registration
var builder = WebApplication.CreateBuilder(args);

// Named coordinators via factory
builder.Services.AddEphemeralWorkCoordinator<TranslationRequest>("fast",
    async (req, ct) => await FastTranslateAsync(req, ct),
    new EphemeralOptions { MaxConcurrency = 16 });

builder.Services.AddEphemeralWorkCoordinator<TranslationRequest>("quality",
    async (req, ct) => await QualityTranslateAsync(req, ct),
    new EphemeralOptions { MaxConcurrency = 4 });

// Keyed coordinator for per-user command processing
builder.Services.AddEphemeralKeyedWorkCoordinator<UserCommand, string>("commands",
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
        FairSchedulingThreshold = 5
    });

var app = builder.Build();
```

```csharp
// Controller - Usage
[ApiController]
[Route("api")]
public class WorkController : ControllerBase
{
    private readonly EphemeralWorkCoordinator<TranslationRequest> _fastTranslator;
    private readonly EphemeralWorkCoordinator<TranslationRequest> _qualityTranslator;
    private readonly EphemeralKeyedWorkCoordinator<UserCommand, string> _commandProcessor;

    public WorkController(
        IEphemeralCoordinatorFactory<TranslationRequest> translationFactory,
        IEphemeralKeyedCoordinatorFactory<UserCommand, string> commandFactory)
    {
        _fastTranslator = translationFactory.CreateCoordinator("fast");
        _qualityTranslator = translationFactory.CreateCoordinator("quality");
        _commandProcessor = commandFactory.CreateCoordinator("commands");
    }

    [HttpPost("translate/fast")]
    public async Task<IActionResult> TranslateFast([FromBody] TranslationRequest request)
    {
        await _fastTranslator.EnqueueAsync(request);
        return Ok(new { queued = true, pending = _fastTranslator.PendingCount });
    }

    [HttpPost("command")]
    public IActionResult SubmitCommand([FromBody] UserCommand command)
    {
        // TryEnqueue respects fair scheduling - returns false if user is over threshold
        if (!_commandProcessor.TryEnqueue(command))
        {
            return StatusCode(429, new { error = "Too many pending commands for this user" });
        }

        return Ok(new { queued = true });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            fastTranslator = new
            {
                pending = _fastTranslator.PendingCount,
                active = _fastTranslator.ActiveCount,
                completed = _fastTranslator.TotalCompleted,
                failed = _fastTranslator.TotalFailed
            },
            qualityTranslator = new
            {
                pending = _qualityTranslator.PendingCount,
                active = _qualityTranslator.ActiveCount,
                completed = _qualityTranslator.TotalCompleted,
                failed = _qualityTranslator.TotalFailed
            },
            commandProcessor = new
            {
                pending = _commandProcessor.PendingCount,
                active = _commandProcessor.ActiveCount,
                completed = _commandProcessor.TotalCompleted,
                failed = _commandProcessor.TotalFailed,
                recentOperations = _commandProcessor.GetSnapshot().Take(10)
            }
        });
    }
}
```

---

## Optimising for Production: Fixing Memory Leaks and Improving Performance

When you build a library like this for production use, especially as a long-lived singleton, there are subtle issues that can bite you. Here's what we fixed and how.

### The Problem: Unbounded Task Accumulation

The original implementation tracked running tasks in a `ConcurrentBag<Task>`:

```csharp
// ❌ ORIGINAL - Memory leak in long-lived coordinators
private readonly ConcurrentBag<Task> _runningTasks;

private async Task ProcessAsync()
{
    await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
    {
        var task = ExecuteItemAsync(item, op);
        _runningTasks.Add(task);  // Added but NEVER removed!
    }

    await Task.WhenAll(_runningTasks.ToArray());
}
```

This works fine for short-lived coordinators, but for singletons that run for the lifetime of your application, `_runningTasks` grows forever. Every completed task stays in memory.

### The Fix: Count-Based Tracking with Signaling

Instead of storing task references, we track the *count* of active tasks and use a `TaskCompletionSource` to signal when all work is done:

```csharp
// ✅ FIXED - No memory leak
private readonly TaskCompletionSource _drainTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
private bool _channelIterationComplete;
private int _activeTaskCount;

private async Task ProcessAsync()
{
    try
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            await _concurrency.WaitAsync(_cts.Token);

            var op = new EphemeralOperation();
            EnqueueOperation(op);
            Interlocked.Decrement(ref _pendingCount);
            Interlocked.Increment(ref _activeTaskCount);

            // Fire-and-forget - we track completion via _activeTaskCount
            _ = ExecuteItemAsync(item, op);
        }

        // Mark iteration complete so task completions can signal drain
        Volatile.Write(ref _channelIterationComplete, true);

        // If all tasks already finished, signal now
        if (Volatile.Read(ref _activeTaskCount) == 0)
        {
            _drainTcs.TrySetResult();
        }

        await _drainTcs.Task;
    }
    catch (OperationCanceledException)
    {
        _drainTcs.TrySetCanceled();
    }
}
```

The key insight: we don't need the task *objects* - we just need to know when they're all done.

### Signaling Completion Correctly

The `ExecuteItemAsync` method signals the drain when it's the last task to finish:

```csharp
private async Task ExecuteItemAsync(T item, EphemeralOperation op)
{
    try
    {
        await _body(item, _cts.Token);
        Interlocked.Increment(ref _totalCompleted);
    }
    catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
    {
        op.Error = ex;
        Interlocked.Increment(ref _totalFailed);
    }
    finally
    {
        op.Completed = DateTimeOffset.UtcNow;
        _concurrency.Release();
        CleanupWindow();
        SampleIfRequested();

        // Signal drain when: last task completes AND channel iteration is done
        // Must be AFTER cleanup so all work is truly finished before DrainAsync returns
        if (Interlocked.Decrement(ref _activeTaskCount) == 0 &&
            Volatile.Read(ref _channelIterationComplete))
        {
            _drainTcs.TrySetResult();
        }
    }
}
```

The ordering matters:
1. **Cleanup first** (`CleanupWindow`, `SampleIfRequested`)
2. **Then check if we should signal**

This ensures that when `DrainAsync()` returns, all operations have truly completed their cleanup - not just their main work.

### Why `_channelIterationComplete`?

Without this flag, there's a race condition:

1. Task A completes, decrements `_activeTaskCount` to 0
2. Task A signals `_drainTcs.TrySetResult()`
3. Meanwhile, the `await foreach` loop is still running and starts Task B
4. But the drain has already been signaled!

The flag ensures we only signal when *both* conditions are true:
- The channel iteration has finished (no more tasks will be started)
- All active tasks have completed

### Faster IDs with XxHash64 (Cross-Process Safe)

`Guid.NewGuid()` is relatively expensive. For operation IDs that only need to be unique within a process, XxHash64 is much faster. But there's a catch: if you restart your process, you might get the same IDs again (same counter + same process start time could collide with a different process that started at a similar tick count).

The fix: include the process ID in the hash input:

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
        // Include process ID for cross-process uniqueness
        Span<byte> buffer = stackalloc byte[24];
        BitConverter.TryWriteBytes(buffer, _processStart);
        BitConverter.TryWriteBytes(buffer.Slice(8), _processId);
        BitConverter.TryWriteBytes(buffer.Slice(16), counter);

        return unchecked((long)XxHash64.HashToUInt64(buffer));
    }
}
```

This is:
- **Allocation-free** (uses `stackalloc`)
- **Thread-safe** (uses `Interlocked.Increment`)
- **Unique across processes** (combines process start time + process ID + counter)
- **Non-sequential** (the hash diffuses the monotonic counter)

### Memory Model Correctness with Volatile

For flag fields that are read and written from multiple threads, use `Volatile.Read` and `Volatile.Write` for proper memory barriers:

```csharp
// Reading
public bool IsCompleted => Volatile.Read(ref _completed);
public int ActiveCount => Volatile.Read(ref _activeTaskCount);

// Writing
Volatile.Write(ref _completed, true);
Volatile.Write(ref _channelIterationComplete, true);
```

On x86/x64, this is technically unnecessary due to the strong memory model, but it makes the intent explicit and is correct on all architectures.

### Per-Key Lock Memory Leak in Keyed Coordinator

The keyed coordinator creates a `SemaphoreSlim` for each unique key. In the original implementation, these were never cleaned up - a memory leak if you process millions of unique keys:

```csharp
// ❌ ORIGINAL - Unbounded growth
private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _perKeyLocks = new();
```

The fix: wrap locks in a tracker with last-used timestamps, and periodically clean up idle locks:

```csharp
// ✅ FIXED - Idle locks are cleaned up
private sealed class KeyLock(SemaphoreSlim gate, int maxCount)
{
    public SemaphoreSlim Gate { get; } = gate;
    public int MaxCount { get; } = maxCount;
    public long LastUsedTicks = Environment.TickCount64;

    public void Touch() => Volatile.Write(ref LastUsedTicks, Environment.TickCount64);
    public long GetLastUsed() => Volatile.Read(ref LastUsedTicks);
}

private readonly ConcurrentDictionary<TKey, KeyLock> _perKeyLocks = new();
private const long KeyLockIdleTimeoutMs = 60_000; // 1 minute
```

The cleanup runs periodically (roughly 1 in 1024 operations to amortise the O(n) scan):

```csharp
private void CleanupIdleKeyLocks(TKey currentKey, KeyLock currentKeyLock)
{
    var now = Environment.TickCount64;

    // Only trigger cleanup ~1 in 1024 calls
    if ((now & 0x3FF) != 0) return;

    foreach (var kvp in _perKeyLocks)
    {
        // Skip current key - it's in use
        if (EqualityComparer<TKey>.Default.Equals(kvp.Key, currentKey))
            continue;

        var keyLock = kvp.Value;
        var idleTime = now - keyLock.GetLastUsed();

        // Only remove if idle long enough AND semaphore is at full capacity
        if (idleTime >= KeyLockIdleTimeoutMs &&
            keyLock.Gate.CurrentCount == keyLock.MaxCount)
        {
            if (_perKeyLocks.TryRemove(kvp))
            {
                _perKeyPendingCount.TryRemove(kvp.Key, out _);
                keyLock.Gate.Dispose();
            }
        }
    }
}
```

### Exception Handling in Source Consumption

When consuming from an `IAsyncEnumerable` source, exceptions from the source itself need to be captured and propagated properly:

```csharp
private async Task ConsumeSourceAsync(IAsyncEnumerable<T> source)
{
    Exception? sourceException = null;
    try
    {
        await foreach (var item in source.WithCancellation(_cts.Token))
        {
            await _channel.Writer.WriteAsync(item, _cts.Token);
            Interlocked.Increment(ref _pendingCount);
            Interlocked.Increment(ref _totalEnqueued);
        }
    }
    catch (OperationCanceledException)
    {
        // Expected on cancellation
    }
    catch (Exception ex)
    {
        // Capture source exception to propagate to channel
        sourceException = ex;
    }
    finally
    {
        Volatile.Write(ref _completed, true);
        // Complete channel with exception if source failed
        _channel.Writer.TryComplete(sourceException);
    }
}
```

Now `DrainAsync()` will throw if the source threw - callers can observe the failure.

### Semaphore Disposal in Static Methods

The static `EphemeralForEachAsync` methods create per-key semaphores. If enumeration throws, they need to still be disposed:

```csharp
public static async Task EphemeralForEachAsync<T, TKey>(...)
{
    var perKeyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();

    try
    {
        foreach (var item in source)
        {
            // ... processing
        }
        await Task.WhenAll(running);
    }
    finally
    {
        // Always dispose, even on exception
        foreach (var gate in perKeyLocks.Values)
        {
            gate.Dispose();
        }
    }
}
```

### The Complete Fix Summary

| Issue | Original | Fixed |
|-------|----------|-------|
| Task tracking | `ConcurrentBag<Task>` grows forever | `int _activeTaskCount` stays bounded |
| Drain signaling | Poll in a loop | `TaskCompletionSource` signals exactly once |
| ID generation | `Guid.NewGuid()` | XxHash64 with stackalloc + process ID |
| ID collisions | Same IDs across processes possible | Process ID included in hash |
| Memory barriers | Implicit (risky) | Explicit `Volatile.Read/Write` |
| Race condition | Signal drain before iteration done | `_channelIterationComplete` flag |
| Per-key locks | Never cleaned up (memory leak) | Idle timeout + periodic cleanup |
| Source exceptions | Swallowed silently | Propagated via channel completion |
| Semaphore disposal | Missed on exception | `try/finally` ensures cleanup |

These changes make the library safe for long-lived singletons processing millions of operations with millions of unique keys.

---

## Conclusion

We've built a complete ephemeral execution library with:

1. **`EphemeralForEachAsync`** - One-shot parallel processing with tracking
2. **Keyed pipelines** - Per-entity sequential execution
3. **`EphemeralWorkCoordinator`** - Long-lived observable queues
4. **Factory pattern** - Named configurations like `IHttpClientFactory`
5. **Full DI integration** - Singleton, scoped, and factory-based lifetimes

The pattern sits in a sweet spot:
- More observable than `Parallel.ForEachAsync`
- Simpler than TPL Dataflow
- More integrated than raw Channels
- Privacy-safe by design

**Fire... and Don't Quite Forget.**

---

## Links

- [Part 1: Fire and Don't *Quite* Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution) - the theory and pattern
- [Learning LRUs - When Exceeding Capacity Makes Your System Better](/blog/learning-lrus-when-capacity-makes-systems-better) - the companion article on bounded memory
- [SemaphoreSlim documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) - the synchronisation primitive we use
- [System.Threading.Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) - the producer-consumer primitive underneath
- [IHttpClientFactory pattern](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests) - the inspiration for our factory pattern

