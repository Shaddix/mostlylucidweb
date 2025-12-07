namespace Mostlylucid.Helpers.Ephemeral.Examples;

/// <summary>
/// Demonstrates dynamic concurrency: adjust MaxConcurrency in response to signals.
/// Includes time-windowed sensing (only react to recent signals) and hysteresis
/// (minimum interval between adjustments) to avoid thrashing. Use when a queue should self-tune off telemetry.
/// </summary>
public sealed class DynamicConcurrencyDemo<T> : IAsyncDisposable
{
    private readonly EphemeralWorkCoordinator<T> _coordinator;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly int _min;
    private readonly int _max;
    private readonly string _scaleUpPattern;
    private readonly string _scaleDownPattern;
    private readonly TimeSpan _signalWindow;
    private readonly TimeSpan _minAdjustInterval;
    private readonly TimeSpan _sampleInterval;
    private readonly Action<int, int>? _onAdjusted;
    private DateTimeOffset _lastAdjust = DateTimeOffset.MinValue;

    public DynamicConcurrencyDemo(
        Func<T, CancellationToken, Task> body,
        SignalSink sink,
        int minConcurrency = 1,
        int maxConcurrency = 32,
        string scaleUpPattern = "load.high",
        string scaleDownPattern = "load.low",
        TimeSpan? signalWindow = null,
        TimeSpan? minAdjustInterval = null,
        TimeSpan? sampleInterval = null,
        Action<int, int>? onAdjusted = null)
    {
        _min = Math.Max(1, minConcurrency);
        _max = Math.Max(_min, maxConcurrency);
        _scaleUpPattern = scaleUpPattern;
        _scaleDownPattern = scaleDownPattern;
        _signalWindow = signalWindow ?? TimeSpan.FromSeconds(5);
        _minAdjustInterval = minAdjustInterval ?? TimeSpan.FromSeconds(1);
        _sampleInterval = (sampleInterval is { Ticks: > 0 } ? sampleInterval : TimeSpan.FromMilliseconds(200)).Value;
        _onAdjusted = onAdjusted;

        _coordinator = new EphemeralWorkCoordinator<T>(
            body,
            new EphemeralOptions
            {
                MaxConcurrency = _min,
                EnableDynamicConcurrency = true,
                Signals = sink
            });

        _loop = Task.Run(() => WatchAsync(sink));
    }

    public int CurrentMaxConcurrency => _coordinator.CurrentMaxConcurrency;

    private async Task WatchAsync(SignalSink sink)
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // Hysteresis: don't adjust more frequently than _minAdjustInterval
                if (DateTimeOffset.UtcNow - _lastAdjust >= _minAdjustInterval)
                {
                    // Time-windowed sensing: only react to recent signals
                    var cutoff = DateTimeOffset.UtcNow - _signalWindow;
                    var snapshot = sink.Sense(s => s.Timestamp >= cutoff);
                    SignalEvent? latestUp = null;
                    SignalEvent? latestDown = null;

                    foreach (var s in snapshot)
                    {
                        if (StringPatternMatcher.Matches(s.Signal, _scaleUpPattern) &&
                            (latestUp is null || s.Timestamp > latestUp.Value.Timestamp))
                        {
                            latestUp = s;
                        }
                        if (StringPatternMatcher.Matches(s.Signal, _scaleDownPattern) &&
                            (latestDown is null || s.Timestamp > latestDown.Value.Timestamp))
                        {
                            latestDown = s;
                        }
                    }

                    var preferUp = latestUp is not null && (latestDown is null || latestUp.Value.Timestamp >= latestDown.Value.Timestamp);
                    var preferDown = latestDown is not null && (latestUp is null || latestDown.Value.Timestamp > latestUp.Value.Timestamp);

                    if (preferUp)
                    {
                        var current = _coordinator.CurrentMaxConcurrency;
                        var next = Math.Min(_max, current * 2);
                        if (next != current)
                        {
                            _coordinator.SetMaxConcurrency(next);
                            _onAdjusted?.Invoke(current, next);
                            _lastAdjust = DateTimeOffset.UtcNow;
                        }
                    }
                    else if (preferDown)
                    {
                        var current = _coordinator.CurrentMaxConcurrency;
                        var next = Math.Max(_min, current / 2);
                        if (next != current)
                        {
                            _coordinator.SetMaxConcurrency(next);
                            _onAdjusted?.Invoke(current, next);
                            _lastAdjust = DateTimeOffset.UtcNow;
                        }
                    }
                }
            }
            catch { /* ignore */ }

            try { await Task.Delay(_sampleInterval, _cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    public ValueTask<long> EnqueueAsync(T item, CancellationToken ct = default)
        => _coordinator.EnqueueWithIdAsync(item, ct);

    public async Task DrainAsync(CancellationToken ct = default)
    {
        _coordinator.Complete();
        await _coordinator.DrainAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _loop.ConfigureAwait(false); } catch { /* ignore */ }
        _cts.Dispose();
        await _coordinator.DisposeAsync();
    }
}
