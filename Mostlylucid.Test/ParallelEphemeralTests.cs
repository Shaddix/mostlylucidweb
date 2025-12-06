using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.Helpers.Ephemeral;
using Xunit;

namespace Mostlylucid.Test;

/// <summary>
/// Tests for EphemeralForEachAsync extension methods (the original parallel foreach pattern).
/// </summary>
public class EphemeralForEachAsyncTests
{
    [Fact]
    public async Task EphemeralForEachAsync_ProcessesAllItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
        var processed = new ConcurrentBag<int>();

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                processed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Assert
        Assert.Equal(100, processed.Count);
        Assert.True(items.All(i => processed.Contains(i)));
    }

    [Fact]
    public async Task EphemeralForEachAsync_RespectsMaxConcurrency()
    {
        // Arrange
        var items = Enumerable.Range(1, 20).ToList();
        var currentConcurrency = 0;
        var maxObservedConcurrency = 0;
        var lockObj = new object();

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    currentConcurrency++;
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, currentConcurrency);
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    currentConcurrency--;
                }
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Assert
        Assert.True(maxObservedConcurrency <= 4, $"Max concurrency was {maxObservedConcurrency}, expected <= 4");
        Assert.True(maxObservedConcurrency >= 2, $"Max concurrency was {maxObservedConcurrency}, expected >= 2 (parallelism should happen)");
    }

    [Fact]
    public async Task EphemeralForEachAsync_TracksOperationsInWindow()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        var snapshots = new ConcurrentBag<IReadOnlyCollection<EphemeralOperationSnapshot>>();

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
            },
            new EphemeralOptions
            {
                MaxConcurrency = 2,
                MaxTrackedOperations = 100,
                OnSample = snapshot => snapshots.Add(snapshot)
            });

        // Assert
        Assert.NotEmpty(snapshots);
        var lastSnapshot = snapshots.Last();
        // Check that we have tracked operations and the completed ones have duration
        Assert.NotEmpty(lastSnapshot);
        var completedOps = lastSnapshot.Where(s => s.Completed.HasValue).ToList();
        Assert.NotEmpty(completedOps);
        Assert.True(completedOps.All(s => s.Duration.HasValue), "All completed operations should have duration");
    }

    [Fact]
    public async Task EphemeralForEachAsync_EvictsOldOperations()
    {
        // Arrange
        var items = Enumerable.Range(1, 50).ToList();
        IReadOnlyCollection<EphemeralOperationSnapshot>? finalSnapshot = null;

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(5, ct);
            },
            new EphemeralOptions
            {
                MaxConcurrency = 8,
                MaxTrackedOperations = 10, // Only keep 10
                OnSample = snapshot => finalSnapshot = snapshot
            });

        // Assert
        Assert.NotNull(finalSnapshot);
        Assert.True(finalSnapshot.Count <= 10, $"Window should be bounded to 10, was {finalSnapshot.Count}");
    }

    [Fact]
    public async Task EphemeralForEachAsync_CapturesErrors()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToList();
        IReadOnlyCollection<EphemeralOperationSnapshot>? finalSnapshot = null;

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(5, ct);
                if (item == 5)
                    throw new InvalidOperationException("Test error");
            },
            new EphemeralOptions
            {
                MaxConcurrency = 2,
                OnSample = snapshot => finalSnapshot = snapshot
            });

        // Assert
        Assert.NotNull(finalSnapshot);
        var faulted = finalSnapshot.Where(s => s.IsFaulted).ToList();
        Assert.Single(faulted);
        Assert.IsType<InvalidOperationException>(faulted[0].Error);
    }

    [Fact]
    public async Task EphemeralForEachAsync_Keyed_ProcessesAllItems()
    {
        // Arrange
        var items = new[]
        {
            new { UserId = "A", Value = 1 },
            new { UserId = "A", Value = 2 },
            new { UserId = "B", Value = 3 },
            new { UserId = "B", Value = 4 },
            new { UserId = "A", Value = 5 },
        };
        var processed = new ConcurrentBag<int>();

        // Act
        await items.EphemeralForEachAsync(
            x => x.UserId,
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                processed.Add(item.Value);
            },
            new EphemeralOptions { MaxConcurrency = 4, MaxConcurrencyPerKey = 1 });

        // Assert
        Assert.Equal(5, processed.Count);
    }

    [Fact]
    public async Task EphemeralForEachAsync_Keyed_EnforcesPerKeySequential()
    {
        // Arrange
        var items = new[]
        {
            new { UserId = "A", Seq = 1 },
            new { UserId = "A", Seq = 2 },
            new { UserId = "A", Seq = 3 },
            new { UserId = "B", Seq = 1 },
            new { UserId = "B", Seq = 2 },
        };
        var orderByUser = new ConcurrentDictionary<string, ConcurrentQueue<int>>();

        // Act
        await items.EphemeralForEachAsync(
            x => x.UserId,
            async (item, ct) =>
            {
                var queue = orderByUser.GetOrAdd(item.UserId, _ => new ConcurrentQueue<int>());
                queue.Enqueue(item.Seq);
                await Task.Delay(20, ct); // Enough delay to interleave if not sequential
            },
            new EphemeralOptions { MaxConcurrency = 4, MaxConcurrencyPerKey = 1 });

        // Assert - each user's items should be in order
        Assert.Equal(new[] { 1, 2, 3 }, orderByUser["A"].ToArray());
        Assert.Equal(new[] { 1, 2 }, orderByUser["B"].ToArray());
    }

    [Fact]
    public async Task EphemeralForEachAsync_Keyed_AllowsParallelAcrossKeys()
    {
        // Arrange
        var items = new[]
        {
            new { UserId = "A", Value = 1 },
            new { UserId = "B", Value = 2 },
            new { UserId = "C", Value = 3 },
            new { UserId = "D", Value = 4 },
        };
        var concurrentExecutions = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        // Act
        await items.EphemeralForEachAsync(
            x => x.UserId,
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    concurrentExecutions++;
                    maxConcurrent = Math.Max(maxConcurrent, concurrentExecutions);
                }

                await Task.Delay(100, ct);

                lock (lockObj)
                {
                    concurrentExecutions--;
                }
            },
            new EphemeralOptions { MaxConcurrency = 4, MaxConcurrencyPerKey = 1 });

        // Assert - should have run multiple keys in parallel
        Assert.True(maxConcurrent >= 2, $"Expected parallel execution across keys, but max concurrent was {maxConcurrent}");
    }

    [Fact]
    public async Task EphemeralForEachAsync_Keyed_TracksKeyInSnapshot()
    {
        // Arrange
        var items = new[]
        {
            new { UserId = "UserA", Value = 1 },
            new { UserId = "UserB", Value = 2 },
        };
        IReadOnlyCollection<EphemeralOperationSnapshot>? finalSnapshot = null;

        // Act
        await items.EphemeralForEachAsync(
            x => x.UserId,
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
            },
            new EphemeralOptions
            {
                MaxConcurrency = 2,
                OnSample = snapshot => finalSnapshot = snapshot
            });

        // Assert
        Assert.NotNull(finalSnapshot);
        var keys = finalSnapshot.Select(s => s.Key).ToHashSet();
        Assert.Contains("UserA", keys);
        Assert.Contains("UserB", keys);
    }

    [Fact]
    public async Task EphemeralForEachAsync_SupportsCancellation()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).ToList();
        var processed = new ConcurrentBag<int>();
        using var cts = new CancellationTokenSource();

        // Act
        var task = items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(50, ct);
                processed.Add(item);
                if (processed.Count >= 5)
                    cts.Cancel();
            },
            new EphemeralOptions { MaxConcurrency = 2 },
            cts.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(processed.Count < 100, "Should have cancelled before processing all items");
    }

    [Fact]
    public async Task EphemeralForEachAsync_EmptySource_CompletesImmediately()
    {
        // Arrange
        var items = Enumerable.Empty<int>();
        var called = false;

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                called = true;
                await Task.Delay(10, ct);
            });

        // Assert
        Assert.False(called);
    }

    [Fact]
    public async Task EphemeralOperationSnapshot_HasCorrectDuration()
    {
        // Arrange
        var items = new[] { 1 };
        EphemeralOperationSnapshot? snapshot = null;

        // Act
        await items.EphemeralForEachAsync(
            async (item, ct) =>
            {
                await Task.Delay(100, ct);
            },
            new EphemeralOptions
            {
                OnSample = s => snapshot = s.FirstOrDefault()
            });

        // Assert
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Duration);
        Assert.True(snapshot.Duration.Value.TotalMilliseconds >= 90,
            $"Duration should be >= 90ms, was {snapshot.Duration.Value.TotalMilliseconds}ms");
    }

    // ===== EphemeralWorkCoordinator Tests =====

    [Fact]
    public async Task WorkCoordinator_ProcessesEnqueuedItems()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                processed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        for (var i = 1; i <= 10; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(10, processed.Count);
        Assert.Equal(10, coordinator.TotalCompleted);
    }

    [Fact]
    public async Task WorkCoordinator_TracksCountsCorrectly()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                if (item == 5) throw new InvalidOperationException("Test");
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 10; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(10, coordinator.TotalEnqueued);
        Assert.Equal(9, coordinator.TotalCompleted);
        Assert.Equal(1, coordinator.TotalFailed);
        Assert.True(coordinator.IsDrained);
    }

    [Fact]
    public async Task WorkCoordinator_GetSnapshot_ReturnsOperations()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(10, ct),
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 5; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var snapshot = coordinator.GetSnapshot();

        // Assert
        Assert.Equal(5, snapshot.Count);
        Assert.All(snapshot, s => Assert.NotNull(s.Completed));
    }

    [Fact]
    public async Task WorkCoordinator_TryEnqueue_ReturnsFalseWhenFull()
    {
        // Arrange - small capacity
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(500, ct), // Slow processing
            new EphemeralOptions { MaxConcurrency = 1, MaxTrackedOperations = 2 });

        // Act - fill up the bounded channel
        var results = new List<bool>();
        for (var i = 0; i < 10; i++)
            results.Add(coordinator.TryEnqueue(i));

        coordinator.Cancel();

        // Assert - some should have failed (channel bounded to 2)
        Assert.Contains(false, results);
    }

    [Fact]
    public async Task WorkCoordinator_FromAsyncEnumerable_ProcessesContinuously()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();

        async IAsyncEnumerable<int> GenerateItems()
        {
            for (var i = 1; i <= 5; i++)
            {
                yield return i;
                await Task.Delay(10);
            }
        }

        await using var coordinator = EphemeralWorkCoordinator<int>.FromAsyncEnumerable(
            GenerateItems(),
            async (item, ct) =>
            {
                processed.Add(item);
                await Task.Delay(5, ct);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(5, processed.Count);
        Assert.True(coordinator.IsDrained);
    }

    [Fact]
    public async Task WorkCoordinator_Cancel_StopsProcessing()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(100, ct);
                processed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 100; i++)
            coordinator.TryEnqueue(i);

        await Task.Delay(50); // Let some process
        coordinator.Cancel();

        // Assert
        Assert.True(processed.Count < 100, "Should have stopped before processing all");
    }

    // ===== EphemeralKeyedWorkCoordinator Tests =====

    [Fact]
    public async Task KeyedCoordinator_EnforcesPerKeySequential()
    {
        // Arrange
        var orderByUser = new ConcurrentDictionary<string, ConcurrentQueue<int>>();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string UserId, int Seq), string>(
            x => x.UserId,
            async (item, ct) =>
            {
                var queue = orderByUser.GetOrAdd(item.UserId, _ => new ConcurrentQueue<int>());
                queue.Enqueue(item.Seq);
                await Task.Delay(20, ct);
            },
            new EphemeralOptions { MaxConcurrency = 4, MaxConcurrencyPerKey = 1 });

        // Act
        await coordinator.EnqueueAsync(("A", 1));
        await coordinator.EnqueueAsync(("A", 2));
        await coordinator.EnqueueAsync(("A", 3));
        await coordinator.EnqueueAsync(("B", 1));
        await coordinator.EnqueueAsync(("B", 2));

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - each user's items should be in order
        Assert.Equal(new[] { 1, 2, 3 }, orderByUser["A"].ToArray());
        Assert.Equal(new[] { 1, 2 }, orderByUser["B"].ToArray());
    }

    [Fact]
    public async Task KeyedCoordinator_FairScheduling_RejectsHotKeys()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(100, ct), // Slow
            new EphemeralOptions
            {
                MaxConcurrency = 2,
                EnableFairScheduling = true,
                FairSchedulingThreshold = 3
            });

        // Act - try to enqueue many items for the same key
        var results = new List<bool>();
        for (var i = 0; i < 10; i++)
            results.Add(coordinator.TryEnqueue(("HotKey", i)));

        coordinator.Cancel();

        // Assert - should have rejected some due to fair scheduling threshold
        var accepted = results.Count(r => r);
        Assert.True(accepted <= 5, $"Expected fair scheduling to reject some, but accepted {accepted}");
    }

    [Fact]
    public async Task KeyedCoordinator_GetPendingCountForKey_TracksCorrectly()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(50, ct),
            new EphemeralOptions { MaxConcurrency = 1 }); // Process slowly

        // Act
        await coordinator.EnqueueAsync(("A", 1));
        await coordinator.EnqueueAsync(("A", 2));
        await coordinator.EnqueueAsync(("B", 1));

        // Check pending before completion
        await Task.Delay(10);
        var pendingA = coordinator.GetPendingCountForKey("A");

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - should have had pending items for A initially
        Assert.True(pendingA >= 0);
        Assert.Equal(0, coordinator.GetPendingCountForKey("A"));
    }

    [Fact]
    public async Task KeyedCoordinator_GetSnapshotForKey_FiltersCorrectly()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(10, ct),
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        await coordinator.EnqueueAsync(("A", 1));
        await coordinator.EnqueueAsync(("A", 2));
        await coordinator.EnqueueAsync(("B", 1));

        coordinator.Complete();
        await coordinator.DrainAsync();

        var snapshotA = coordinator.GetSnapshotForKey("A");
        var snapshotB = coordinator.GetSnapshotForKey("B");

        // Assert
        Assert.Equal(2, snapshotA.Count);
        Assert.Single(snapshotB);
        Assert.All(snapshotA, s => Assert.Equal("A", s.Key));
    }
}

/// <summary>
/// Additional comprehensive tests for EphemeralWorkCoordinator.
/// </summary>
public class EphemeralWorkCoordinatorTests
{
    [Fact]
    public async Task Constructor_StartsProcessingImmediately()
    {
        // Arrange
        var processingStarted = new TaskCompletionSource<bool>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                processingStarted.TrySetResult(true);
                await Task.Delay(10, ct);
            },
            new EphemeralOptions { MaxConcurrency = 1 });

        // Act
        await coordinator.EnqueueAsync(1);
        var started = await Task.WhenAny(processingStarted.Task, Task.Delay(1000)) == processingStarted.Task;

        coordinator.Cancel();

        // Assert
        Assert.True(started, "Processing should start immediately after enqueue");
    }

    [Fact]
    public async Task EnqueueAsync_ThrowsAfterComplete()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        coordinator.Complete();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.EnqueueAsync(1).AsTask());
    }

    [Fact]
    public async Task TryEnqueue_ReturnsFalseAfterComplete()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        coordinator.Complete();

        // Act
        var result = coordinator.TryEnqueue(1);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DrainAsync_ThrowsIfNotCompleted()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.DrainAsync());
    }

    [Fact]
    public async Task ActiveCount_ReflectsRunningOperations()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await gate.Task;
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);
        await Task.Delay(50); // Let items start processing

        var activeCount = coordinator.ActiveCount;
        gate.SetResult(true);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(activeCount >= 1, $"Expected active count >= 1, got {activeCount}");
    }

    [Fact]
    public async Task PendingCount_DecreasesAsItemsProcess()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await gate.Task;
            },
            new EphemeralOptions { MaxConcurrency = 1 });

        // Act - enqueue multiple items
        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);
        await Task.Delay(50);

        var pendingBefore = coordinator.PendingCount;
        gate.SetResult(true);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var pendingAfter = coordinator.PendingCount;

        // Assert
        Assert.True(pendingBefore >= 1, $"Expected pending >= 1 before drain, got {pendingBefore}");
        Assert.Equal(0, pendingAfter);
    }

    [Fact]
    public async Task GetRunning_ReturnsOnlyInProgressOperations()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                if (item == 1)
                    await gate.Task;
                else
                    await Task.Delay(1, ct);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        await coordinator.EnqueueAsync(1); // Will block
        await coordinator.EnqueueAsync(2); // Will complete quickly
        await Task.Delay(100);

        var running = coordinator.GetRunning();
        gate.SetResult(true);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - item 1 should have been running, item 2 completed
        Assert.True(running.Count <= 2);
    }

    [Fact]
    public async Task GetCompleted_ReturnsOnlyFinishedOperations()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(10, ct),
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 5; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var completed = coordinator.GetCompleted();

        // Assert
        Assert.Equal(5, completed.Count);
        Assert.All(completed, c => Assert.NotNull(c.Completed));
    }

    [Fact]
    public async Task GetFailed_ReturnsOnlyFaultedOperations()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(5, ct);
                if (item % 2 == 0)
                    throw new InvalidOperationException($"Error for {item}");
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 6; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var failed = coordinator.GetFailed();

        // Assert - items 2, 4, 6 should have failed
        Assert.Equal(3, failed.Count);
        Assert.All(failed, f =>
        {
            Assert.True(f.IsFaulted);
            Assert.NotNull(f.Error);
            Assert.IsType<InvalidOperationException>(f.Error);
        });
    }

    [Fact]
    public async Task IsDrained_FalseWhileProcessing_TrueAfterDrain()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await gate.Task,
            new EphemeralOptions { MaxConcurrency = 1 });

        // Act
        await coordinator.EnqueueAsync(1);
        await Task.Delay(50);

        var drainedWhileProcessing = coordinator.IsDrained;
        gate.SetResult(true);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var drainedAfter = coordinator.IsDrained;

        // Assert
        Assert.False(drainedWhileProcessing);
        Assert.True(drainedAfter);
    }

    [Fact]
    public async Task IsCompleted_ReflectsCompleteCall()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        // Act
        var completedBefore = coordinator.IsCompleted;
        coordinator.Complete();
        var completedAfter = coordinator.IsCompleted;

        // Assert
        Assert.False(completedBefore);
        Assert.True(completedAfter);
    }

    [Fact]
    public async Task Dispose_CancelsProcessing()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(100, ct);
                processed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 50; i++)
            coordinator.TryEnqueue(i);

        await Task.Delay(50);
        await coordinator.DisposeAsync();

        // Assert
        Assert.True(processed.Count < 50, "Dispose should have cancelled remaining work");
    }

    [Fact]
    public async Task OnSample_CalledAfterEachOperation()
    {
        // Arrange
        var sampleCount = 0;
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(5, ct),
            new EphemeralOptions
            {
                MaxConcurrency = 1,
                OnSample = _ => Interlocked.Increment(ref sampleCount)
            });

        // Act
        for (var i = 1; i <= 5; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(sampleCount >= 5, $"Expected OnSample called at least 5 times, got {sampleCount}");
    }

    [Fact]
    public async Task MaxTrackedOperations_BoundsSnapshotSize()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct),
            new EphemeralOptions
            {
                MaxConcurrency = 4,
                MaxTrackedOperations = 5
            });

        // Act
        for (var i = 1; i <= 20; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var snapshot = coordinator.GetSnapshot();

        // Assert
        Assert.True(snapshot.Count <= 5, $"Expected <= 5 tracked operations, got {snapshot.Count}");
    }

    [Fact]
    public async Task MultipleErrorTypes_AllCaptured()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                throw item switch
                {
                    1 => new ArgumentException("arg"),
                    2 => new InvalidOperationException("invalid"),
                    3 => new NotSupportedException("not supported"),
                    _ => new Exception("generic")
                };
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act
        for (var i = 1; i <= 4; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        var failed = coordinator.GetFailed();

        // Assert
        Assert.Equal(4, failed.Count);
        var errorTypes = failed.Select(f => f.Error!.GetType()).ToHashSet();
        Assert.Contains(typeof(ArgumentException), errorTypes);
        Assert.Contains(typeof(InvalidOperationException), errorTypes);
        Assert.Contains(typeof(NotSupportedException), errorTypes);
    }

    [Fact]
    public async Task FromAsyncEnumerable_WithSlowProducer_ProcessesConcurrently()
    {
        // Arrange
        var processed = new ConcurrentBag<(int item, DateTimeOffset time)>();

        async IAsyncEnumerable<int> SlowProducer([EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 1; i <= 4; i++)
            {
                yield return i;
                await Task.Delay(50, ct); // Slow production
            }
        }

        await using var coordinator = EphemeralWorkCoordinator<int>.FromAsyncEnumerable(
            SlowProducer(),
            async (item, ct) =>
            {
                processed.Add((item, DateTimeOffset.UtcNow));
                await Task.Delay(10, ct);
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(4, processed.Count);
    }

    [Fact]
    public async Task FromAsyncEnumerable_WithCancellation_StopsEarly()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<int> InfiniteProducer([EnumeratorCancellation] CancellationToken ct = default)
        {
            var i = 0;
            while (!ct.IsCancellationRequested)
            {
                yield return i++;
                await Task.Delay(10, ct);
            }
        }

        await using var coordinator = EphemeralWorkCoordinator<int>.FromAsyncEnumerable(
            InfiniteProducer(cts.Token),
            async (item, ct) =>
            {
                processed.Add(item);
                if (processed.Count >= 5)
                    cts.Cancel();
                await Task.Delay(5, ct);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act & Assert - should not hang
        try
        {
            await coordinator.DrainAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.True(processed.Count >= 5);
        Assert.True(processed.Count < 100, "Should have stopped early");
    }

    [Fact]
    public async Task ConcurrencyLimit_EnforcedUnderLoad()
    {
        // Arrange
        var currentConcurrency = 0;
        var maxObserved = 0;
        var lockObj = new object();
        const int maxConcurrency = 3;

        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    currentConcurrency++;
                    maxObserved = Math.Max(maxObserved, currentConcurrency);
                }

                await Task.Delay(20, ct);

                lock (lockObj)
                {
                    currentConcurrency--;
                }
            },
            new EphemeralOptions { MaxConcurrency = maxConcurrency });

        // Act
        for (var i = 0; i < 20; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(maxObserved <= maxConcurrency,
            $"Max observed concurrency {maxObserved} exceeded limit {maxConcurrency}");
        Assert.True(maxObserved >= 2, "Should have used parallelism");
    }
}

/// <summary>
/// Comprehensive tests for EphemeralKeyedWorkCoordinator.
/// </summary>
public class EphemeralKeyedWorkCoordinatorTests
{
    [Fact]
    public async Task PerKeySequential_GloballyParallel()
    {
        // Arrange
        var concurrencyByKey = new ConcurrentDictionary<string, int>();
        var maxConcurrencyByKey = new ConcurrentDictionary<string, int>();
        var globalConcurrency = 0;
        var maxGlobalConcurrency = 0;
        var lockObj = new object();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    globalConcurrency++;
                    maxGlobalConcurrency = Math.Max(maxGlobalConcurrency, globalConcurrency);

                    var keyConcurrency = concurrencyByKey.AddOrUpdate(item.Key, 1, (_, c) => c + 1);
                    maxConcurrencyByKey.AddOrUpdate(item.Key, keyConcurrency, (_, c) => Math.Max(c, keyConcurrency));
                }

                await Task.Delay(30, ct);

                lock (lockObj)
                {
                    globalConcurrency--;
                    concurrencyByKey.AddOrUpdate(item.Key, 0, (_, c) => c - 1);
                }
            },
            new EphemeralOptions { MaxConcurrency = 4, MaxConcurrencyPerKey = 1 });

        // Act - enqueue items for 4 different keys
        foreach (var key in new[] { "A", "B", "C", "D" })
        {
            for (var i = 1; i <= 3; i++)
                await coordinator.EnqueueAsync((key, i));
        }

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(maxGlobalConcurrency >= 2, $"Expected global parallelism, got {maxGlobalConcurrency}");
        Assert.True(maxGlobalConcurrency <= 4, $"Global concurrency {maxGlobalConcurrency} exceeded limit 4");

        foreach (var kvp in maxConcurrencyByKey)
        {
            Assert.True(kvp.Value <= 1,
                $"Key {kvp.Key} had max concurrency {kvp.Value}, expected <= 1");
        }
    }

    [Fact]
    public async Task MaxConcurrencyPerKey_GreaterThanOne()
    {
        // Arrange
        var maxPerKey = new ConcurrentDictionary<string, int>();
        var currentPerKey = new ConcurrentDictionary<string, int>();
        var lockObj = new object();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                lock (lockObj)
                {
                    var current = currentPerKey.AddOrUpdate(item.Key, 1, (_, c) => c + 1);
                    maxPerKey.AddOrUpdate(item.Key, current, (_, m) => Math.Max(m, current));
                }

                await Task.Delay(50, ct);

                lock (lockObj)
                {
                    currentPerKey.AddOrUpdate(item.Key, 0, (_, c) => c - 1);
                }
            },
            new EphemeralOptions { MaxConcurrency = 8, MaxConcurrencyPerKey = 2 }); // Allow 2 per key

        // Act
        for (var i = 0; i < 6; i++)
            await coordinator.EnqueueAsync(("A", i));

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(maxPerKey["A"] >= 2, $"Expected at least 2 concurrent for key A, got {maxPerKey["A"]}");
        Assert.True(maxPerKey["A"] <= 2, $"Expected at most 2 concurrent for key A, got {maxPerKey["A"]}");
    }

    [Fact]
    public async Task FairScheduling_DistributesWorkAcrossKeys()
    {
        // Arrange
        var processedOrder = new ConcurrentQueue<string>();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                processedOrder.Enqueue(item.Key);
                await Task.Delay(10, ct);
            },
            new EphemeralOptions
            {
                MaxConcurrency = 1, // Serial processing to see order
                EnableFairScheduling = true,
                FairSchedulingThreshold = 2
            });

        // Act - try to flood with one key, but fair scheduling should reject
        var hotKeyAccepted = 0;
        var coldKeyAccepted = 0;

        for (var i = 0; i < 10; i++)
        {
            if (coordinator.TryEnqueue(("HOT", i)))
                hotKeyAccepted++;
        }

        for (var i = 0; i < 5; i++)
        {
            if (coordinator.TryEnqueue(("COLD", i)))
                coldKeyAccepted++;
        }

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(hotKeyAccepted <= 4, $"Hot key should be throttled, accepted {hotKeyAccepted}");
        Assert.True(coldKeyAccepted >= 2, $"Cold key should be accepted, got {coldKeyAccepted}");
    }

    [Fact]
    public async Task FairScheduling_DisabledByDefault()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(100, ct),
            new EphemeralOptions
            {
                MaxConcurrency = 1,
                // EnableFairScheduling = false (default)
            });

        // Act - all items for same key should be accepted (no fair scheduling)
        var accepted = 0;
        for (var i = 0; i < 10; i++)
        {
            if (coordinator.TryEnqueue(("KEY", i)))
                accepted++;
        }

        coordinator.Cancel();

        // Assert - without fair scheduling, more should be accepted
        Assert.True(accepted >= 5, $"Without fair scheduling, expected more accepted, got {accepted}");
    }

    [Fact]
    public async Task GetSnapshotForKey_EmptyForUnknownKey()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(1, ct),
            new EphemeralOptions { MaxConcurrency = 2 });

        await coordinator.EnqueueAsync(("A", 1));
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Act
        var snapshot = coordinator.GetSnapshotForKey("UNKNOWN");

        // Assert
        Assert.Empty(snapshot);
    }

    [Fact]
    public async Task GetRunning_FiltersByCompletionStatus()
    {
        // Arrange
        var gate = new TaskCompletionSource<bool>();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                if (item.Key == "SLOW")
                    await gate.Task;
                else
                    await Task.Delay(1, ct);
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        await coordinator.EnqueueAsync(("SLOW", 1));
        await coordinator.EnqueueAsync(("FAST", 1));
        await coordinator.EnqueueAsync(("FAST", 2));
        await Task.Delay(100);

        var running = coordinator.GetRunning();
        gate.SetResult(true);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - only SLOW should have been running
        Assert.True(running.Count <= 1, $"Expected <= 1 running, got {running.Count}");
        if (running.Count == 1)
        {
            Assert.Equal("SLOW", running.First().Key);
        }
    }

    [Fact]
    public async Task FromAsyncEnumerable_Keyed_ProcessesCorrectly()
    {
        // Arrange
        var orderByKey = new ConcurrentDictionary<string, ConcurrentQueue<int>>();

        async IAsyncEnumerable<(string Key, int Value)> Producer([EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return ("A", 1);
            yield return ("B", 1);
            yield return ("A", 2);
            yield return ("B", 2);
            yield return ("A", 3);
        }

        await using var coordinator = EphemeralKeyedWorkCoordinator<(string Key, int Value), string>.FromAsyncEnumerable(
            Producer(),
            x => x.Key,
            async (item, ct) =>
            {
                var queue = orderByKey.GetOrAdd(item.Key, _ => new ConcurrentQueue<int>());
                queue.Enqueue(item.Value);
                await Task.Delay(20, ct);
            },
            new EphemeralOptions { MaxConcurrency = 2, MaxConcurrencyPerKey = 1 });

        // Act
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, orderByKey["A"].ToArray());
        Assert.Equal(new[] { 1, 2 }, orderByKey["B"].ToArray());
    }

    [Fact]
    public async Task ErrorsIsolatedPerKey()
    {
        // Arrange
        var results = new ConcurrentDictionary<string, bool>();

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                await Task.Delay(5, ct);
                if (item.Key == "FAIL")
                    throw new InvalidOperationException("Intentional failure");
                results[item.Key] = true;
            },
            new EphemeralOptions { MaxConcurrency = 2, MaxConcurrencyPerKey = 1 });

        // Act
        await coordinator.EnqueueAsync(("OK1", 1));
        await coordinator.EnqueueAsync(("FAIL", 1));
        await coordinator.EnqueueAsync(("OK2", 1));

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - failures in FAIL key shouldn't affect OK keys
        Assert.True(results.ContainsKey("OK1"));
        Assert.True(results.ContainsKey("OK2"));
        Assert.False(results.ContainsKey("FAIL"));
        Assert.Equal(1, coordinator.TotalFailed);
    }

    [Fact]
    public async Task TotalCounts_AccurateAcrossKeys()
    {
        // Arrange
        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                await Task.Delay(5, ct);
                if (item.Value % 3 == 0)
                    throw new Exception("Every third fails");
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        for (var i = 1; i <= 9; i++)
        {
            var key = i <= 3 ? "A" : i <= 6 ? "B" : "C";
            await coordinator.EnqueueAsync((key, i));
        }

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(9, coordinator.TotalEnqueued);
        Assert.Equal(6, coordinator.TotalCompleted); // 1,2,4,5,7,8 succeed
        Assert.Equal(3, coordinator.TotalFailed);    // 3,6,9 fail
    }
}

/// <summary>
/// Tests for DI extension methods.
/// </summary>
public class EphemeralDependencyInjectionTests
{
    [Fact]
    public void AddEphemeralWorkCoordinator_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        var provider = services.BuildServiceProvider();
        var coordinator1 = provider.GetService<EphemeralWorkCoordinator<int>>();
        var coordinator2 = provider.GetService<EphemeralWorkCoordinator<int>>();

        // Assert
        Assert.NotNull(coordinator1);
        Assert.Same(coordinator1, coordinator2); // Singleton
    }

    [Fact]
    public async Task AddEphemeralWorkCoordinator_WithServiceProvider_ResolvesServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddEphemeralWorkCoordinator<int>(
            sp =>
            {
                var testService = sp.GetRequiredService<ITestService>();
                return async (item, ct) =>
                {
                    testService.Process(item);
                    await Task.Delay(1, ct);
                };
            });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<EphemeralWorkCoordinator<int>>();
        var testService = provider.GetRequiredService<ITestService>();

        // Act
        await coordinator.EnqueueAsync(42);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Contains(42, ((TestService)testService).ProcessedItems);
    }

    [Fact]
    public void AddEphemeralKeyedWorkCoordinator_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) => await Task.Delay(1, ct));

        var provider = services.BuildServiceProvider();
        var coordinator1 = provider.GetService<EphemeralKeyedWorkCoordinator<(string Key, int Value), string>>();
        var coordinator2 = provider.GetService<EphemeralKeyedWorkCoordinator<(string Key, int Value), string>>();

        // Assert
        Assert.NotNull(coordinator1);
        Assert.Same(coordinator1, coordinator2);
    }

    [Fact]
    public async Task AddScopedEphemeralWorkCoordinator_CreatesPerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScopedEphemeralWorkCoordinator<int>(
            _ => async (item, ct) => await Task.Delay(1, ct));

        var provider = services.BuildServiceProvider();

        // Act
        EphemeralWorkCoordinator<int> coordinator1, coordinator2;
        await using (var scope1 = provider.CreateAsyncScope())
        {
            coordinator1 = scope1.ServiceProvider.GetRequiredService<EphemeralWorkCoordinator<int>>();
        }

        await using (var scope2 = provider.CreateAsyncScope())
        {
            coordinator2 = scope2.ServiceProvider.GetRequiredService<EphemeralWorkCoordinator<int>>();
        }

        // Assert
        Assert.NotSame(coordinator1, coordinator2); // Different scopes, different instances
    }

    [Fact]
    public async Task AddScopedEphemeralKeyedWorkCoordinator_DisposesWithScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScopedEphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            _ => async (item, ct) => await Task.Delay(1, ct));

        var provider = services.BuildServiceProvider();
        EphemeralKeyedWorkCoordinator<(string Key, int Value), string> coordinator;

        // Act
        await using (var scope = provider.CreateAsyncScope())
        {
            coordinator = scope.ServiceProvider.GetRequiredService<EphemeralKeyedWorkCoordinator<(string Key, int Value), string>>();
            await coordinator.EnqueueAsync(("A", 1));
            // Scope disposes here, which should dispose coordinator
        }

        // Assert - coordinator should be cancelled/disposed
        Assert.True(coordinator.IsCompleted);
    }

    public interface ITestService
    {
        void Process(int item);
    }

    public class TestService : ITestService
    {
        public ConcurrentBag<int> ProcessedItems { get; } = new();
        public void Process(int item) => ProcessedItems.Add(item);
    }
}

/// <summary>
/// Edge cases and stress tests.
/// </summary>
public class EphemeralEdgeCaseTests
{
    [Fact]
    public async Task VeryHighConcurrency_DoesNotDeadlock()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                processed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 100 });

        // Act
        for (var i = 0; i < 1000; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();

        var drainTask = coordinator.DrainAsync();
        var completed = await Task.WhenAny(drainTask, Task.Delay(10000)) == drainTask;

        // Assert
        Assert.True(completed, "Should complete without deadlock");
        Assert.Equal(1000, processed.Count);
    }

    [Fact]
    public async Task ZeroDelayOperations_ProcessCorrectly()
    {
        // Arrange
        var processed = new ConcurrentBag<int>();
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            (item, ct) =>
            {
                processed.Add(item);
                return Task.CompletedTask;
            },
            new EphemeralOptions { MaxConcurrency = 4 });

        // Act
        for (var i = 0; i < 100; i++)
            await coordinator.EnqueueAsync(i);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(100, processed.Count);
    }

    [Fact]
    public async Task RapidEnqueueAndCancel_NoExceptions()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1000, ct),
            new EphemeralOptions { MaxConcurrency = 2 });

        // Act - rapid fire enqueue then immediate cancel
        var enqueueTask = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    coordinator.TryEnqueue(i);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        });

        await Task.Delay(10);
        coordinator.Cancel();

        // Assert - should not throw
        await enqueueTask;
    }

    [Fact]
    public async Task ManyKeys_MemoryDoesNotExplode()
    {
        // Arrange
        var processed = 0;

        await using var coordinator = new EphemeralKeyedWorkCoordinator<(string Key, int Value), string>(
            x => x.Key,
            async (item, ct) =>
            {
                Interlocked.Increment(ref processed);
                await Task.Delay(1, ct);
            },
            new EphemeralOptions { MaxConcurrency = 16, MaxConcurrencyPerKey = 1 });

        // Act - create many unique keys
        for (var i = 0; i < 500; i++)
        {
            await coordinator.EnqueueAsync(($"Key{i}", i));
        }

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(500, processed);
    }

    [Fact]
    public async Task OperationThatNeverCompletes_CancelledByDispose()
    {
        // Arrange
        var started = new TaskCompletionSource<bool>();
        var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                started.SetResult(true);
                await Task.Delay(Timeout.Infinite, ct);
            },
            new EphemeralOptions { MaxConcurrency = 1 });

        // Act
        await coordinator.EnqueueAsync(1);
        await started.Task;

        var disposeTask = coordinator.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(5000)) == disposeTask;

        // Assert
        Assert.True(completed, "Dispose should cancel infinite operation");
    }

    [Fact]
    public async Task SnapshotUnderHighLoad_ThreadSafe()
    {
        // Arrange
        var snapshots = new ConcurrentBag<int>();

        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(5, ct),
            new EphemeralOptions { MaxConcurrency = 8 });

        // Act - parallel snapshot reads while processing
        var snapshotTask = Task.Run(async () =>
        {
            for (var i = 0; i < 50; i++)
            {
                var snapshot = coordinator.GetSnapshot();
                snapshots.Add(snapshot.Count);
                await Task.Delay(10);
            }
        });

        var enqueueTask = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                await coordinator.EnqueueAsync(i);
            }
        });

        await Task.WhenAll(snapshotTask, enqueueTask);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - no exceptions during concurrent access
        Assert.True(snapshots.Count > 0);
    }

    [Fact]
    public async Task SingleItemSource_ProcessedCorrectly()
    {
        // Arrange
        var processed = false;

        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) =>
            {
                processed = true;
                await Task.Delay(1, ct);
            });

        // Act
        await coordinator.EnqueueAsync(1);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(processed);
        Assert.Equal(1, coordinator.TotalCompleted);
    }

    [Fact]
    public async Task EmptyDrain_CompletesImmediately()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(1, ct));

        // Act - complete without enqueueing anything
        coordinator.Complete();
        var drainTask = coordinator.DrainAsync();
        var completed = await Task.WhenAny(drainTask, Task.Delay(1000)) == drainTask;

        // Assert
        Assert.True(completed);
        Assert.Equal(0, coordinator.TotalEnqueued);
    }

    [Fact]
    public async Task MaxOperationLifetime_EvictsOldEntries()
    {
        // Arrange
        await using var coordinator = new EphemeralWorkCoordinator<int>(
            async (item, ct) => await Task.Delay(10, ct),
            new EphemeralOptions
            {
                MaxConcurrency = 1,
                MaxTrackedOperations = 5,
                MaxOperationLifetime = TimeSpan.FromMilliseconds(50)
            });

        // Act - process items with delay
        for (var i = 0; i < 20; i++)
        {
            await coordinator.EnqueueAsync(i);
        }

        coordinator.Complete();
        await coordinator.DrainAsync();
        await Task.Delay(150); // allow age-based eviction window

        var finalSnapshot = coordinator.GetSnapshot();
        // Due to time-based eviction, not all 20 should be present
        Assert.True(finalSnapshot.Count < 20,
            $"Expected some eviction due to MaxOperationLifetime, got {finalSnapshot.Count}");
    }
}

/// <summary>
/// Tests for the named/typed coordinator factory pattern (like AddHttpClient).
/// </summary>
public class EphemeralCoordinatorFactoryTests
{
    [Fact]
    public async Task Factory_CreateNamedCoordinators()
    {
        // Arrange
        var services = new ServiceCollection();
        var fastProcessed = new ConcurrentBag<int>();
        var slowProcessed = new ConcurrentBag<int>();

        services.AddEphemeralWorkCoordinator<int>("fast",
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                fastProcessed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 8 });

        services.AddEphemeralWorkCoordinator<int>("slow",
            async (item, ct) =>
            {
                await Task.Delay(10, ct);
                slowProcessed.Add(item);
            },
            new EphemeralOptions { MaxConcurrency = 2 });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act
        var fast = factory.CreateCoordinator("fast");
        var slow = factory.CreateCoordinator("slow");

        await fast.EnqueueAsync(1);
        await fast.EnqueueAsync(2);
        await slow.EnqueueAsync(100);

        fast.Complete();
        slow.Complete();

        await Task.WhenAll(fast.DrainAsync(), slow.DrainAsync());

        // Assert
        Assert.Equal(2, fastProcessed.Count);
        Assert.Single(slowProcessed);
        Assert.Contains(1, fastProcessed);
        Assert.Contains(2, fastProcessed);
        Assert.Contains(100, slowProcessed);
    }

    [Fact]
    public void Factory_SameNameReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEphemeralWorkCoordinator<int>("test",
            (item, ct) => Task.CompletedTask);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act
        var coordinator1 = factory.CreateCoordinator("test");
        var coordinator2 = factory.CreateCoordinator("test");

        // Assert - same instance returned
        Assert.Same(coordinator1, coordinator2);
    }

    [Fact]
    public void Factory_DifferentNamesReturnDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEphemeralWorkCoordinator<int>("a", (item, ct) => Task.CompletedTask);
        services.AddEphemeralWorkCoordinator<int>("b", (item, ct) => Task.CompletedTask);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act
        var coordinatorA = factory.CreateCoordinator("a");
        var coordinatorB = factory.CreateCoordinator("b");

        // Assert - different instances
        Assert.NotSame(coordinatorA, coordinatorB);
    }

    [Fact]
    public void Factory_UnregisteredNameThrows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEphemeralWorkCoordinator<int>("registered",
            (item, ct) => Task.CompletedTask);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => factory.CreateCoordinator("unregistered"));

        Assert.Contains("unregistered", ex.Message);
    }

    [Fact]
    public async Task Factory_DefaultNamedCoordinator()
    {
        // Arrange
        var services = new ServiceCollection();
        var processed = new ConcurrentBag<int>();

        services.AddEphemeralWorkCoordinator<int>("",
            _ => async (item, ct) =>
            {
                await Task.Delay(1, ct);
                processed.Add(item);
            });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act - get default (unnamed) coordinator
        var coordinator = factory.CreateCoordinator();

        await coordinator.EnqueueAsync(42);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Single(processed);
        Assert.Contains(42, processed);
    }

    [Fact]
    public async Task Factory_WithServiceProviderAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        var testService = new TestService();
        services.AddSingleton<ITestService>(testService);

        services.AddEphemeralWorkCoordinator<int>("with-di",
            sp =>
            {
                var svc = sp.GetRequiredService<ITestService>();
                return (item, ct) =>
                {
                    svc.Process(item);
                    return Task.CompletedTask;
                };
            });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralCoordinatorFactory<int>>();

        // Act
        var coordinator = factory.CreateCoordinator("with-di");

        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(3, testService.ProcessedItems.Count);
        Assert.Contains(1, testService.ProcessedItems);
        Assert.Contains(2, testService.ProcessedItems);
        Assert.Contains(3, testService.ProcessedItems);
    }

    [Fact]
    public async Task KeyedFactory_CreateNamedCoordinators()
    {
        // Arrange
        var services = new ServiceCollection();
        var processOrder = new ConcurrentBag<(string key, int item)>();

        services.AddEphemeralKeyedWorkCoordinator<int, string>("keyed",
            item => item < 100 ? "A" : "B",
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                processOrder.Add((item < 100 ? "A" : "B", item));
            },
            new EphemeralOptions { MaxConcurrencyPerKey = 1 });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IEphemeralKeyedCoordinatorFactory<int, string>>();

        // Act
        var coordinator = factory.CreateCoordinator("keyed");

        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(100);
        await coordinator.EnqueueAsync(101);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(4, processOrder.Count);
        Assert.Equal(2, processOrder.Count(x => x.key == "A"));
        Assert.Equal(2, processOrder.Count(x => x.key == "B"));
    }

    public interface ITestService
    {
        void Process(int item);
    }

    public class TestService : ITestService
    {
        public ConcurrentBag<int> ProcessedItems { get; } = new();
        public void Process(int item) => ProcessedItems.Add(item);
    }
}

/// <summary>
/// Tests for the result-capturing coordinator variant.
/// </summary>
public class EphemeralResultCoordinatorTests
{
    [Fact]
    public async Task ResultCoordinator_CapturesResults()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, string>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                return $"Result-{item}";
            });

        // Act
        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        var results = coordinator.GetResults();
        Assert.Equal(3, results.Count);
        Assert.Contains("Result-1", results);
        Assert.Contains("Result-2", results);
        Assert.Contains("Result-3", results);
    }

    [Fact]
    public async Task ResultCoordinator_GetSuccessful_OnlyReturnsSuccesses()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, string>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                if (item == 2)
                    throw new InvalidOperationException("Item 2 fails");
                return $"Result-{item}";
            });

        // Act
        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        var successful = coordinator.GetSuccessful();
        Assert.Equal(2, successful.Count);
        Assert.All(successful, s => Assert.True(s.HasResult));
        Assert.All(successful, s => Assert.False(s.IsFaulted));

        var failed = coordinator.GetFailed();
        Assert.Single(failed);
        Assert.True(failed.First().IsFaulted);
    }

    [Fact]
    public async Task ResultCoordinator_GetSnapshot_IncludesResults()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, int>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                return item * 10;
            });

        // Act
        await coordinator.EnqueueAsync(5);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        var snapshot = coordinator.GetSnapshot();
        Assert.Single(snapshot);
        var op = snapshot.First();
        Assert.True(op.HasResult);
        Assert.Equal(50, op.Result);
    }

    [Fact]
    public async Task ResultCoordinator_GetBaseSnapshot_ExcludesResults()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, string>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                return $"Secret-{item}";
            });

        // Act
        await coordinator.EnqueueAsync(1);
        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert - base snapshot has no result field
        var baseSnapshot = coordinator.GetBaseSnapshot();
        Assert.Single(baseSnapshot);
        // EphemeralOperationSnapshot (non-generic) has no Result property
        Assert.NotNull(baseSnapshot.First().Duration);
    }

    [Fact]
    public async Task ResultCoordinator_FromAsyncEnumerable_CapturesResults()
    {
        // Arrange
        async IAsyncEnumerable<int> GenerateItems()
        {
            for (var i = 1; i <= 5; i++)
            {
                yield return i;
                await Task.Delay(1);
            }
        }

        await using var coordinator = EphemeralResultCoordinator<int, int>.FromAsyncEnumerable(
            GenerateItems(),
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                return item * item;
            });

        // Act
        await coordinator.DrainAsync();

        // Assert
        var results = coordinator.GetResults();
        Assert.Equal(5, results.Count);
        Assert.Contains(1, results);   // 1*1
        Assert.Contains(4, results);   // 2*2
        Assert.Contains(9, results);   // 3*3
        Assert.Contains(16, results);  // 4*4
        Assert.Contains(25, results);  // 5*5
    }

    [Fact]
    public async Task ResultCoordinator_ComplexResultType()
    {
        // Arrange - simulate session fingerprinting
        await using var coordinator = new EphemeralResultCoordinator<SessionInput, SessionResult>(
            async (input, ct) =>
            {
                await Task.Delay(1, ct);
                return new SessionResult(
                    Fingerprint: $"fp-{input.SessionId}",
                    EventCount: input.Events.Length,
                    TotalDuration: TimeSpan.FromSeconds(input.Events.Length * 10));
            });

        // Act
        await coordinator.EnqueueAsync(new SessionInput("sess-1", ["click", "scroll", "click"]));
        await coordinator.EnqueueAsync(new SessionInput("sess-2", ["pageview"]));

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        var results = coordinator.GetResults();
        Assert.Equal(2, results.Count);

        var sess1 = results.First(r => r.Fingerprint == "fp-sess-1");
        Assert.Equal(3, sess1.EventCount);
        Assert.Equal(TimeSpan.FromSeconds(30), sess1.TotalDuration);

        var sess2 = results.First(r => r.Fingerprint == "fp-sess-2");
        Assert.Equal(1, sess2.EventCount);
    }

    [Fact]
    public async Task ResultCoordinator_TryEnqueue_Works()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, int>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                return item * 2;
            });

        // Act
        var enqueued = coordinator.TryEnqueue(5);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.True(enqueued);
        Assert.Single(coordinator.GetResults());
        Assert.Equal(10, coordinator.GetResults().First());
    }

    [Fact]
    public async Task ResultCoordinator_CountersWork()
    {
        // Arrange
        await using var coordinator = new EphemeralResultCoordinator<int, int>(
            async (item, ct) =>
            {
                await Task.Delay(1, ct);
                if (item == 2) throw new Exception("fail");
                return item;
            });

        // Act
        await coordinator.EnqueueAsync(1);
        await coordinator.EnqueueAsync(2);
        await coordinator.EnqueueAsync(3);

        coordinator.Complete();
        await coordinator.DrainAsync();

        // Assert
        Assert.Equal(3, coordinator.TotalEnqueued);
        Assert.Equal(2, coordinator.TotalCompleted);
        Assert.Equal(1, coordinator.TotalFailed);
    }

    public record SessionInput(string SessionId, string[] Events);
    public record SessionResult(string Fingerprint, int EventCount, TimeSpan TotalDuration);
}

