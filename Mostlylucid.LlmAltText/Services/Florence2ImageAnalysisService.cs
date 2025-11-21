using Florence2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.LlmAltText.Models;

namespace Mostlylucid.LlmAltText.Services;

/// <summary>
/// Florence-2 Vision Language Model implementation for alt text generation and OCR
/// </summary>
public class Florence2ImageAnalysisService : IImageAnalysisService, IDisposable
{
    private readonly ILogger<Florence2ImageAnalysisService> _logger;
    private readonly AltTextOptions _options;
    private readonly Florence2Model? _model;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public bool IsReady => _isInitialized && _model is not null;

    public Florence2ImageAnalysisService(
        ILogger<Florence2ImageAnalysisService> logger,
        IOptions<AltTextOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        try
        {
            LogInfo("Initializing Florence-2 Vision Language Model...");
            LogInfo($"Model path: {_options.ModelPath}");
            LogInfo("Note: Models (~800MB) will be downloaded on first use if not present");

            var modelSource = new FlorenceModelDownloader(_options.ModelPath);

            // Download models if not already present
            LogInfo("Checking for model files...");
            modelSource
                .DownloadModelsAsync(
                    (Florence2.IStatus status) => LogModelDownloadStatus(status),
                    _logger,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            _model = new Florence2Model(modelSource);
            _isInitialized = true;

            LogInfo("Florence-2 model initialized successfully");
            LogInfo($"Available task types: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION, OCR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Florence-2 model. Alt text generation will not be available.");
            _isInitialized = false;
        }
    }

    public async Task<string> GenerateAltTextAsync(Stream imageStream, string taskType = "MORE_DETAILED_CAPTION")
    {
        await EnsureInitializedAsync();

        var task = ResolveTaskType(taskType, TaskTypes.MORE_DETAILED_CAPTION);

        try
        {
            LogInfo($"Generating alt text using task type: {task}");
            var startTime = DateTime.UtcNow;

            var results = _model!.Run(task, new[] { imageStream }, textInput: _options.AltTextPrompt, CancellationToken.None);
            var altText = results.FirstOrDefault()?.PureText;

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Alt text generated in {duration:F0}ms");

            if (string.IsNullOrWhiteSpace(altText))
            {
                _logger.LogWarning("No alt text generated for image");
                return "No description available";
            }

            var normalized = NormalizeAltText(altText);
            LogInfo($"Generated alt text: {normalized.Substring(0, Math.Min(50, normalized.Length))}...");

            return normalized;
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
            LogInfo("Extracting text from image using OCR");
            var startTime = DateTime.UtcNow;

            var results = _model!.Run(TaskTypes.OCR, new[] { imageStream }, textInput: string.Empty, CancellationToken.None);
            var ocrText = results.FirstOrDefault()?.PureText;

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Text extracted in {duration:F0}ms");

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                LogInfo("No text found in image");
                return "No text found";
            }

            var trimmed = ocrText.Trim();
            LogInfo($"Extracted {trimmed.Length} characters of text");

            return trimmed;
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
            LogInfo("Starting complete image analysis (alt text + OCR)");
            var startTime = DateTime.UtcNow;

            // Create a memory stream to allow multiple reads
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            LogInfo($"Image loaded: {memoryStream.Length:N0} bytes");

            // Generate alt text
            memoryStream.Position = 0;
            var altText = await GenerateAltTextAsync(memoryStream, _options.DefaultTaskType);

            // Extract text (OCR)
            memoryStream.Position = 0;
            var extractedText = await ExtractTextAsync(memoryStream);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LogInfo($"Complete image analysis finished in {duration:F0}ms");

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
        if (_isInitialized && _model is not null) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized && _model is not null) return;

            throw new InvalidOperationException(
                "Florence-2 model failed to initialize. Please check logs for details. " +
                "Ensure you have sufficient disk space (~800MB) and network connectivity for model downloads.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private TaskTypes ResolveTaskType(string taskType, TaskTypes fallback)
    {
        if (Enum.TryParse<TaskTypes>(taskType, true, out var parsed))
        {
            return parsed;
        }

        _logger.LogWarning("Unknown task type '{TaskType}'; using fallback '{Fallback}'", taskType, fallback);
        return fallback;
    }

    private string NormalizeAltText(string altText)
    {
        var normalized = altText.Trim();

        // Ensure proper sentence ending
        if (!normalized.EndsWith(".") && !normalized.EndsWith("!") && !normalized.EndsWith("?"))
        {
            normalized += ".";
        }

        // Check word count and warn if exceeding recommendation
        var wordCount = normalized.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > _options.MaxWords)
        {
            _logger.LogWarning(
                "Generated alt text has {WordCount} words, exceeding recommended maximum of {MaxWords}",
                wordCount, _options.MaxWords);
        }

        return normalized;
    }

    private void LogModelDownloadStatus(Florence2.IStatus status)
    {
        if (_options.EnableDiagnosticLogging)
        {
            if (!string.IsNullOrEmpty(status.Error))
            {
                _logger.LogError("Model download error: {Error}", status.Error);
            }
            else
            {
                _logger.LogInformation(
                    "Model download progress: {Progress:P1} - {Message}",
                    status.Progress,
                    status.Message ?? "Processing");
            }
        }
    }

    private void LogInfo(string message)
    {
        if (_options.EnableDiagnosticLogging)
        {
            _logger.LogInformation(message);
        }
    }

    public void Dispose()
    {
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
