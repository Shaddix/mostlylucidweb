using Mostlylucid.DataSummarizer.Services;
using Xunit;

namespace Mostlylucid.DataSummarizer.Tests;

public class TargetAnalysisTests
{
    [Fact]
    public async Task TargetAwareProfiling_Completes_OnDecimalColumns()
    {
        var csv = "CreditScore,Exited\n650.5,1\n700.1,0\n620.9,1\n680.0,0\n";
        var path = WriteTempCsv(csv);
        var svc = new DataSummarizerService(verbose: false, ollamaModel: null, vectorStorePath: null, profileOptions: new Models.ProfileOptions { TargetColumn = "Exited" });

        var report = await svc.SummarizeAsync(path, useLlm: false);

        Assert.NotNull(report.Profile.Target);
        Assert.True(report.Profile.Target!.FeatureEffects.Count > 0);
    }

    private static string WriteTempCsv(string content)
    {
        var file = Path.Combine(Path.GetTempPath(), $"target_test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(file, content);
        return file;
    }
}
