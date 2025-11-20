using Florence2;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.AltText.Demo.Services;

public class Florence2ImageAnalysisService : IImageAnalysisService, IDisposable
{
    private readonly ILogger<Florence2ImageAnalysisService> _logger;
    private readonly Florence2Model? _model;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public Florence2ImageAnalysisService(ILogger<Florence2ImageAnalysisService> logger)
    {
        _logger = logger;

        try
        {
            _logger.LogInformation("Initializing Florence2 model...");
            var modelSource = new FlorenceModelDownloader("./models");

            // Download models if not already present
            // TODO: Fix Florence2 API - DownloadModelsAsync now requires onStatusUpdate parameter
            // modelSource.DownloadModelsAsync(status => _logger.LogInformation("Download status: {Status}", status), _logger).GetAwaiter().GetResult();
            throw new NotImplementedException("Florence2 API has changed - needs to be updated to match version 25.7.59767");

            _model = new Florence2Model(modelSource);
            _isInitialized = true;

            _logger.LogInformation("Florence2 model initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Florence2 model");
            _isInitialized = false;
        }
    }

    public async Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION")
    {
        await EnsureInitializedAsync();

        try
        {
            _logger.LogInformation("Generating alt text for image using task type: {TaskType}", taskType);

            // TODO: Fix Florence2 API - Run method now requires textInput parameter
            // var results = _model!.Run(taskType, new[] { imageStream }, textInput: "", CancellationToken.None);
            throw new NotImplementedException("Florence2 API has changed - needs to be updated to match version 25.7.59767");

            _logger.LogWarning("No alt text generated for image");
            return "No description available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating alt text");
            throw;
        }
    }

    public async Task<string> ExtractTextAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            _logger.LogInformation("Extracting text from image using OCR");

            // TODO: Fix Florence2 API - Run method now requires textInput parameter
            // var results = _model!.Run("OCR", new[] { imageStream }, textInput: "", CancellationToken.None);
            throw new NotImplementedException("Florence2 API has changed - needs to be updated to match version 25.7.59767");

            _logger.LogWarning("No text extracted from image");
            return "No text found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from image");
            throw;
        }
    }

    public async Task<(string AltText, string ExtractedText)> AnalyzeImageAsync(Stream imageStream)
    {
        await EnsureInitializedAsync();

        try
        {
            // Create a memory stream to allow multiple reads
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);

            // Generate alt text
            memoryStream.Position = 0;
            var altText = await GenerateAltTextAsync(memoryStream, "MORE_DETAILED_CAPTION");

            // Extract text (OCR)
            memoryStream.Position = 0;
            var extractedText = await ExtractTextAsync(memoryStream);

            return (altText, extractedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image");
            throw;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            throw new InvalidOperationException("Florence2 model failed to initialize. Please check logs for details.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        // TODO: Florence2Model no longer implements IDisposable in version 25.7.59767
        // _model?.Dispose();
        _initLock.Dispose();
    }
}
