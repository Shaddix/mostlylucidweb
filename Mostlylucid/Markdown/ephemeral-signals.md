# Ephemeral Signals - Turning Atoms into a Sensing Network

<!--category-- ASP.NET, Architecture, Systems Design, Async -->
<datetime class="hidden">2025-12-12T16:00</datetime>

In **[Part 1](/blog/fire-and-dont-quite-forget-ephemeral-execution)** we built ephemeral execution - bounded, private, self-cleaning async workflows. In **[Part 2](/blog/ephemeral-execution-library)** we turned that into a reusable library with coordinators, keyed pipelines, and DI integration.

This article adds one small feature that changes everything: **signals**.

## Source Files

The signal infrastructure lives in:

| File | Purpose |
|------|---------|
| [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs) | `SignalEvent`, `SignalPropagation`, `SignalConstraints`, `SignalSink`, `ISignalEmitter`, `AsyncSignalProcessor` |
| [EphemeralOperation.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOperation.cs) | Signal emission from operations |
| [EphemeralOptions.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOptions.cs) | Signal-reactive configuration (`CancelOnSignals`, `DeferOnSignals`, `OnSignalAsync`) |
| [StringPatternMatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/StringPatternMatcher.cs) | Glob-style pattern matching for signal filtering |

[TOC]

---

## The Problem: Isolated Atoms

Our ephemeral coordinators are great at processing work, but they're isolated. Each coordinator knows about its own operations, but has no awareness of what's happening elsewhere in the system.

```csharp
// Translation coordinator has no idea that...
await translationCoordinator.EnqueueAsync(request);

// ...the API just hit a rate limit
// ...another service is experiencing backpressure
// ...a downstream dependency is slow
```

We could wire up explicit dependencies, but that creates coupling. What we want is **ambient awareness** - coordinators that can sense their environment without being directly connected.

---

## The Solution: Signals on Operations

From [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs):

```csharp
public readonly record struct SignalEvent(
    string Signal,
    long OperationId,
    string? Key,
    DateTimeOffset Timestamp,
    SignalPropagation? Propagation = null)
{
    public int Depth => Propagation?.Depth ?? 0;
    public bool WouldCycle(string signal) => Propagation?.Contains(signal) == true;
    public bool Is(string name) => Signal == name;
    public bool StartsWith(string prefix) => Signal.StartsWith(prefix, StringComparison.Ordinal);
}
```

An operation can raise signals during execution. Those signals live in the ephemeral window alongside the operation. When the operation ages out, the signals go with it.

That's it. No message broker. No separate infrastructure. Just strings attached to operations.

---

## How This Compares to Other Approaches

### Application Insights / OpenTelemetry

```csharp
using var activity = source.StartActivity("ProcessOrder");
activity?.SetTag("order.id", orderId);
activity?.SetTag("rate.limited", true);
```

**Best for**: Distributed tracing across services, long-term telemetry storage, correlation IDs.

**Use telemetry when**: You need to trace requests across multiple services, store metrics for analysis, or integrate with monitoring tools.

**Use Ephemeral signals when**: You need in-process ambient awareness, reactive coordination, or don't want telemetry infrastructure.

### Reactive Extensions (Rx)

```csharp
var rateLimits = Observable.FromEventPattern<RateLimitEventArgs>(
    h => api.RateLimitHit += h,
    h => api.RateLimitHit -= h);

rateLimits
    .Throttle(TimeSpan.FromSeconds(1))
    .Subscribe(e => HandleRateLimit(e));
```

**Best for**: Complex event processing, time-based operations, combining multiple event streams.

**Use Rx when**: You need complex temporal queries (windowing, debouncing, combining streams).

**Use Ephemeral signals when**: You want simpler polling-based sensing, automatic cleanup, or integration with operation tracking.

### MediatR Notifications

```csharp
public class RateLimitNotification : INotification
{
    public int RetryAfterMs { get; init; }
}

await _mediator.Publish(new RateLimitNotification { RetryAfterMs = 5000 });
```

**Best for**: Decoupled in-process event handling with multiple handlers.

**Use MediatR when**: You want multiple handlers to react to the same event synchronously.

**Use Ephemeral signals when**: You want ambient sensing without explicit subscription, self-cleaning history, or integration with bounded execution.

### Polly Circuit Breaker

```csharp
var circuitBreaker = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
```

**Best for**: Resilience around individual calls with automatic state management.

**Use Polly when**: You need per-call resilience with automatic half-open/closed transitions.

**Use Ephemeral signals when**: You want ambient awareness across many operations, custom circuit logic, or integration with operation tracking.

**Combine them**: Use Polly inside your work body, emit signals when circuits trip.

### Comparison Table

| Approach | Self-Cleaning | Ambient Sensing | Decoupled | Custom Logic | Integration |
|----------|:-------------:|:---------------:|:---------:|:------------:|:-----------:|
| OpenTelemetry | ❌ | ❌ | ✅ | ❌ | External tools |
| Reactive Extensions | ❌ | ✅ | ✅ | ✅ | Complex |
| MediatR | ❌ | ❌ | ✅ | ✅ | Manual |
| Polly Circuit Breaker | ✅ | ❌ | ❌ | ❌ | Per-call |
| **Ephemeral Signals** | ✅ | ✅ | ✅ | ✅ | Built-in |

---

## Raising Signals

Operations implement `ISignalEmitter`:

```csharp
public interface ISignalEmitter
{
    void Emit(string signal);
    bool EmitCaused(string signal, SignalPropagation? cause);
    long OperationId { get; }
    string? Key { get; }
}
```

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

Signals are just strings. Use simple names (`"rate-limit"`) or structured names (`"rate-limit:5000ms"`).

---

## Sensing Signals

All coordinators provide optimised signal querying:

```csharp
// Check if any recent operation hit a rate limit
if (coordinator.HasSignal("rate-limit"))
{
    await Task.Delay(1000);
}

// Count slow responses in the window
var slowCount = coordinator.CountSignals("slow-response");
if (slowCount > 10)
{
    await ThrottleAsync();
}

// Get signals by pattern
var httpErrors = coordinator.GetSignalsByPattern("http.error.*");

// Get signals since a time
var recentSignals = coordinator.GetSignalsSince(DateTimeOffset.UtcNow.AddMinutes(-1));

// Get signals for a specific key
var userSignals = coordinator.GetSignalsByKey("user-123");
```

---

## Signal-Reactive Processing

From [EphemeralOptions.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOptions.cs):

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

When a signal in `CancelOnSignals` is detected, new items are skipped (counted as failed).
When a signal in `DeferOnSignals` is detected, new items wait until the signal clears.

---

## Real-World Example: Adaptive Rate Limiting

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

    private async Task ProcessTranslationAsync(
        TranslationRequest request,
        CancellationToken ct)
    {
        // Work body has access to signaling via coordinator
        try
        {
            await _translationApi.TranslateAsync(request, ct);
        }
        catch (RateLimitException ex)
        {
            // Signal will be visible to other operations
            throw;
        }
    }
}
```

Every instance of this service automatically backs off when rate limits are hit. No shared state. No message passing. Just reading the ephemeral window.

---

## Real-World Example: Cross-Coordinator Awareness

Multiple coordinators can sense each other through a shared `SignalSink`:

```csharp
public class OrderProcessingSystem
{
    private readonly SignalSink _sharedSignals = new(maxCapacity: 1000);

    private readonly EphemeralWorkCoordinator<Order> _orderProcessor;
    private readonly EphemeralWorkCoordinator<PaymentRequest> _paymentProcessor;

    public OrderProcessingSystem()
    {
        var options = new EphemeralOptions { Signals = _sharedSignals };

        _orderProcessor = new EphemeralWorkCoordinator<Order>(
            ProcessOrderAsync, options);

        _paymentProcessor = new EphemeralWorkCoordinator<PaymentRequest>(
            ProcessPaymentAsync, options);
    }

    public async Task ProcessOrderAsync(Order order)
    {
        // Check shared signals for payment gateway issues
        if (_sharedSignals.Detect("gateway-error"))
        {
            await _retryQueue.EnqueueAsync(order);
            return;
        }

        await _orderProcessor.EnqueueAsync(order);
    }
}
```

---

## Real-World Example: Health Monitoring

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

No metrics library needed. Just query the ephemeral window.

---

## Real-World Example: Signal-Based Circuit Breaking

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

The circuit breaker has no state of its own - it just reads the ephemeral window.

---

## Signal Constraints: Preventing Infinite Loops

From [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs):

When signals can cause other signals, you risk infinite loops. `SignalConstraints` prevents this:

```csharp
var options = new EphemeralOptions
{
    SignalConstraints = new SignalConstraints
    {
        // Max propagation depth before blocking
        MaxDepth = 10,

        // Prevent A → B → A cycles
        BlockCycles = true,

        // Signals that end propagation chains
        TerminalSignals = new HashSet<string> { "completed", "failed", "resolved" },

        // Signals that emit but don't propagate
        LeafSignals = new HashSet<string> { "logged", "metric" },

        // Callback when a signal is blocked
        OnBlocked = (signal, reason) =>
        {
            _logger.LogWarning("Signal {Signal} blocked: {Reason}",
                signal.Signal, reason);
        }
    }
};
```

### Signal Propagation

Track causality with `EmitCaused`:

```csharp
public void HandleSignal(SignalEvent evt, ISignalEmitter emitter)
{
    if (evt.Is("order-placed"))
    {
        // This signal carries the propagation chain
        // Will be blocked if it would create a cycle
        emitter.EmitCaused("inventory-reserved", evt.Propagation);
    }
}
```

The propagation chain tracks the path: `order-placed → inventory-reserved → ...`

If `inventory-reserved` tried to emit `order-placed`, it would be blocked (cycle detected).

---

## The SignalSink: Global Signal Space

From [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs):

For signals that need to be visible across coordinators:

```csharp
public sealed class SignalSink
{
    private readonly ConcurrentQueue<SignalEvent> _window;
    private readonly int _maxCapacity;
    private readonly TimeSpan _maxAge;

    public SignalSink(int maxCapacity = 1000, TimeSpan? maxAge = null);

    // Raise signals
    public void Raise(SignalEvent signal);
    public void Raise(string signal, string? key = null);

    // Sense signals
    public IReadOnlyList<SignalEvent> Sense();
    public IReadOnlyList<SignalEvent> Sense(Func<SignalEvent, bool> predicate);
    public bool Detect(string signalName);
    public bool Detect(Func<SignalEvent, bool> predicate);

    public int Count { get; }
}
```

Usage:

```csharp
// Create a shared sink
var sink = new SignalSink(maxCapacity: 1000, maxAge: TimeSpan.FromMinutes(2));

// Configure coordinators to use it
var options = new EphemeralOptions { Signals = sink };

// Or raise signals directly
sink.Raise("system-maintenance");

// Sense from anywhere
if (sink.Detect("system-maintenance"))
{
    await DeferWorkAsync();
}
```

---

## Pattern Matching with StringPatternMatcher

From [StringPatternMatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/StringPatternMatcher.cs):

Glob-style matching for signal filtering:

```csharp
// Exact match
coordinator.HasSignal("rate-limit");

// Wildcard patterns
coordinator.HasSignalMatching("http.*");           // http.timeout, http.error
coordinator.HasSignalMatching("error.*.critical"); // error.payment.critical
coordinator.HasSignalMatching("user-???-failed");  // user-123-failed

// Comma-separated patterns in CancelOnSignals/DeferOnSignals
new EphemeralOptions
{
    CancelOnSignals = new HashSet<string>
    {
        "system-overload, circuit-open",  // Either pattern
        "error.*"                          // Any error signal
    }
}
```

---

## Signal Naming Conventions

Keep signals simple and consistent:

```csharp
// Good - simple, categorical
op.Signal("success");
op.Signal("failure");
op.Signal("rate-limit");
op.Signal("timeout");
op.Signal("cache-hit");

// Good - structured for parsing
op.Signal("rate-limit:5000ms");
op.Signal("retry:attempt-3");
op.Signal("slow:2500ms");
op.Signal("http.error:429");

// Good - hierarchical for pattern matching
op.Signal("payment.declined");
op.Signal("payment.approved");
op.Signal("payment.gateway-error");

// Avoid - entity identification belongs in Key, not signals
op.Signal("user-123-rate-limited");  // Bad

// Instead
op.Key = "user-123";
op.Signal("rate-limit");
```

---

## Async Signal Handling

Synchronous signal handlers (`OnSignal`) run on the operation's thread - they must be fast. For I/O-bound processing (logging to external services, sending notifications, database writes), use async handlers.

### OnSignalAsync

Configure async handling in options:

```csharp
var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        // Async handler - non-blocking, processed in background
        OnSignalAsync = async (signal, ct) =>
        {
            await _telemetry.TrackSignalAsync(signal.Signal, signal.Key, ct);

            if (signal.StartsWith("error"))
            {
                await _alertService.SendAlertAsync(signal, ct);
            }
        },

        // Control concurrency of async handlers (default: 4)
        MaxConcurrentSignalHandlers = 4,

        // Max queued signals before dropping (default: 1000)
        MaxQueuedSignals = 1000,

        // Sync handler still available for fast, in-memory operations
        OnSignal = signal =>
        {
            _metrics.IncrementCounter(signal.Signal);
        }
    });
```

The async signal processor:
- Signals are enqueued immediately (non-blocking emission)
- Processing happens in a bounded background queue
- Concurrency is limited to prevent resource exhaustion
- Oldest signals are dropped when queue is full
- Exceptions in handlers are swallowed (handlers should manage their own errors)

### AsyncSignalProcessor

For standalone async signal processing outside coordinators:

```csharp
await using var processor = new AsyncSignalProcessor(
    async (signal, ct) =>
    {
        await _externalService.LogAsync(signal, ct);
    },
    maxConcurrency: 4,
    maxQueueSize: 1000);

// Enqueue signals (returns immediately)
bool queued = processor.Enqueue(new SignalEvent(
    "rate-limit",
    operationId,
    key,
    DateTimeOffset.UtcNow));

if (!queued)
{
    // Queue was full - signal was dropped
    _metrics.IncrementDroppedSignals();
}

// Check stats
Console.WriteLine($"Queued: {processor.QueuedCount}");
Console.WriteLine($"Processed: {processor.ProcessedCount}");
Console.WriteLine($"Dropped: {processor.DroppedCount}");
```

### Real-World Example: External Telemetry

```csharp
public class TelemetrySignalHandler
{
    private readonly AsyncSignalProcessor _processor;
    private readonly ITelemetryClient _telemetry;

    public TelemetrySignalHandler(ITelemetryClient telemetry)
    {
        _telemetry = telemetry;
        _processor = new AsyncSignalProcessor(
            HandleSignalAsync,
            maxConcurrency: 8,
            maxQueueSize: 5000);
    }

    public void OnSignal(SignalEvent signal) => _processor.Enqueue(signal);

    private async Task HandleSignalAsync(SignalEvent signal, CancellationToken ct)
    {
        var properties = new Dictionary<string, string>
        {
            ["signal"] = signal.Signal,
            ["operationId"] = signal.OperationId.ToString(),
            ["key"] = signal.Key ?? "none",
            ["depth"] = signal.Depth.ToString()
        };

        await _telemetry.TrackEventAsync("EphemeralSignal", properties, ct);

        // Categorized tracking
        if (signal.StartsWith("error"))
            await _telemetry.TrackExceptionAsync(signal.Signal, properties, ct);
        else if (signal.StartsWith("perf"))
            await _telemetry.TrackMetricAsync(signal.Signal, 1, ct);
    }
}

// Usage
var handler = new TelemetrySignalHandler(telemetryClient);

var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        OnSignal = handler.OnSignal  // Fast sync dispatch to async queue
    });
```

---

## Why This Works

Signals are powerful because they're **ephemeral**:

| Property | Benefit |
|----------|---------|
| **Bounded** | Can't grow unbounded - old signals age out |
| **Self-cleaning** | No cleanup code needed |
| **Decoupled** | Emitters don't know about listeners |
| **Observable** | Any code can sense the current state |
| **Private** | No user data - just signal names |
| **Fast** | O(1) detection with short-circuiting |

The ephemeral window is already there for debugging. Signals just give it semantic meaning.

---

## Conclusion

Signals turn isolated execution atoms into a **sensing network**. Each coordinator can:

- **Emit** signals about what it experienced
- **Sense** signals from its own history or a shared sink
- **React** automatically via `CancelOnSignals` and `DeferOnSignals`

No message broker. No shared state. No coordination protocol. Just operations with metadata that naturally decays.

The atoms don't talk to each other directly - they just leave traces in the ephemeral window that others can observe. It's stigmergy for async systems.

**Fire... Signal... Sense... Forget.**

---

## Links

- [Part 1: Fire and Don't *Quite* Forget](/blog/fire-and-dont-quite-forget-ephemeral-execution) - the theory and pattern
- [Part 2: Building a Reusable Ephemeral Execution Library](/blog/ephemeral-execution-library) - the full implementation
- [Stigmergy (Wikipedia)](https://en.wikipedia.org/wiki/Stigmergy) - indirect coordination through environment modification
- [Circuit Breaker Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
- [Reactive Extensions](https://github.com/dotnet/reactive)

