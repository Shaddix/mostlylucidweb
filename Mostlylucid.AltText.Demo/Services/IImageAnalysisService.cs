namespace Mostlylucid.AltText.Demo.Services;

public interface IImageAnalysisService
{
    Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION");
    Task<string> ExtractTextAsync(Stream imageStream);
    Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream);
}
