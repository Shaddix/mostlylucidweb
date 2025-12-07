using Mostlylucid.Helpers.Ephemeral;
using Xunit;

namespace Mostlylucid.Test.Ephemeral;

public class PriorityWorkCoordinatorTests
{
    [Fact]
    public async Task Higher_Priority_Drains_First()
    {
        var order = new List<string>();

        await using var coordinator = new PriorityWorkCoordinator<string>(
            new PriorityWorkCoordinatorOptions<string>(
                async (item, ct) =>
                {
                    await Task.Delay(5, ct);
                    lock (order) { order.Add(item); }
                },
                Lanes: new[] { new PriorityLane("high"), new PriorityLane("normal") },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 1 }));

        await coordinator.EnqueueAsync("n1", "normal");
        await coordinator.EnqueueAsync("n2", "normal");
        await coordinator.EnqueueAsync("h1", "high");
        await coordinator.EnqueueAsync("h2", "high");

        await coordinator.DrainAsync();

        Assert.Equal(["h1", "h2", "n1", "n2"], order);
    }

    [Fact]
    public async Task Lane_Depth_Is_Enforced()
    {
        await using var coordinator = new PriorityWorkCoordinator<int>(
            new PriorityWorkCoordinatorOptions<int>(
                async (item, ct) => await Task.Delay(1, ct),
                Lanes: new[] { new PriorityLane("priority", MaxDepth: 2), new PriorityLane("normal") },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 2 }));

        Assert.True(await coordinator.EnqueueAsync(1, "priority"));
        Assert.True(await coordinator.EnqueueAsync(2, "priority"));
        Assert.False(await coordinator.EnqueueAsync(3, "priority")); // exceeds depth

        await coordinator.DrainAsync();
    }

    [Fact]
    public async Task Lane_Cancel_Signal_Drops_Items()
    {
        var sink = new SignalSink();
        var processed = 0;

        await using var coordinator = new PriorityWorkCoordinator<int>(
            new PriorityWorkCoordinatorOptions<int>(
                async (item, ct) =>
                {
                    Interlocked.Increment(ref processed);
                    await Task.Delay(1, ct);
                },
                Lanes: new[]
                {
                    new PriorityLane("blocked", CancelOnSignals: new HashSet<string> { "stop" }),
                    new PriorityLane("open")
                },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 2, Signals = sink }));

        sink.Raise("stop");
        await coordinator.EnqueueAsync(1, "blocked");
        await coordinator.EnqueueAsync(2, "open");

        await coordinator.DrainAsync();

        Assert.Equal(1, processed);
    }

    [Fact]
    public async Task Processes_Thousand_Items_All_Lanes()
    {
        var processed = 0;

        await using var coordinator = new PriorityWorkCoordinator<int>(
            new PriorityWorkCoordinatorOptions<int>(
                async (item, ct) =>
                {
                    Interlocked.Increment(ref processed);
                    await Task.Delay(1, ct);
                },
                Lanes: new[]
                {
                    new PriorityLane("critical"),
                    new PriorityLane("standard")
                },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 16 }));

        for (var i = 0; i < 500; i++)
        {
            await coordinator.EnqueueAsync(i, "critical");
            await coordinator.EnqueueAsync(i, "standard");
        }

        await coordinator.DrainAsync();

        Assert.Equal(1000, processed);
    }
}
