using Mostlylucid.Helpers.Ephemeral.Examples;
using Xunit;

namespace Mostlylucid.Test.Ephemeral.Examples;

public class SignalCoordinatedReadsTests
{
    [Fact]
    public async Task Reads_Wait_During_Update_And_Resume()
    {
        var result = await SignalCoordinatedReads.RunAsync(readCount: 12, updateCount: 1);

        Assert.Equal(12, result.ReadsCompleted);
        Assert.Equal(1, result.UpdatesCompleted);
        Assert.Contains(result.Signals, s => s == "update.in-progress");
        Assert.Contains(result.Signals, s => s == "update.done");
    }
}
