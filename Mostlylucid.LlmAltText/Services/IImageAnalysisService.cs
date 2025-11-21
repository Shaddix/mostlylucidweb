namespace Mostlylucid.LlmAltText.Services;

/// <summary>
/// Service for AI-powered image analysis including alt text generation and OCR
/// </summary>
public interface IImageAnalysisService
{
    /// <summary>
    /// Generate descriptive alt text for an image
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <param name="taskType">Vision task type: CAPTION, DETAILED_CAPTION, or MORE_DETAILED_CAPTION</param>
    /// <returns>Generated alt text description</returns>
    Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION");

    /// <summary>
    /// Extract text content from an image using OCR
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(Stream imageStream);

    /// <summary>
    /// Perform complete image analysis: generate alt text and extract any text
    /// </summary>
    /// <param name="imageStream">Image data stream (will not be disposed by this method)</param>
    /// <returns>Tuple containing both alt text and extracted text</returns>
    Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream);

    /// <summary>
    /// Check if the service is initialized and ready to process images
    /// </summary>
    bool IsReady { get; }
}
