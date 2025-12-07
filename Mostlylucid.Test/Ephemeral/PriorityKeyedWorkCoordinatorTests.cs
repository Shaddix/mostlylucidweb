using Mostlylucid.Helpers.Ephemeral;
using Xunit;

namespace Mostlylucid.Test.Ephemeral;

public class PriorityKeyedWorkCoordinatorTests
{
    [Fact]
    public async Task Keyed_Lanes_Respect_Priority()
    {
        var order = new List<(string Lane, int Item)>();

        await using var coordinator = new PriorityKeyedWorkCoordinator<(string Lane, int Item), string>(
            new PriorityKeyedWorkCoordinatorOptions<(string Lane, int Item), string>(
                item => item.Lane,
                async (item, ct) =>
                {
                    await Task.Delay(5, ct);
                    lock (order) { order.Add(item); }
                },
                Lanes: new[] { new PriorityLane("fast"), new PriorityLane("slow") },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 1, MaxConcurrencyPerKey = 1 }));

        await coordinator.EnqueueAsync(("slow", 1), "slow");
        await coordinator.EnqueueAsync(("slow", 2), "slow");
        await coordinator.EnqueueAsync(("fast", 1), "fast");
        await coordinator.EnqueueAsync(("fast", 2), "fast");

        await coordinator.DrainAsync();

        Assert.Collection(order,
            item => Assert.Equal("fast", item.Lane),
            item => Assert.Equal("fast", item.Lane),
            item => Assert.Equal("slow", item.Lane),
            item => Assert.Equal("slow", item.Lane));
    }

    [Fact]
    public async Task Keyed_Lane_Depth_Enforced()
    {
        await using var coordinator = new PriorityKeyedWorkCoordinator<(string Lane, int Item), string>(
            new PriorityKeyedWorkCoordinatorOptions<(string Lane, int Item), string>(
                item => item.Lane,
                async (item, ct) => await Task.Delay(1, ct),
                Lanes: new[] { new PriorityLane("high", MaxDepth: 1), new PriorityLane("low") },
                EphemeralOptions: new EphemeralOptions { MaxConcurrency = 2, MaxConcurrencyPerKey = 1 }));

        Assert.True(await coordinator.EnqueueAsync(("high", 1), "high"));
        Assert.False(await coordinator.EnqueueAsync(("high", 2), "high"));

        await coordinator.DrainAsync();
    }
}
