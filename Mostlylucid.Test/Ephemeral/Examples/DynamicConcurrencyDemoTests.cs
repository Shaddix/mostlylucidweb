using Mostlylucid.Helpers.Ephemeral;
using Mostlylucid.Helpers.Ephemeral.Examples;
using Xunit;

namespace Mostlylucid.Test.Ephemeral.Examples;

public class DynamicConcurrencyDemoTests
{
    [Fact]
    public async Task Scales_Up_And_Down_On_Signals()
    {
        var sink = new SignalSink(maxCapacity: 16, maxAge: TimeSpan.FromSeconds(5));
        var scaledUp = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scaledDown = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var demo = new DynamicConcurrencyDemo<int>(
            async (item, ct) => await Task.Delay(5, ct),
            sink,
            minConcurrency: 1,
            maxConcurrency: 4,
            scaleUpPattern: "load.high",
            scaleDownPattern: "load.low",
            signalWindow: TimeSpan.FromSeconds(5),
            minAdjustInterval: TimeSpan.FromMilliseconds(100),
            sampleInterval: TimeSpan.FromMilliseconds(50),
            onAdjusted: (prev, next) =>
            {
                if (next > prev) scaledUp.TrySetResult(next);
                if (next < prev) scaledDown.TrySetResult(next);
            }); // Tight sampling for deterministic tests

        await demo.EnqueueAsync(1); // force creation

        sink.Raise(new SignalEvent("load.high", 1, null, DateTimeOffset.UtcNow));
        var upValue = await scaledUp.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Wait for hysteresis before next adjustment
        await Task.Delay(150);

        sink.Raise(new SignalEvent("load.low", 2, null, DateTimeOffset.UtcNow));
        var downValue = await scaledDown.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(upValue > 1, $"Expected scale up > 1, saw {upValue}");
        Assert.True(downValue <= upValue, $"Expected scale down to stay <= {upValue}, saw {downValue}");

        await demo.DrainAsync();
    }
}
