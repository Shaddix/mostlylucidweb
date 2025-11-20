using Mostlylucid.SemanticGallery.Demo.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.SemanticGallery.Demo.Services;

/// <summary>
/// Simplified image analysis service for demo purposes
/// Uses the existing Florence2 service from SemanticSearch project
/// </summary>
public class SimplifiedImageAnalysisService
{
    private readonly ILogger<SimplifiedImageAnalysisService> _logger;
    private readonly IImageAnalysisService? _imageAnalysisService;

    public SimplifiedImageAnalysisService(
        ILogger<SimplifiedImageAnalysisService> logger,
        IImageAnalysisService? imageAnalysisService = null)
    {
        _logger = logger;
        _imageAnalysisService = imageAnalysisService;
    }

    public async Task<(string Caption, string ExtractedText)> AnalyzeImageAsync(Stream imageStream)
    {
        try
        {
            if (_imageAnalysisService != null)
            {
                // Use real Florence-2 service if available
                imageStream.Position = 0;
                var caption = await _imageAnalysisService.GenerateAltTextAsync(imageStream, "MORE_DETAILED_CAPTION");

                imageStream.Position = 0;
                var extractedText = await _imageAnalysisService.ExtractTextAsync(imageStream);

                return (caption, extractedText);
            }
            else
            {
                // Fallback to mock data for demo
                _logger.LogWarning("Florence-2 not available, using mock data");
                return ("Demo image - AI captioning not available", string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image");
            return ("Error analyzing image", string.Empty);
        }
    }

    public async Task<List<DetectedFace>> DetectFacesAsync(Stream imageStream)
    {
        // Simplified face detection for demo
        // In production, use MTCNN, RetinaFace, or FaceNet
        _logger.LogInformation("Face detection not implemented in demo - returning empty list");
        return await Task.FromResult(new List<DetectedFace>());
    }
}
