using Florence2;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.AltText.Demo.Services;

public class Florence2ImageAnalysisService : IImageAnalysisService, IDisposable
{
    private readonly ILogger<Florence2ImageAnalysisService> _logger;
    private readonly Florence2Model _model;
    private readonly Florence2ModelSession? _session;
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
            modelSource.DownloadModelsAsync().GetAwaiter().GetResult();

            _model = new Florence2Model(modelSource);
            _session = _model.CreateSession();
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

            var results = _session!.Run(taskType, imageStream);

            if (results != null && results.Count > 0)
            {
                var altText = results.First().Value;
                _logger.LogInformation("Successfully generated alt text: {AltText}", altText);
                return altText;
            }

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

            var results = _session!.Run("OCR", imageStream);

            if (results != null && results.Count > 0)
            {
                var extractedText = results.First().Value;
                _logger.LogInformation("Successfully extracted text: {ExtractedText}", extractedText);
                return extractedText;
            }

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
        _session?.Dispose();
        _initLock.Dispose();
    }
}
