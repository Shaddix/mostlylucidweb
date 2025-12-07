using Mostlylucid.Helpers.Ephemeral.Examples;
using Xunit;

namespace Mostlylucid.Test.Ephemeral.Examples;

public class KeyedPriorityFanOutTests
{
    [Fact]
    public async Task Priority_Drains_Before_Normal()
    {
        var result = new List<string>();
        var gate = new SemaphoreSlim(0, 1);

        await using var fanout = new KeyedPriorityFanOut<string, (string Key, string Value)>(
            keySelector: x => x.Key,
            body: async (item, ct) =>
            {
                result.Add(item.Value);
                await Task.Delay(10, ct);
                if (result.Count == 1) gate.Release(); // first seen should be priority
            },
            maxConcurrency: 2,
            perKeyConcurrency: 1);

        await fanout.EnqueueAsync(("user", "normal-1"));
        var accepted = await fanout.EnqueuePriorityAsync(("user", "priority-1"));
        Assert.True(accepted);
        await fanout.EnqueueAsync(("user", "normal-2"));

        // Wait until first completion to assert priority went first.
        await gate.WaitAsync(TimeSpan.FromSeconds(2));

        await fanout.DrainAsync();

        Assert.Equal("priority-1", result[0]);
        Assert.Contains(result, x => x == "normal-1");
        Assert.Contains(result, x => x == "normal-2");
    }

    [Fact]
    public async Task Respects_MaxPriorityDepth()
    {
        await using var fanout = new KeyedPriorityFanOut<string, (string Key, int Value)>(
            keySelector: x => x.Key,
            body: (_, _) => Task.CompletedTask,
            maxConcurrency: 2,
            perKeyConcurrency: 1,
            maxPriorityDepth: 1);

        var ok = await fanout.EnqueuePriorityAsync(("user", 1));
        var rejected = await fanout.EnqueuePriorityAsync(("user", 2)); // exceeds cap

        Assert.True(ok);
        Assert.False(rejected);

        await fanout.DrainAsync();
    }
}
