using System.Collections.Concurrent;

namespace Mostlylucid.Helpers.Ephemeral;

/// <summary>
/// Async dispatcher that fans signals into Ephemeral coordinators using pattern-based routing.
/// Emits stay synchronous; dispatch happens via internal coordinators.
/// </summary>
public sealed class SignalDispatcher : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, DispatchPolicy> _policies = new();
    private readonly EphemeralKeyedWorkCoordinator<SignalEvent, string> _coordinator;
    private readonly CancellationTokenSource _cts = new();

    private record DispatchPolicy(Func<SignalEvent, Task> Handler);

    public SignalDispatcher(EphemeralOptions? coordinatorOptions = null)
    {
        // Keyed by signal name; per-signal sequential unless overridden via options.MaxConcurrencyPerKey
        _coordinator = new EphemeralKeyedWorkCoordinator<SignalEvent, string>(
            evt => evt.Signal,
            async (evt, ct) =>
            {
                foreach (var (pattern, policy) in _policies)
                {
                    if (StringPatternMatcher.Matches(evt.Signal, pattern))
                    {
                        await policy.Handler(evt);
                        return;
                    }
                }
            },
            coordinatorOptions ?? new EphemeralOptions { MaxConcurrency = Environment.ProcessorCount, MaxConcurrencyPerKey = 1, MaxTrackedOperations = 128, EnableDynamicConcurrency = false });
    }

    /// <summary>
    /// Register or replace a handler for a signal pattern (supports '*' and '?').
    /// </summary>
    public void Register(string pattern, Func<SignalEvent, Task> handler)
    {
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentNullException(nameof(pattern));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        _policies[pattern] = new DispatchPolicy(handler);
    }

    /// <summary>
    /// Enqueue a signal for async dispatch. Returns false if dispatcher is cancelled.
    /// </summary>
    public bool Dispatch(SignalEvent evt)
    {
        if (_cts.IsCancellationRequested) return false;
        _ = _coordinator.EnqueueAsync(evt, _cts.Token);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await _coordinator.DisposeAsync();
        _cts.Dispose();
    }
}
