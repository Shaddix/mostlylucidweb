using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mostlylucid.Helpers;

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

public sealed record EphemeralOperationSnapshot(
    Guid Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string? Key,
    bool IsFaulted,
    Exception? Error,
    TimeSpan? Duration);

/// <summary>
/// Snapshot of an operation that captures a result of type TResult.
/// </summary>
public sealed record EphemeralOperationSnapshot<TResult>(
    Guid Id,
    DateTimeOffset Started,
    DateTimeOffset? Completed,
    string? Key,
    bool IsFaulted,
    Exception? Error,
    TimeSpan? Duration,
    TResult? Result,
    bool HasResult);

internal sealed class EphemeralOperation
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Completed { get; set; }
    public Exception? Error { get; set; }
    public string? Key { get; init; }

    public TimeSpan? Duration =>
        Completed is { } done ? done - Started : null;

    public EphemeralOperationSnapshot ToSnapshot() =>
        new(Id, Started, Completed, Key, IsFaulted: Error != null, Error, Duration);
}

internal sealed class EphemeralOperation<TResult>
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Completed { get; set; }
    public Exception? Error { get; set; }
    public string? Key { get; init; }
    public TResult? Result { get; set; }
    public bool HasResult { get; set; }

    public TimeSpan? Duration =>
        Completed is { } done ? done - Started : null;

    public bool IsSuccess => Completed.HasValue && Error is null;

    public EphemeralOperationSnapshot<TResult> ToSnapshot() =>
        new(Id, Started, Completed, Key, IsFaulted: Error != null, Error, Duration, Result, HasResult);

    public EphemeralOperationSnapshot ToBaseSnapshot() =>
        new(Id, Started, Completed, Key, IsFaulted: Error != null, Error, Duration);
}

public static class ParallelEphemeral
{
    /// <summary>
    /// Ephemeral parallel foreach:
    /// - Bounded concurrency
    /// - Keeps a small rolling window of recent operations
    /// - No payloads stored, only metadata
    /// </summary>
    public static async Task EphemeralForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new EphemeralOptions();

        using var concurrency = new SemaphoreSlim(options.MaxConcurrency);
        var recent = new ConcurrentQueue<EphemeralOperation>();
        var running = new ConcurrentBag<Task>();

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);

            var op = new EphemeralOperation();
            EnqueueEphemeral(op, recent, options);

            var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, concurrency);
            running.Add(task);
        }

        // Wait for all in-flight work to complete
        await Task.WhenAll(running).ConfigureAwait(false);
    }

    /// <summary>
    /// Keyed version:
    /// - Overall concurrency bounded by MaxConcurrency
    /// - Per-key concurrency bounded by MaxConcurrencyPerKey (default 1 = sequential pipelines per key)
    /// </summary>
    public static async Task EphemeralForEachAsync<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
    {
        options ??= new EphemeralOptions();

        using var globalConcurrency = new SemaphoreSlim(options.MaxConcurrency);
        var perKeyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();
        var recent = new ConcurrentQueue<EphemeralOperation>();
        var running = new ConcurrentBag<Task>();

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = keySelector(item);
            var keyGate = perKeyLocks.GetOrAdd(
                key,
                _ => new SemaphoreSlim(options.MaxConcurrencyPerKey));

            await globalConcurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            await keyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            var op = new EphemeralOperation { Key = key?.ToString() };
            EnqueueEphemeral(op, recent, options);

            var task = ExecuteAsync(item, body, op, recent, options, cancellationToken, keyGate, globalConcurrency);
            running.Add(task);
        }

        await Task.WhenAll(running).ConfigureAwait(false);

        // Cleanup per-key gates
        foreach (var gate in perKeyLocks.Values)
        {
            gate.Dispose();
        }
    }

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
            op.Error = ex;
        }
        finally
        {
            op.Completed = DateTimeOffset.UtcNow;
            foreach (var semaphore in semaphores)
                semaphore.Release();
            CleanupWindow(recent, options);
            SampleIfRequested(recent, options);
        }
    }

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
        // Size-based eviction
        while (recent.Count > options.MaxTrackedOperations &&
               recent.TryDequeue(out _))
        {
        }

        // Age-based eviction (best-effort, don't overthink it)
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

    private static void SampleIfRequested(
        ConcurrentQueue<EphemeralOperation> recent,
        EphemeralOptions options)
    {
        var sampler = options.OnSample;
        if (sampler is null) return;

        // Cheap snapshot; caller decides what to do
        var snapshot = recent
            .Select(x => x.ToSnapshot())
            .ToArray();

        if (snapshot.Length > 0)
        {
            sampler(snapshot);
        }
    }
}

/// <summary>
/// A long-lived, observable work coordinator that accepts items continuously.
/// Unlike EphemeralForEachAsync (which processes a collection), this stays alive
/// and lets you enqueue items over time, inspect operations, and gracefully shutdown.
/// </summary>
public sealed class EphemeralWorkCoordinator<T> : IAsyncDisposable
{
    private readonly Channel<T> _channel;
    private readonly Func<T, CancellationToken, Task> _body;
    private readonly EphemeralOptions _options;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentQueue<EphemeralOperation> _recent;
    private readonly SemaphoreSlim _concurrency;
    private readonly Task _processingTask;
    private readonly ConcurrentBag<Task> _runningTasks;
    private readonly Task? _sourceConsumerTask;
    private bool _completed;
    private int _pendingCount;
    private int _totalEnqueued;
    private int _totalCompleted;
    private int _totalFailed;

    /// <summary>
    /// Creates a coordinator that accepts manual enqueues via EnqueueAsync/TryEnqueue.
    /// </summary>
    public EphemeralWorkCoordinator(
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation>();
        _concurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _runningTasks = new ConcurrentBag<Task>();

        // Bounded channel provides back-pressure
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
    }

    /// <summary>
    /// Creates a coordinator that continuously consumes from an IAsyncEnumerable source.
    /// Runs until the source completes or cancellation is requested.
    /// </summary>
    private EphemeralWorkCoordinator(
        IAsyncEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation>();
        _concurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _runningTasks = new ConcurrentBag<Task>();

        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
        _sourceConsumerTask = ConsumeSourceAsync(source);
    }

    /// <summary>
    /// Creates a coordinator that continuously consumes from an IAsyncEnumerable source.
    /// Runs until the source completes or cancellation is requested.
    /// </summary>
    public static EphemeralWorkCoordinator<T> FromAsyncEnumerable(
        IAsyncEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        return new EphemeralWorkCoordinator<T>(source, body, options);
    }

    /// <summary>
    /// Number of items waiting to be processed.
    /// </summary>
    public int PendingCount => _pendingCount;

    /// <summary>
    /// Number of items currently being processed.
    /// </summary>
    public int ActiveCount => _options.MaxConcurrency - _concurrency.CurrentCount;

    /// <summary>
    /// Total items enqueued since creation.
    /// </summary>
    public int TotalEnqueued => _totalEnqueued;

    /// <summary>
    /// Total items completed successfully.
    /// </summary>
    public int TotalCompleted => _totalCompleted;

    /// <summary>
    /// Total items that failed with an exception.
    /// </summary>
    public int TotalFailed => _totalFailed;

    /// <summary>
    /// Whether Complete() has been called.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Whether all work is done (completed + drained).
    /// </summary>
    public bool IsDrained => _completed && _pendingCount == 0 && ActiveCount == 0;

    /// <summary>
    /// Gets a snapshot of recent operations (both running and completed).
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetSnapshot()
    {
        return _recent.Select(x => x.ToSnapshot()).ToArray();
    }

    /// <summary>
    /// Gets only the currently running operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetRunning()
    {
        return _recent
            .Where(x => x.Completed is null)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets only the completed operations (success or failure).
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetCompleted()
    {
        return _recent
            .Where(x => x.Completed is not null)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets only the failed operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetFailed()
    {
        return _recent
            .Where(x => x.Error is not null)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Enqueue a new item for processing. Blocks if at capacity.
    /// </summary>
    public async ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        if (_completed)
            throw new InvalidOperationException("Coordinator has been completed; no new items accepted.");

        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _pendingCount);
        Interlocked.Increment(ref _totalEnqueued);
    }

    /// <summary>
    /// Try to enqueue without blocking. Returns false if at capacity.
    /// </summary>
    public bool TryEnqueue(T item)
    {
        if (_completed)
            return false;

        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _pendingCount);
            Interlocked.Increment(ref _totalEnqueued);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Signal that no more items will be added. Processing continues until drained.
    /// </summary>
    public void Complete()
    {
        _completed = true;
        _channel.Writer.Complete();
    }

    /// <summary>
    /// Wait for all enqueued work to complete.
    /// For manual enqueue mode, call Complete() first.
    /// For IAsyncEnumerable mode, waits for source to complete.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        // For IAsyncEnumerable source, wait for it to complete first
        if (_sourceConsumerTask is not null)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            await _sourceConsumerTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        else if (!_completed)
        {
            throw new InvalidOperationException("Call Complete() before DrainAsync().");
        }

        using var linkedCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        await _processingTask.WaitAsync(linkedCts2.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel all pending work and stop accepting new items.
    /// </summary>
    public void Cancel()
    {
        _completed = true;
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    private async Task ConsumeSourceAsync(IAsyncEnumerable<T> source)
    {
        try
        {
            await foreach (var item in source.WithCancellation(_cts.Token).ConfigureAwait(false))
            {
                await _channel.Writer.WriteAsync(item, _cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _pendingCount);
                Interlocked.Increment(ref _totalEnqueued);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        finally
        {
            _completed = true;
            _channel.Writer.TryComplete();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await _concurrency.WaitAsync(_cts.Token).ConfigureAwait(false);

                var op = new EphemeralOperation();
                EnqueueOperation(op);
                Interlocked.Decrement(ref _pendingCount);

                var task = ExecuteItemAsync(item, op);
                _runningTasks.Add(task);
            }

            // Wait for any remaining in-flight tasks
            await Task.WhenAll(_runningTasks.ToArray()).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    private async Task ExecuteItemAsync(T item, EphemeralOperation op)
    {
        try
        {
            await _body(item, _cts.Token).ConfigureAwait(false);
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
        }
    }

    private void EnqueueOperation(EphemeralOperation op)
    {
        _recent.Enqueue(op);
        CleanupWindow();
    }

    private void CleanupWindow()
    {
        // Size-based eviction
        while (_recent.Count > _options.MaxTrackedOperations && _recent.TryDequeue(out _))
        {
        }

        // Age-based eviction
        if (_options.MaxOperationLifetime is { } maxAge && _recent.TryPeek(out var head))
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            while (head is not null && head.Started < cutoff && _recent.TryDequeue(out _))
            {
                if (!_recent.TryPeek(out head))
                    break;
            }
        }
    }

    private void SampleIfRequested()
    {
        var sampler = _options.OnSample;
        if (sampler is null) return;

        var snapshot = _recent.Select(x => x.ToSnapshot()).ToArray();
        if (snapshot.Length > 0)
        {
            sampler(snapshot);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        try
        {
            if (_sourceConsumerTask is not null)
                await _sourceConsumerTask.ConfigureAwait(false);
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _concurrency.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Keyed version: per-key sequential execution with fair scheduling.
/// </summary>
public sealed class EphemeralKeyedWorkCoordinator<T, TKey> : IAsyncDisposable
    where TKey : notnull
{
    private readonly Channel<T> _channel;
    private readonly Func<T, TKey> _keySelector;
    private readonly Func<T, CancellationToken, Task> _body;
    private readonly EphemeralOptions _options;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentQueue<EphemeralOperation> _recent;
    private readonly SemaphoreSlim _globalConcurrency;
    private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _perKeyLocks;
    private readonly ConcurrentDictionary<TKey, int> _perKeyPendingCount;
    private readonly Task _processingTask;
    private readonly ConcurrentBag<Task> _runningTasks;
    private readonly Task? _sourceConsumerTask;
    private bool _completed;
    private int _pendingCount;
    private int _totalEnqueued;
    private int _totalCompleted;
    private int _totalFailed;

    /// <summary>
    /// Creates a keyed coordinator that accepts manual enqueues.
    /// </summary>
    public EphemeralKeyedWorkCoordinator(
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation>();
        _globalConcurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _perKeyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();
        _perKeyPendingCount = new ConcurrentDictionary<TKey, int>();
        _runningTasks = new ConcurrentBag<Task>();

        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
    }

    /// <summary>
    /// Creates a keyed coordinator that continuously consumes from an IAsyncEnumerable source.
    /// </summary>
    private EphemeralKeyedWorkCoordinator(
        IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation>();
        _globalConcurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _perKeyLocks = new ConcurrentDictionary<TKey, SemaphoreSlim>();
        _perKeyPendingCount = new ConcurrentDictionary<TKey, int>();
        _runningTasks = new ConcurrentBag<Task>();

        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
        _sourceConsumerTask = ConsumeSourceAsync(source);
    }

    /// <summary>
    /// Creates a keyed coordinator that continuously consumes from an IAsyncEnumerable source.
    /// </summary>
    public static EphemeralKeyedWorkCoordinator<T, TKey> FromAsyncEnumerable(
        IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        return new EphemeralKeyedWorkCoordinator<T, TKey>(source, keySelector, body, options);
    }

    /// <summary>
    /// Number of items waiting to be processed.
    /// </summary>
    public int PendingCount => _pendingCount;

    /// <summary>
    /// Number of items currently being processed.
    /// </summary>
    public int ActiveCount => _options.MaxConcurrency - _globalConcurrency.CurrentCount;

    /// <summary>
    /// Total items enqueued since creation.
    /// </summary>
    public int TotalEnqueued => _totalEnqueued;

    /// <summary>
    /// Total items completed successfully.
    /// </summary>
    public int TotalCompleted => _totalCompleted;

    /// <summary>
    /// Total items that failed with an exception.
    /// </summary>
    public int TotalFailed => _totalFailed;

    /// <summary>
    /// Whether Complete() has been called.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Whether all work is done (completed + drained).
    /// </summary>
    public bool IsDrained => _completed && _pendingCount == 0 && ActiveCount == 0;

    /// <summary>
    /// Gets pending count for a specific key.
    /// </summary>
    public int GetPendingCountForKey(TKey key) =>
        _perKeyPendingCount.TryGetValue(key, out var count) ? count : 0;

    /// <summary>
    /// Gets a snapshot of recent operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetSnapshot() =>
        _recent.Select(x => x.ToSnapshot()).ToArray();

    /// <summary>
    /// Gets operations for a specific key.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetSnapshotForKey(TKey key)
    {
        var keyString = key.ToString();
        return _recent
            .Where(x => x.Key == keyString)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets currently running operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetRunning() =>
        _recent.Where(x => x.Completed is null).Select(x => x.ToSnapshot()).ToArray();

    /// <summary>
    /// Enqueue a new item for processing.
    /// </summary>
    public async ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        if (_completed)
            throw new InvalidOperationException("Coordinator has been completed; no new items accepted.");

        var key = _keySelector(item);
        _perKeyPendingCount.AddOrUpdate(key, 1, (_, c) => c + 1);

        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _pendingCount);
        Interlocked.Increment(ref _totalEnqueued);
    }

    /// <summary>
    /// Try to enqueue without blocking.
    /// When fair scheduling is enabled, returns false if the key is above threshold.
    /// </summary>
    public bool TryEnqueue(T item)
    {
        if (_completed)
            return false;

        var key = _keySelector(item);

        // Fair scheduling: reject if this key is too hot
        if (_options.EnableFairScheduling)
        {
            var keyCount = _perKeyPendingCount.GetOrAdd(key, 0);
            if (keyCount >= _options.FairSchedulingThreshold)
                return false;
        }

        if (_channel.Writer.TryWrite(item))
        {
            _perKeyPendingCount.AddOrUpdate(key, 1, (_, c) => c + 1);
            Interlocked.Increment(ref _pendingCount);
            Interlocked.Increment(ref _totalEnqueued);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Signal that no more items will be added.
    /// </summary>
    public void Complete()
    {
        _completed = true;
        _channel.Writer.Complete();
    }

    /// <summary>
    /// Wait for all enqueued work to complete.
    /// For manual enqueue mode, call Complete() first.
    /// For IAsyncEnumerable mode, waits for source to complete.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        if (_sourceConsumerTask is not null)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            await _sourceConsumerTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        else if (!_completed)
        {
            throw new InvalidOperationException("Call Complete() before DrainAsync().");
        }

        using var linkedCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        await _processingTask.WaitAsync(linkedCts2.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel all pending work.
    /// </summary>
    public void Cancel()
    {
        _completed = true;
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    private async Task ConsumeSourceAsync(IAsyncEnumerable<T> source)
    {
        try
        {
            await foreach (var item in source.WithCancellation(_cts.Token).ConfigureAwait(false))
            {
                var key = _keySelector(item);
                _perKeyPendingCount.AddOrUpdate(key, 1, (_, c) => c + 1);

                await _channel.Writer.WriteAsync(item, _cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _pendingCount);
                Interlocked.Increment(ref _totalEnqueued);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            _completed = true;
            _channel.Writer.TryComplete();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                var key = _keySelector(item);
                var keyGate = _perKeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(_options.MaxConcurrencyPerKey));

                await _globalConcurrency.WaitAsync(_cts.Token).ConfigureAwait(false);
                await keyGate.WaitAsync(_cts.Token).ConfigureAwait(false);

                var op = new EphemeralOperation { Key = key.ToString() };
                EnqueueOperation(op);
                Interlocked.Decrement(ref _pendingCount);
                _perKeyPendingCount.AddOrUpdate(key, 0, (_, c) => Math.Max(0, c - 1));

                var task = ExecuteItemAsync(item, key, op, keyGate);
                _runningTasks.Add(task);
            }

            await Task.WhenAll(_runningTasks.ToArray()).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            // Cleanup per-key semaphores
            foreach (var gate in _perKeyLocks.Values)
            {
                gate.Dispose();
            }
        }
    }

    private async Task ExecuteItemAsync(T item, TKey key, EphemeralOperation op, SemaphoreSlim keyGate)
    {
        try
        {
            await _body(item, _cts.Token).ConfigureAwait(false);
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
            keyGate.Release();
            _globalConcurrency.Release();
            CleanupWindow();
            SampleIfRequested();
        }
    }

    private void EnqueueOperation(EphemeralOperation op)
    {
        _recent.Enqueue(op);
        CleanupWindow();
    }

    private void CleanupWindow()
    {
        while (_recent.Count > _options.MaxTrackedOperations && _recent.TryDequeue(out _))
        {
        }

        if (_options.MaxOperationLifetime is { } maxAge && _recent.TryPeek(out var head))
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            while (head is not null && head.Started < cutoff && _recent.TryDequeue(out _))
            {
                if (!_recent.TryPeek(out head))
                    break;
            }
        }
    }

    private void SampleIfRequested()
    {
        var sampler = _options.OnSample;
        if (sampler is null) return;

        var snapshot = _recent.Select(x => x.ToSnapshot()).ToArray();
        if (snapshot.Length > 0)
        {
            sampler(snapshot);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        try
        {
            if (_sourceConsumerTask is not null)
                await _sourceConsumerTask.ConfigureAwait(false);
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _globalConcurrency.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// A work coordinator that captures results from each operation.
/// Use this when you need to retrieve outcomes (summaries, fingerprints, IDs) from completed work.
/// </summary>
/// <typeparam name="TInput">The type of items being processed.</typeparam>
/// <typeparam name="TResult">The type of result captured from each operation.</typeparam>
public sealed class EphemeralWorkCoordinator<TInput, TResult> : IAsyncDisposable
{
    private readonly Channel<TInput> _channel;
    private readonly Func<TInput, CancellationToken, Task<TResult>> _body;
    private readonly EphemeralOptions _options;
    private readonly CancellationTokenSource _cts;
    private readonly ConcurrentQueue<EphemeralOperation<TResult>> _recent;
    private readonly SemaphoreSlim _concurrency;
    private readonly Task _processingTask;
    private readonly ConcurrentBag<Task> _runningTasks;
    private readonly Task? _sourceConsumerTask;
    private bool _completed;
    private int _pendingCount;
    private int _totalEnqueued;
    private int _totalCompleted;
    private int _totalFailed;

    /// <summary>
    /// Creates a result-capturing coordinator that accepts manual enqueues.
    /// </summary>
    public EphemeralWorkCoordinator(
        Func<TInput, CancellationToken, Task<TResult>> body,
        EphemeralOptions? options = null)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation<TResult>>();
        _concurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _runningTasks = new ConcurrentBag<Task>();

        _channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
    }

    /// <summary>
    /// Creates a result-capturing coordinator that consumes from an IAsyncEnumerable source.
    /// </summary>
    private EphemeralWorkCoordinator(
        IAsyncEnumerable<TInput> source,
        Func<TInput, CancellationToken, Task<TResult>> body,
        EphemeralOptions? options)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
        _options = options ?? new EphemeralOptions();
        _cts = new CancellationTokenSource();
        _recent = new ConcurrentQueue<EphemeralOperation<TResult>>();
        _concurrency = new SemaphoreSlim(_options.MaxConcurrency);
        _runningTasks = new ConcurrentBag<Task>();

        _channel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(_options.MaxTrackedOperations)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = ProcessAsync();
        _sourceConsumerTask = ConsumeSourceAsync(source);
    }

    /// <summary>
    /// Creates a result-capturing coordinator from an IAsyncEnumerable source.
    /// </summary>
    public static EphemeralWorkCoordinator<TInput, TResult> FromAsyncEnumerable(
        IAsyncEnumerable<TInput> source,
        Func<TInput, CancellationToken, Task<TResult>> body,
        EphemeralOptions? options = null)
    {
        return new EphemeralWorkCoordinator<TInput, TResult>(source, body, options);
    }

    /// <summary>
    /// Number of items waiting to be processed.
    /// </summary>
    public int PendingCount => _pendingCount;

    /// <summary>
    /// Number of items currently being processed.
    /// </summary>
    public int ActiveCount => _options.MaxConcurrency - _concurrency.CurrentCount;

    /// <summary>
    /// Total items enqueued since creation.
    /// </summary>
    public int TotalEnqueued => _totalEnqueued;

    /// <summary>
    /// Total items completed successfully.
    /// </summary>
    public int TotalCompleted => _totalCompleted;

    /// <summary>
    /// Total items that failed with an exception.
    /// </summary>
    public int TotalFailed => _totalFailed;

    /// <summary>
    /// Whether Complete() has been called.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Whether all work is done (completed + drained).
    /// </summary>
    public bool IsDrained => _completed && _pendingCount == 0 && ActiveCount == 0;

    /// <summary>
    /// Gets a snapshot of recent operations with their results.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot<TResult>> GetSnapshot()
    {
        return _recent.Select(x => x.ToSnapshot()).ToArray();
    }

    /// <summary>
    /// Gets a snapshot without result data (for logging/metrics).
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot> GetBaseSnapshot()
    {
        return _recent.Select(x => x.ToBaseSnapshot()).ToArray();
    }

    /// <summary>
    /// Gets only successfully completed operations with their results.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot<TResult>> GetSuccessful()
    {
        return _recent
            .Where(x => x.IsSuccess && x.HasResult)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets only the currently running operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot<TResult>> GetRunning()
    {
        return _recent
            .Where(x => x.Completed is null)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets only the failed operations.
    /// </summary>
    public IReadOnlyCollection<EphemeralOperationSnapshot<TResult>> GetFailed()
    {
        return _recent
            .Where(x => x.Error is not null)
            .Select(x => x.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Gets just the results from successful operations (no metadata).
    /// </summary>
    public IReadOnlyCollection<TResult> GetResults()
    {
        return _recent
            .Where(x => x.IsSuccess && x.HasResult)
            .Select(x => x.Result!)
            .ToArray();
    }

    /// <summary>
    /// Enqueue a new item for processing. Blocks if at capacity.
    /// </summary>
    public async ValueTask EnqueueAsync(TInput item, CancellationToken cancellationToken = default)
    {
        if (_completed)
            throw new InvalidOperationException("Coordinator has been completed; no new items accepted.");

        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _pendingCount);
        Interlocked.Increment(ref _totalEnqueued);
    }

    /// <summary>
    /// Try to enqueue without blocking. Returns false if at capacity.
    /// </summary>
    public bool TryEnqueue(TInput item)
    {
        if (_completed)
            return false;

        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _pendingCount);
            Interlocked.Increment(ref _totalEnqueued);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Signal that no more items will be added.
    /// </summary>
    public void Complete()
    {
        _completed = true;
        _channel.Writer.Complete();
    }

    /// <summary>
    /// Wait for all enqueued work to complete.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        if (_sourceConsumerTask is not null)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            await _sourceConsumerTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        else if (!_completed)
        {
            throw new InvalidOperationException("Call Complete() before DrainAsync().");
        }

        using var linkedCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        await _processingTask.WaitAsync(linkedCts2.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancel all pending work.
    /// </summary>
    public void Cancel()
    {
        _completed = true;
        _channel.Writer.TryComplete();
        _cts.Cancel();
    }

    private async Task ConsumeSourceAsync(IAsyncEnumerable<TInput> source)
    {
        try
        {
            await foreach (var item in source.WithCancellation(_cts.Token).ConfigureAwait(false))
            {
                await _channel.Writer.WriteAsync(item, _cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _pendingCount);
                Interlocked.Increment(ref _totalEnqueued);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            _completed = true;
            _channel.Writer.TryComplete();
        }
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                await _concurrency.WaitAsync(_cts.Token).ConfigureAwait(false);

                var op = new EphemeralOperation<TResult>();
                EnqueueOperation(op);
                Interlocked.Decrement(ref _pendingCount);

                var task = ExecuteItemAsync(item, op);
                _runningTasks.Add(task);
            }

            await Task.WhenAll(_runningTasks.ToArray()).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private async Task ExecuteItemAsync(TInput item, EphemeralOperation<TResult> op)
    {
        try
        {
            var result = await _body(item, _cts.Token).ConfigureAwait(false);
            op.Result = result;
            op.HasResult = true;
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
        }
    }

    private void EnqueueOperation(EphemeralOperation<TResult> op)
    {
        _recent.Enqueue(op);
        CleanupWindow();
    }

    private void CleanupWindow()
    {
        while (_recent.Count > _options.MaxTrackedOperations && _recent.TryDequeue(out _))
        {
        }

        if (_options.MaxOperationLifetime is { } maxAge && _recent.TryPeek(out var head))
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            while (head is not null && head.Started < cutoff && _recent.TryDequeue(out _))
            {
                if (!_recent.TryPeek(out head))
                    break;
            }
        }
    }

    private void SampleIfRequested()
    {
        var sampler = _options.OnSample;
        if (sampler is null) return;

        var snapshot = _recent.Select(x => x.ToBaseSnapshot()).ToArray();
        if (snapshot.Length > 0)
        {
            sampler(snapshot);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        try
        {
            if (_sourceConsumerTask is not null)
                await _sourceConsumerTask.ConfigureAwait(false);
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _concurrency.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Factory interface for creating named/typed ephemeral work coordinators.
/// Similar to IHttpClientFactory - each named coordinator shares configuration but has independent state.
/// </summary>
public interface IEphemeralCoordinatorFactory<T>
{
    /// <summary>
    /// Gets or creates a coordinator with the specified name.
    /// Coordinators are cached by name within the factory's scope.
    /// </summary>
    EphemeralWorkCoordinator<T> CreateCoordinator(string name = "");
}

/// <summary>
/// Factory interface for creating named/typed keyed ephemeral work coordinators.
/// </summary>
public interface IEphemeralKeyedCoordinatorFactory<T, TKey>
    where TKey : notnull
{
    /// <summary>
    /// Gets or creates a keyed coordinator with the specified name.
    /// </summary>
    EphemeralKeyedWorkCoordinator<T, TKey> CreateCoordinator(string name = "");
}

/// <summary>
/// Configuration for a named coordinator.
/// </summary>
public sealed class EphemeralCoordinatorConfiguration<T>
{
    internal Func<IServiceProvider, Func<T, CancellationToken, Task>>? BodyFactory { get; set; }
    internal EphemeralOptions? Options { get; set; }
}

/// <summary>
/// Configuration for a named keyed coordinator.
/// </summary>
public sealed class EphemeralKeyedCoordinatorConfiguration<T, TKey>
    where TKey : notnull
{
    internal Func<T, TKey>? KeySelector { get; set; }
    internal Func<IServiceProvider, Func<T, CancellationToken, Task>>? BodyFactory { get; set; }
    internal EphemeralOptions? Options { get; set; }
}

/// <summary>
/// Builder for configuring named ephemeral coordinators.
/// Similar to IHttpClientBuilder.
/// </summary>
public interface IEphemeralCoordinatorBuilder<T>
{
    /// <summary>
    /// The name of the coordinator being configured.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The service collection.
    /// </summary>
    IServiceCollection Services { get; }
}

internal sealed class EphemeralCoordinatorBuilder<T> : IEphemeralCoordinatorBuilder<T>
{
    public EphemeralCoordinatorBuilder(string name, IServiceCollection services)
    {
        Name = name;
        Services = services;
    }

    public string Name { get; }
    public IServiceCollection Services { get; }
}

internal sealed class EphemeralCoordinatorFactory<T> : IEphemeralCoordinatorFactory<T>, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, EphemeralCoordinatorConfiguration<T>> _configurations;
    private readonly ConcurrentDictionary<string, Lazy<EphemeralWorkCoordinator<T>>> _coordinators = new();
    private bool _disposed;

    public EphemeralCoordinatorFactory(
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, EphemeralCoordinatorConfiguration<T>> configurations)
    {
        _serviceProvider = serviceProvider;
        _configurations = configurations;
    }

    public EphemeralWorkCoordinator<T> CreateCoordinator(string name = "")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _coordinators.GetOrAdd(name, n => new Lazy<EphemeralWorkCoordinator<T>>(() =>
        {
            if (!_configurations.TryGetValue(n, out var config))
            {
                throw new InvalidOperationException(
                    $"No coordinator configuration found for name '{n}'. " +
                    $"Call AddEphemeralWorkCoordinator<{typeof(T).Name}>(\"{n}\", ...) during registration.");
            }

            var body = config.BodyFactory!(_serviceProvider);
            return new EphemeralWorkCoordinator<T>(body, config.Options);
        })).Value;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var lazy in _coordinators.Values)
        {
            if (lazy.IsValueCreated)
            {
                lazy.Value.Cancel();
            }
        }
    }
}

internal sealed class EphemeralKeyedCoordinatorFactory<T, TKey> : IEphemeralKeyedCoordinatorFactory<T, TKey>, IDisposable
    where TKey : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, EphemeralKeyedCoordinatorConfiguration<T, TKey>> _configurations;
    private readonly ConcurrentDictionary<string, Lazy<EphemeralKeyedWorkCoordinator<T, TKey>>> _coordinators = new();
    private bool _disposed;

    public EphemeralKeyedCoordinatorFactory(
        IServiceProvider serviceProvider,
        ConcurrentDictionary<string, EphemeralKeyedCoordinatorConfiguration<T, TKey>> configurations)
    {
        _serviceProvider = serviceProvider;
        _configurations = configurations;
    }

    public EphemeralKeyedWorkCoordinator<T, TKey> CreateCoordinator(string name = "")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _coordinators.GetOrAdd(name, n => new Lazy<EphemeralKeyedWorkCoordinator<T, TKey>>(() =>
        {
            if (!_configurations.TryGetValue(n, out var config))
            {
                throw new InvalidOperationException(
                    $"No keyed coordinator configuration found for name '{n}'. " +
                    $"Call AddEphemeralKeyedWorkCoordinator<{typeof(T).Name}, {typeof(TKey).Name}>(\"{n}\", ...) during registration.");
            }

            var body = config.BodyFactory!(_serviceProvider);
            return new EphemeralKeyedWorkCoordinator<T, TKey>(config.KeySelector!, body, config.Options);
        })).Value;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var lazy in _coordinators.Values)
        {
            if (lazy.IsValueCreated)
            {
                lazy.Value.Cancel();
            }
        }
    }
}

/// <summary>
/// DI extensions for registering ephemeral work coordinators as services.
/// </summary>
public static class EphemeralServiceCollectionExtensions
{
    /// <summary>
    /// Registers an EphemeralWorkCoordinator as a singleton service.
    /// The coordinator starts processing immediately and runs for the lifetime of the application.
    /// </summary>
    public static IServiceCollection AddEphemeralWorkCoordinator<T>(
        this IServiceCollection services,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
    {
        services.TryAddSingleton(sp =>
        {
            var body = bodyFactory(sp);
            return new EphemeralWorkCoordinator<T>(body, options);
        });

        return services;
    }

    /// <summary>
    /// Registers an EphemeralWorkCoordinator as a singleton with a simpler body (no service provider needed).
    /// </summary>
    public static IServiceCollection AddEphemeralWorkCoordinator<T>(
        this IServiceCollection services,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        return services.AddEphemeralWorkCoordinator<T>(_ => body, options);
    }

    /// <summary>
    /// Registers an EphemeralKeyedWorkCoordinator as a singleton service.
    /// </summary>
    public static IServiceCollection AddEphemeralKeyedWorkCoordinator<T, TKey>(
        this IServiceCollection services,
        Func<T, TKey> keySelector,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        services.TryAddSingleton(sp =>
        {
            var body = bodyFactory(sp);
            return new EphemeralKeyedWorkCoordinator<T, TKey>(keySelector, body, options);
        });

        return services;
    }

    /// <summary>
    /// Registers an EphemeralKeyedWorkCoordinator with a simpler body.
    /// </summary>
    public static IServiceCollection AddEphemeralKeyedWorkCoordinator<T, TKey>(
        this IServiceCollection services,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        return services.AddEphemeralKeyedWorkCoordinator<T, TKey>(keySelector, _ => body, options);
    }

    /// <summary>
    /// Registers a scoped coordinator factory that creates coordinators per scope.
    /// Each scope gets its own coordinator that is disposed when the scope ends.
    /// </summary>
    public static IServiceCollection AddScopedEphemeralWorkCoordinator<T>(
        this IServiceCollection services,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
    {
        services.TryAddScoped(sp =>
        {
            var body = bodyFactory(sp);
            return new EphemeralWorkCoordinator<T>(body, options);
        });

        return services;
    }

    /// <summary>
    /// Registers a scoped keyed coordinator factory.
    /// </summary>
    public static IServiceCollection AddScopedEphemeralKeyedWorkCoordinator<T, TKey>(
        this IServiceCollection services,
        Func<T, TKey> keySelector,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        services.TryAddScoped(sp =>
        {
            var body = bodyFactory(sp);
            return new EphemeralKeyedWorkCoordinator<T, TKey>(keySelector, body, options);
        });

        return services;
    }

    // ========================================================================
    // NAMED/TYPED COORDINATOR FACTORY PATTERN (like AddHttpClient)
    // ========================================================================

    /// <summary>
    /// Registers a named ephemeral work coordinator using the factory pattern.
    /// Similar to AddHttpClient - allows multiple named configurations that share a factory.
    /// </summary>
    /// <example>
    /// // Register named coordinators
    /// services.AddEphemeralWorkCoordinator&lt;TranslationRequest&gt;("fast",
    ///     sp =&gt; async (req, ct) =&gt; await TranslateAsync(req, ct),
    ///     new EphemeralOptions { MaxConcurrency = 32 });
    ///
    /// services.AddEphemeralWorkCoordinator&lt;TranslationRequest&gt;("slow",
    ///     sp =&gt; async (req, ct) =&gt; await TranslateSlowAsync(req, ct),
    ///     new EphemeralOptions { MaxConcurrency = 4 });
    ///
    /// // Inject the factory
    /// public class MyService
    /// {
    ///     private readonly EphemeralWorkCoordinator&lt;TranslationRequest&gt; _fast;
    ///     private readonly EphemeralWorkCoordinator&lt;TranslationRequest&gt; _slow;
    ///
    ///     public MyService(IEphemeralCoordinatorFactory&lt;TranslationRequest&gt; factory)
    ///     {
    ///         _fast = factory.CreateCoordinator("fast");
    ///         _slow = factory.CreateCoordinator("slow");
    ///     }
    /// }
    /// </example>
    public static IEphemeralCoordinatorBuilder<T> AddEphemeralWorkCoordinator<T>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
    {
        // Ensure the configuration dictionary exists
        var configKey = typeof(ConcurrentDictionary<string, EphemeralCoordinatorConfiguration<T>>);
        var configurations = services
            .Where(sd => sd.ServiceType == configKey)
            .Select(sd => sd.ImplementationInstance)
            .OfType<ConcurrentDictionary<string, EphemeralCoordinatorConfiguration<T>>>()
            .FirstOrDefault();

        if (configurations is null)
        {
            configurations = new ConcurrentDictionary<string, EphemeralCoordinatorConfiguration<T>>();
            services.AddSingleton(configurations);

            // Register the factory as singleton
            services.AddSingleton<IEphemeralCoordinatorFactory<T>>(sp =>
                new EphemeralCoordinatorFactory<T>(sp, configurations));
        }

        // Add or update the configuration for this name
        configurations[name] = new EphemeralCoordinatorConfiguration<T>
        {
            BodyFactory = bodyFactory,
            Options = options
        };

        return new EphemeralCoordinatorBuilder<T>(name, services);
    }

    /// <summary>
    /// Registers a named ephemeral work coordinator with a simpler body (no IServiceProvider needed).
    /// </summary>
    public static IEphemeralCoordinatorBuilder<T> AddEphemeralWorkCoordinator<T>(
        this IServiceCollection services,
        string name,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
    {
        return services.AddEphemeralWorkCoordinator<T>(name, _ => body, options);
    }

    /// <summary>
    /// Registers a named keyed ephemeral work coordinator using the factory pattern.
    /// </summary>
    public static IServiceCollection AddEphemeralKeyedWorkCoordinator<T, TKey>(
        this IServiceCollection services,
        string name,
        Func<T, TKey> keySelector,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        var configKey = typeof(ConcurrentDictionary<string, EphemeralKeyedCoordinatorConfiguration<T, TKey>>);
        var configurations = services
            .Where(sd => sd.ServiceType == configKey)
            .Select(sd => sd.ImplementationInstance)
            .OfType<ConcurrentDictionary<string, EphemeralKeyedCoordinatorConfiguration<T, TKey>>>()
            .FirstOrDefault();

        if (configurations is null)
        {
            configurations = new ConcurrentDictionary<string, EphemeralKeyedCoordinatorConfiguration<T, TKey>>();
            services.AddSingleton(configurations);

            services.AddSingleton<IEphemeralKeyedCoordinatorFactory<T, TKey>>(sp =>
                new EphemeralKeyedCoordinatorFactory<T, TKey>(sp, configurations));
        }

        configurations[name] = new EphemeralKeyedCoordinatorConfiguration<T, TKey>
        {
            KeySelector = keySelector,
            BodyFactory = bodyFactory,
            Options = options
        };

        return services;
    }

    /// <summary>
    /// Registers a named keyed ephemeral work coordinator with a simpler body.
    /// </summary>
    public static IServiceCollection AddEphemeralKeyedWorkCoordinator<T, TKey>(
        this IServiceCollection services,
        string name,
        Func<T, TKey> keySelector,
        Func<T, CancellationToken, Task> body,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        return services.AddEphemeralKeyedWorkCoordinator<T, TKey>(name, keySelector, _ => body, options);
    }

    /// <summary>
    /// Registers a default (unnamed) coordinator via the factory pattern.
    /// Use CreateCoordinator() without arguments to retrieve it.
    /// </summary>
    public static IEphemeralCoordinatorBuilder<T> AddEphemeralWorkCoordinatorFactory<T>(
        this IServiceCollection services,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
    {
        return services.AddEphemeralWorkCoordinator<T>("", bodyFactory, options);
    }

    /// <summary>
    /// Registers a default (unnamed) keyed coordinator via the factory pattern.
    /// </summary>
    public static IServiceCollection AddEphemeralKeyedWorkCoordinatorFactory<T, TKey>(
        this IServiceCollection services,
        Func<T, TKey> keySelector,
        Func<IServiceProvider, Func<T, CancellationToken, Task>> bodyFactory,
        EphemeralOptions? options = null)
        where TKey : notnull
    {
        return services.AddEphemeralKeyedWorkCoordinator<T, TKey>("", keySelector, bodyFactory, options);
    }
}
