# Ephemeral Signals - Turning Atoms into a Sensing Network

<!--category-- ASP.NET, Architecture, Systems Design, Async -->
<datetime class="hidden">2025-12-12T16:00</datetime>

In **[Part 1](/blog/fire-and-dont-quite-forget-ephemeral-execution)** we built ephemeral execution - bounded, private, self-cleaning async workflows. In **[Part 2](/blog/ephemeral-execution-library)** we turned that into a reusable library with coordinators, keyed pipelines, and DI integration.

This article adds one small feature that changes everything: **signals**.

## Source Files

The signal infrastructure lives in:

| File | Purpose |
|------|---------|
| [Signals.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Signals.cs) | `SignalEvent`, `SignalRetractedEvent`, `SignalPropagation`, `SignalConstraints`, `SignalSink`, `ISignalEmitter`, `AsyncSignalProcessor` |
| [EphemeralOperation.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOperation.cs) | Signal emission and retraction from operations |
| [EphemeralOptions.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/EphemeralOptions.cs) | Signal-reactive configuration (`CancelOnSignals`, `DeferOnSignals`, `OnSignal`, `OnSignalRetracted`) |
| [StringPatternMatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/StringPatternMatcher.cs) | Glob-style pattern matching for signal filtering |
| [SignalDispatcher.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/SignalDispatcher.cs) | Async signal routing with pattern matching (supports `*`, `?`, comma lists, deterministic order) |
| [Examples/SignalingHttpClient.cs](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Examples/SignalingHttpClient.cs) | Sample fine-grained signal emission for HTTP calls |

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

## Raising and Retracting Signals

Operations implement `ISignalEmitter`:

```csharp
public interface ISignalEmitter
{
    // Emit signals
    void Emit(string signal);
    bool EmitCaused(string signal, SignalPropagation? cause);

    // Retract (remove) signals
    bool Retract(string signal);
    int RetractMatching(string pattern);
    bool HasSignal(string signal);

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
Pattern filters use glob semantics (`*`, `?`) and support comma lists (`"error.*,timeout"`). Matching is deterministic and allocation-light via `StringPatternMatcher`.

### Fine-Grained Signal Example: HTTP Calls

For very detailed observability, you can emit signals at each stage of an operation. The library includes a sample [SignalingHttpClient](https://github.com/scottgal/mostlylucidweb/blob/main/Mostlylucid/Helpers/Ephemeral/Examples/SignalingHttpClient.cs) that demonstrates this pattern:

```csharp
using Mostlylucid.Helpers.Ephemeral.Examples;

// Inside your work body where you have access to the operation's emitter:
await coordinator.ProcessAsync(async (request, op, ct) =>
{
    var data = await SignalingHttpClient.DownloadWithSignalsAsync(
        httpClient,
        new HttpRequestMessage(HttpMethod.Get, request.Url),
        op,  // ISignalEmitter
        ct);

    // Process the downloaded data...
});
```

This emits signals at each stage:

| Signal | When |
|--------|------|
| `stage.starting` | Before the request begins |
| `progress:0` | Initial progress mark |
| `stage.request` | HTTP request sent |
| `stage.headers` | Response headers received |
| `stage.reading` | Starting to read body |
| `progress:XX` | Progress percentage (0-100) during download |
| `stage.completed` | Download finished |

You can then query these with pattern matching:

```csharp
// Find all stage transitions
var stages = coordinator.GetSignalsByPattern("stage.*");

// Check download progress
var progress = coordinator.GetSignalsByPattern("progress:*");

// Check if any download is still in progress
if (coordinator.HasSignalMatching("stage.reading") &&
    !coordinator.HasSignalMatching("stage.completed"))
{
    // Download in progress
}
```

### Retracting Signals

Operations can also remove their own signals. This is useful for temporary states:

```csharp
await coordinator.ProcessAsync(async (item, op, ct) =>
{
    // Mark as processing
    op.Emit("processing");

    try
    {
        await ProcessItemAsync(item, ct);

        // Success - retract the processing signal
        op.Retract("processing");
        op.Emit("completed");
    }
    catch (RetryableException)
    {
        // Keep processing signal, add retry info
        op.Emit("retrying");
    }
    catch (Exception)
    {
        // Remove all temporary signals
        op.RetractMatching("processing*");
        op.Emit("failed");
        throw;
    }
});
```

### Retraction Events

Just like signal emission, retractions can trigger callbacks:

```csharp
var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        // Sync retraction handler
        OnSignalRetracted = evt =>
        {
            _metrics.DecrementGauge(evt.Signal);
            Console.WriteLine($"Signal {evt.Signal} retracted from op {evt.OperationId}");

            if (evt.WasPatternMatch)
                Console.WriteLine($"  (matched pattern: {evt.Pattern})");
        },

        // Async retraction handler
        OnSignalRetractedAsync = async (evt, ct) =>
        {
            await _telemetry.TrackRetraction(evt.Signal, evt.OperationId, ct);
        }
    });
```

The `SignalRetractedEvent` includes:
- `Signal` - The retracted signal name
- `OperationId` - The operation that retracted it
- `Key` - The operation's key (if any)
- `Timestamp` - When retraction occurred
- `WasPatternMatch` - True if retracted via `RetractMatching`
- `Pattern` - The pattern used (if pattern match)

### Real-World Example: Rate Limit Recovery

```csharp
await coordinator.ProcessAsync(async (request, op, ct) =>
{
    // Check if we already have a rate limit signal
    if (op.HasSignal("rate-limited"))
    {
        // We're in recovery mode
        await Task.Delay(1000, ct);
    }

    try
    {
        var response = await _api.SendAsync(request, ct);

        // Success! Remove any rate limit signal
        if (op.Retract("rate-limited"))
        {
            op.Emit("rate-limit-cleared");
        }
    }
    catch (RateLimitException ex)
    {
        op.Emit("rate-limited");
        op.Emit($"rate-limit:{ex.RetryAfterMs}ms");
        throw;
    }
});
```

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

Synchronous signal handlers (`OnSignal`) run on the operation's thread—keep them fast. To do I/O-bound work, fan signals into an async path with `SignalDispatcher` (pattern matching, deterministic order) or `AsyncSignalProcessor`.

### SignalDispatcher fan-out

```csharp
await using var dispatcher = new SignalDispatcher(new EphemeralOptions
{
    MaxConcurrency = Environment.ProcessorCount,
    MaxConcurrencyPerKey = 1  // sequential per signal name by default
});

dispatcher.Register("error.*", evt => _alerts.SendAsync(evt.Signal));
dispatcher.Register("progress:*", evt => _metrics.Record(evt.Signal));

// In coordinator options, keep OnSignal cheap and enqueue
var coordinator = new EphemeralWorkCoordinator<Request>(
    body,
    new EphemeralOptions
    {
        OnSignal = dispatcher.Dispatch
    });
```

Patterns support `*`, `?`, and comma lists (`"error.*,timeout"`). All matching handlers run in registration order on a background keyed coordinator; emit remains synchronous.

### AsyncSignalProcessor

For standalone async processing:

```csharp
await using var processor = new AsyncSignalProcessor(
    async (signal, ct) =>
    {
        await _externalService.LogAsync(signal, ct);
    },
    maxConcurrency: 4,
    maxQueueSize: 1000);

// Enqueue signals (returns immediately)
processor.Enqueue(new SignalEvent(
    "rate-limit",
    operationId,
    key,
    DateTimeOffset.UtcNow));
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
