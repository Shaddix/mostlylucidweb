using System.Collections.Generic;
using System.IO;
using Mostlylucid.DataSummarizer.Models;

namespace Mostlylucid.DataSummarizer.Configuration;

public class DataSummarizerSettings
{
    public string Name => "DataSummarizer";
    public ProfileOptions ProfileOptions { get; set; } = new();
    public MarkdownReportSettings MarkdownReport { get; set; } = new();
}

public class MarkdownReportSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseLlm { get; set; } = true;
    public string? OutputDirectory { get; set; }
        = Path.Combine(AppContext.BaseDirectory, "reports");
    public bool IncludeFocusQuestions { get; set; } = true;
    public List<string> FocusQuestions { get; set; } = new();
}
