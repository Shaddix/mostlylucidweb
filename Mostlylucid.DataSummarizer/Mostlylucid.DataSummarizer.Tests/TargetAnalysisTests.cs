using Mostlylucid.DataSummarizer.Models;
using Mostlylucid.DataSummarizer.Services;
using Xunit;

namespace Mostlylucid.DataSummarizer.Tests;

public class TargetAnalysisTests
{
    [Fact]
    public async Task TargetAwareProfiling_Completes_OnDecimalColumns()
    {
        // Need at least 5 rows per target class for effect analysis
        var csv = @"CreditScore,Exited
650.5,1
700.1,0
620.9,1
680.0,0
615.2,1
710.3,0
625.8,1
690.5,0
630.1,1
705.7,0
";
        var path = WriteTempCsv(csv);
        var svc = new DataSummarizerService(verbose: false, ollamaModel: null, vectorStorePath: null, profileOptions: new ProfileOptions { TargetColumn = "Exited" });

        var report = await svc.SummarizeAsync(path, useLlm: false);

        Assert.NotNull(report.Profile.Target);
        Assert.True(report.Profile.Target!.FeatureEffects.Count > 0, $"Expected feature effects but got {report.Profile.Target?.FeatureEffects?.Count ?? 0}");
    }

    private static string WriteTempCsv(string content)
    {
        var file = Path.Combine(Path.GetTempPath(), $"target_test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(file, content);
        return file;
    }
}
