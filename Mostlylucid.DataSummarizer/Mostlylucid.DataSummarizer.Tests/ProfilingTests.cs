using System.Text;
using Mostlylucid.DataSummarizer.Services;
using Xunit;

namespace Mostlylucid.DataSummarizer.Tests;

public class ProfilingTests
{
    [Fact]
    public async Task ProfilesBasicCsv()
    {
        // Use more rows so columns are classified as Numeric, not Id
        var csv = "A,B\n1,2\n3,4\n5,6\n1,2\n3,4\n5,6\n1,2\n3,4\n5,6\n1,2\n";
        var path = WriteTempCsv(csv);
        var svc = new DataSummarizerService(verbose: false, ollamaModel: null, vectorStorePath: null);

        var report = await svc.SummarizeAsync(path, useLlm: false);

        Assert.Equal(10, report.Profile.RowCount);
        Assert.Equal(2, report.Profile.ColumnCount);
        Assert.Contains(report.Profile.Columns, c => c.Name == "A" && c.InferredType == Models.ColumnType.Numeric);
        Assert.Contains(report.Profile.Columns, c => c.Name == "B" && c.InferredType == Models.ColumnType.Numeric);
    }

    [Fact]
    public async Task HighUniqueNumericNotFlaggedAsId()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Salary");
        for (int i = 1; i <= 200; i++)
        {
            sb.AppendLine($"{i},{100000 + i}");
        }
        var path = WriteTempCsv(sb.ToString());
        var svc = new DataSummarizerService(verbose: false, ollamaModel: null, vectorStorePath: null);

        var report = await svc.SummarizeAsync(path, useLlm: false);
        var alerts = report.Profile.Alerts;
        Assert.DoesNotContain(alerts, a => a.Column == "Salary" && a.Type == Models.AlertType.HighCardinality);
    }

    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ds-test-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }
}
