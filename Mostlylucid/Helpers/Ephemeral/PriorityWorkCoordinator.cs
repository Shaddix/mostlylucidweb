using System.Collections.Concurrent;

namespace Mostlylucid.Helpers.Ephemeral;

/// <summary>
/// Defines a priority lane for a priority-enabled coordinator.
/// </summary>
public sealed record PriorityLane(
    string Name,
    int? MaxDepth = null,
    IReadOnlySet<string>? CancelOnSignals = null,
    IReadOnlySet<string>? DeferOnSignals = null,
    TimeSpan? DeferCheckInterval = null,
    int? MaxDeferAttempts = null)
{
    public string Name { get; init; } = Name ?? throw new ArgumentNullException(nameof(Name));
    public int? MaxDepth { get; init; } = MaxDepth is > 0 ? MaxDepth : null;
    public IReadOnlySet<string>? CancelOnSignals { get; init; } = CancelOnSignals;
    public IReadOnlySet<string>? DeferOnSignals { get; init; } = DeferOnSignals;
    public TimeSpan DeferCheckInterval { get; init; } = DeferCheckInterval ?? TimeSpan.FromMilliseconds(100);
    public int MaxDeferAttempts { get; init; } = MaxDeferAttempts is > 0 ? MaxDeferAttempts.Value : 50;
}

/// <summary>
/// Options for configuring a priority-enabled unkeyed coordinator.
/// </summary>
public sealed record PriorityWorkCoordinatorOptions<T>(
    Func<T, CancellationToken, Task> Body,
    IReadOnlyCollection<PriorityLane>? Lanes = null,
    EphemeralOptions? EphemeralOptions = null);

/// <summary>
/// Options for configuring a priority-enabled keyed coordinator.
/// </summary>
public sealed record PriorityKeyedWorkCoordinatorOptions<T, TKey>(
    Func<T, TKey> KeySelector,
    Func<T, CancellationToken, Task> Body,
    IReadOnlyCollection<PriorityLane>? Lanes = null,
    EphemeralOptions? EphemeralOptions = null) where TKey : notnull;

/// <summary>
/// Priority wrapper over EphemeralWorkCoordinator. Higher lanes are always drained before lower lanes.
/// </summary>
public sealed class PriorityWorkCoordinator<T> : IAsyncDisposable
{
    private sealed class Lane
    {
        public PriorityLane Definition { get; }
        public ConcurrentQueue<T> Queue { get; } = new();
        public int Count;
        public int DeferAttempts;

        public Lane(PriorityLane definition) => Definition = definition;
    }

    private readonly EphemeralWorkCoordinator<T> _coordinator;
    private readonly List<Lane> _lanes;
    private readonly Dictionary<string, Lane> _laneLookup;
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private volatile bool _completed;
    private readonly SignalSink? _signalSource;
    private readonly bool _laneHasSignalRules;
    private readonly TimeSpan _minDeferInterval;

    public PriorityWorkCoordinator(PriorityWorkCoordinatorOptions<T> options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _lanes = BuildLanes(options.Lanes);
        _laneLookup = _lanes.ToDictionary(l => l.Definition.Name, StringComparer.Ordinal);
        _signalSource = options.EphemeralOptions?.Signals;
        _laneHasSignalRules = _lanes.Any(l => l.Definition.CancelOnSignals is { Count: > 0 } || l.Definition.DeferOnSignals is { Count: > 0 });
        if (_laneHasSignalRules && _signalSource is null)
            throw new InvalidOperationException("Lane-level signal gating requires EphemeralOptions.Signals to be set.");
        _minDeferInterval = _lanes.Select(l => l.Definition.DeferCheckInterval).DefaultIfEmpty(TimeSpan.FromMilliseconds(100)).Min();

        _coordinator = new EphemeralWorkCoordinator<T>(
            options.Body,
            options.EphemeralOptions ?? new EphemeralOptions());

        _pump = Task.Run(PumpAsync);
    }

    /// <summary>
    /// Enqueue an item into the specified lane (default "normal"). Returns false if the lane is full.
    /// </summary>
    public ValueTask<bool> EnqueueAsync(T item, string laneName = "normal", CancellationToken ct = default)
    {
        ThrowIfCompleted();
        if (!_laneLookup.TryGetValue(laneName, out var lane))
            throw new ArgumentException($"Lane '{laneName}' is not configured.", nameof(laneName));

        var count = Interlocked.Increment(ref lane.Count);
        if (lane.Definition.MaxDepth is not null && count > lane.Definition.MaxDepth)
        {
            Interlocked.Decrement(ref lane.Count);
            return ValueTask.FromResult(false);
        }

        lane.Queue.Enqueue(item);
        _signal.Release();
        return ValueTask.FromResult(true);
    }

    public async Task DrainAsync(CancellationToken ct = default)
    {
        _completed = true;
        _signal.Release();
        await _pump.ConfigureAwait(false);
        _coordinator.Complete();
        await _coordinator.DrainAsync(ct).ConfigureAwait(false);
    }

    public (string Lane, int Count)[] PendingCounts =>
        _lanes.Select(l => (l.Definition.Name, Volatile.Read(ref l.Count))).ToArray();

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
                var snapshot = _laneHasSignalRules ? _signalSource?.Sense() : null;
                var deferredSeen = false;

                while (true)
                {
                    var dequeued = false;

                    foreach (var lane in _lanes)
                    {
                        if (lane.Definition.CancelOnSignals is { Count: > 0 } &&
                            HasSignal(lane.Definition.CancelOnSignals, snapshot))
                        {
                            while (lane.Queue.TryDequeue(out _))
                            {
                                Interlocked.Decrement(ref lane.Count);
                            }
                            lane.DeferAttempts = 0;
                            continue;
                        }

                        if (lane.Definition.DeferOnSignals is { Count: > 0 } &&
                            HasSignal(lane.Definition.DeferOnSignals, snapshot))
                        {
                            lane.DeferAttempts++;
                            if (lane.DeferAttempts < lane.Definition.MaxDeferAttempts)
                            {
                                deferredSeen = true;
                                continue;
                            }
                            lane.DeferAttempts = 0; // fall through and run after max attempts
                        }

                        while (lane.Queue.TryDequeue(out var item))
                        {
                            Interlocked.Decrement(ref lane.Count);
                            await _coordinator.EnqueueAsync(item, _cts.Token).ConfigureAwait(false);
                            dequeued = true;
                        }

                        if (lane.Queue.IsEmpty is false)
                        {
                            // Lane still has items; check again before touching lower lanes
                            dequeued = true;
                            break;
                        }
                    }

                    if (!dequeued)
                        break;
                }

                if (_completed && _lanes.All(l => l.Queue.IsEmpty))
                    break;

                if (deferredSeen)
                {
                    try { await Task.Delay(_minDeferInterval, _cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private static List<Lane> BuildLanes(IReadOnlyCollection<PriorityLane>? lanes)
    {
        if (lanes is null || lanes.Count == 0)
            return [new Lane(new PriorityLane("normal"))];

        var result = new List<Lane>(lanes.Count);
        foreach (var lane in lanes)
        {
            if (string.IsNullOrWhiteSpace(lane.Name))
                throw new ArgumentException("Lane name cannot be empty.");
            result.Add(new Lane(lane));
        }
        return result;
    }

    private static bool HasSignal(IReadOnlySet<string>? patterns, IReadOnlyList<SignalEvent>? snapshot)
    {
        if (patterns is not { Count: > 0 } || snapshot is null)
            return false;

        foreach (var s in snapshot)
        {
            if (StringPatternMatcher.MatchesAny(s.Signal, patterns))
                return true;
        }
        return false;
    }

    private void ThrowIfCompleted()
    {
        if (_completed) throw new InvalidOperationException("Coordinator has been completed.");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _signal.Release();
        try { await _pump.ConfigureAwait(false); } catch { /* ignore */ }
        _signal.Dispose();
        _cts.Dispose();
        await _coordinator.DisposeAsync();
    }
}

/// <summary>
/// Priority wrapper over EphemeralKeyedWorkCoordinator. Higher lanes drain first; per-key ordering still applies inside the coordinator.
/// </summary>
public sealed class PriorityKeyedWorkCoordinator<T, TKey> : IAsyncDisposable where TKey : notnull
{
    private sealed class Lane
    {
        public PriorityLane Definition { get; }
        public ConcurrentQueue<T> Queue { get; } = new();
        public int Count;

        public Lane(PriorityLane definition) => Definition = definition;
    }

    private readonly EphemeralKeyedWorkCoordinator<T, TKey> _coordinator;
    private readonly Func<T, TKey> _keySelector;
    private readonly List<Lane> _lanes;
    private readonly Dictionary<string, Lane> _laneLookup;
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private volatile bool _completed;

    public PriorityKeyedWorkCoordinator(PriorityKeyedWorkCoordinatorOptions<T, TKey> options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _keySelector = options.KeySelector ?? throw new ArgumentNullException(nameof(options.KeySelector));
        _lanes = BuildLanes(options.Lanes);
        _laneLookup = _lanes.ToDictionary(l => l.Definition.Name, StringComparer.Ordinal);

        _coordinator = new EphemeralKeyedWorkCoordinator<T, TKey>(
            _keySelector,
            options.Body,
            options.EphemeralOptions ?? new EphemeralOptions());

        _pump = Task.Run(PumpAsync);
    }

    public ValueTask<bool> EnqueueAsync(T item, string laneName = "normal", CancellationToken ct = default)
    {
        ThrowIfCompleted();
        if (!_laneLookup.TryGetValue(laneName, out var lane))
            throw new ArgumentException($"Lane '{laneName}' is not configured.", nameof(laneName));

        var count = Interlocked.Increment(ref lane.Count);
        if (lane.Definition.MaxDepth is not null && count > lane.Definition.MaxDepth)
        {
            Interlocked.Decrement(ref lane.Count);
            return ValueTask.FromResult(false);
        }

        lane.Queue.Enqueue(item);
        _signal.Release();
        return ValueTask.FromResult(true);
    }

    public async Task DrainAsync(CancellationToken ct = default)
    {
        _completed = true;
        _signal.Release();
        await _pump.ConfigureAwait(false);
        _coordinator.Complete();
        await _coordinator.DrainAsync(ct).ConfigureAwait(false);
    }

    public (string Lane, int Count)[] PendingCounts =>
        _lanes.Select(l => (l.Definition.Name, Volatile.Read(ref l.Count))).ToArray();

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);

                while (true)
                {
                    var dequeued = false;

                    foreach (var lane in _lanes)
                    {
                        while (lane.Queue.TryDequeue(out var item))
                        {
                            Interlocked.Decrement(ref lane.Count);
                            await _coordinator.EnqueueAsync(item, _cts.Token).ConfigureAwait(false);
                            dequeued = true;
                        }

                        if (lane.Queue.IsEmpty is false)
                        {
                            dequeued = true;
                            break;
                        }
                    }

                    if (!dequeued)
                        break;
                }

                if (_completed && _lanes.All(l => l.Queue.IsEmpty))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private static List<Lane> BuildLanes(IReadOnlyCollection<PriorityLane>? lanes)
    {
        if (lanes is null || lanes.Count == 0)
            return [new Lane(new PriorityLane("normal"))];

        var result = new List<Lane>(lanes.Count);
        foreach (var lane in lanes)
        {
            if (string.IsNullOrWhiteSpace(lane.Name))
                throw new ArgumentException("Lane name cannot be empty.");
            result.Add(new Lane(lane));
        }
        return result;
    }

    private void ThrowIfCompleted()
    {
        if (_completed) throw new InvalidOperationException("Coordinator has been completed.");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _signal.Release();
        try { await _pump.ConfigureAwait(false); } catch { /* ignore */ }
        _signal.Dispose();
        _cts.Dispose();
        await _coordinator.DisposeAsync();
    }
}
