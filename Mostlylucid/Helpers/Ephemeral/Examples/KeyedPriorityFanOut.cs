using System.Collections.Concurrent;

namespace Mostlylucid.Helpers.Ephemeral.Examples;

/// <summary>
/// Keyed fan-out with a small priority lane: priority items execute first, then normal items resume.
/// Per-key ordering is preserved within each lane; priority is always drained before normal work.
/// </summary>
public sealed class KeyedPriorityFanOut<TKey, T> : IAsyncDisposable where TKey : notnull
{
    private readonly EphemeralKeyedWorkCoordinator<T, TKey> _coordinator;
    private readonly ConcurrentQueue<T> _priorityQueue = new();
    private readonly ConcurrentQueue<T> _normalQueue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;
    private readonly Func<T, TKey> _keySelector;
    private readonly Func<T, CancellationToken, Task> _body;
    private readonly int? _maxPriorityDepth;
    private volatile bool _completed;
    private int _priorityCount;
    private int _normalCount;

    public KeyedPriorityFanOut(
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        int maxConcurrency,
        int perKeyConcurrency = 1,
        SignalSink? sink = null,
        int? maxPriorityDepth = null)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _maxPriorityDepth = maxPriorityDepth is > 0 ? maxPriorityDepth : null;

        _coordinator = new EphemeralKeyedWorkCoordinator<T, TKey>(
            _keySelector,
            _body,
            new EphemeralOptions
            {
                MaxConcurrency = maxConcurrency,
                MaxConcurrencyPerKey = Math.Max(1, perKeyConcurrency),
                Signals = sink,
                EnableFairScheduling = false
            });

        _pump = Task.Run(PumpAsync);
    }

    /// <summary>
    /// Enqueue a normal-priority item.
    /// </summary>
    public ValueTask EnqueueAsync(T item, CancellationToken ct = default)
    {
        ThrowIfCompleted();
        Interlocked.Increment(ref _normalCount);
        _normalQueue.Enqueue(item);
        _signal.Release();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Enqueue a priority item; priority items are always drained before normal items.
    /// </summary>
    public ValueTask<bool> EnqueuePriorityAsync(T item, CancellationToken ct = default)
    {
        ThrowIfCompleted();

        var count = Interlocked.Increment(ref _priorityCount);
        if (_maxPriorityDepth is not null && count > _maxPriorityDepth.Value)
        {
            Interlocked.Decrement(ref _priorityCount);
            return ValueTask.FromResult(false);
        }

        _priorityQueue.Enqueue(item);
        _signal.Release();
        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Drain all work. Priority items will complete before normal items resume.
    /// </summary>
    public async Task DrainAsync(CancellationToken ct = default)
    {
        _completed = true;
        _signal.Release(); // wake pump
        await _pump.ConfigureAwait(false);
        _coordinator.Complete();
        await _coordinator.DrainAsync(ct).ConfigureAwait(false);
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);

                // Drain as many items as are currently queued, giving strict priority to the priority queue.
                while (_priorityQueue.TryDequeue(out var pItem))
                {
                    Interlocked.Decrement(ref _priorityCount);
                    await _coordinator.EnqueueAsync(pItem, _cts.Token).ConfigureAwait(false);
                }

                while (_priorityQueue.IsEmpty && _normalQueue.TryDequeue(out var nItem))
                {
                    Interlocked.Decrement(ref _normalCount);
                    await _coordinator.EnqueueAsync(nItem, _cts.Token).ConfigureAwait(false);
                }

                if (_completed && _priorityQueue.IsEmpty && _normalQueue.IsEmpty)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _signal.Release();
        try { await _pump.ConfigureAwait(false); } catch { /* ignore */ }
        _cts.Dispose();
        _signal.Dispose();
        await _coordinator.DisposeAsync();
    }

    /// <summary>
    /// Current queued counts (priority lane and normal lane). Does not include work already in-flight inside the coordinator.
    /// </summary>
    public (int Priority, int Normal) PendingCounts =>
        (Volatile.Read(ref _priorityCount), Volatile.Read(ref _normalCount));

    private void ThrowIfCompleted()
    {
        if (_completed) throw new InvalidOperationException("Coordinator has been completed.");
    }
}
