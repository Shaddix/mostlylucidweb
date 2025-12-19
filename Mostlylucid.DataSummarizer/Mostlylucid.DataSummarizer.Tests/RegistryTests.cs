using Mostlylucid.DataSummarizer.Services;
using Xunit;

namespace Mostlylucid.DataSummarizer.Tests;

public class RegistryTests
{
    [Fact]
    public async Task IngestAndAskRegistry_NoLlm_ReturnsContext()
    {
        var tmpDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ds-reg-{Guid.NewGuid():N}"));
        var dbPath = Path.Combine(tmpDir.FullName, "registry.duckdb");

        var file1 = WriteCsv(tmpDir.FullName, "f1.csv", "A,B\n1,foo\n2,bar\n3,baz\n");
        var file2 = WriteCsv(tmpDir.FullName, "f2.csv", "X,Y\n10,100\n20,200\n30,300\n");

        using var svc = new DataSummarizerService(verbose: false, ollamaModel: null, vectorStorePath: dbPath);
        await svc.IngestAsync(new[] { file1, file2 }, maxLlmInsights: 0);

        var answer = await svc.AskRegistryAsync("quick overview", topK: 4);

        Assert.NotNull(answer);
        Assert.False(string.IsNullOrWhiteSpace(answer!.Description));
    }

    private static string WriteCsv(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
