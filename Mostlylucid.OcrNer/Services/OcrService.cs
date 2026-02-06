using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;
using Mostlylucid.OcrNer.Services.Preprocessing;
using Tesseract;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Tesseract OCR service with automatic tessdata download and image preprocessing.
///
/// How it works:
/// 1. On first call, downloads tessdata for the configured language
/// 2. Preprocesses the image (grayscale, contrast, sharpen) for best OCR results
/// 3. Runs Tesseract OCR on the preprocessed image
/// 4. Returns extracted text with confidence score
///
/// The Tesseract engine is created once and reused (singleton pattern).
/// Thread safety is ensured via SemaphoreSlim.
///
/// Prerequisites: Tesseract native libraries must be installed on the system.
/// On Windows: install via NuGet (automatically included).
/// On Linux: apt-get install tesseract-ocr
/// On macOS: brew install tesseract
/// </summary>
public class OcrService : IOcrService, IDisposable
{
    private readonly ILogger<OcrService> _logger;
    private readonly OcrNerConfig _config;
    private readonly ModelDownloader _downloader;
    private readonly ImagePreprocessor _preprocessor;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private TesseractEngine? _engine;
    private bool _initialized;

    public OcrService(
        ILogger<OcrService> logger,
        IOptions<OcrNerConfig> config,
        ModelDownloader downloader,
        ImagePreprocessor preprocessor)
    {
        _logger = logger;
        _config = config.Value;
        _downloader = downloader;
        _preprocessor = preprocessor;
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractTextAsync(string imagePath, CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image file not found: {imagePath}", imagePath);

        var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
        return await ExtractTextAsync(imageBytes, ct);
    }

    /// <inheritdoc />
    public async Task<OcrResult> ExtractTextAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Preprocess for better OCR (configurable level)
        var preprocessed = _config.Preprocessing == Config.PreprocessingLevel.None
            ? imageBytes
            : _preprocessor.Preprocess(imageBytes, _config.Preprocessing switch
            {
                Config.PreprocessingLevel.Minimal => PreprocessingOptions.Minimal,
                Config.PreprocessingLevel.Aggressive => PreprocessingOptions.Aggressive,
                _ => PreprocessingOptions.Default
            });

        return await Task.Run(() =>
        {
            using var pix = Pix.LoadFromMemory(preprocessed);
            using var page = _engine!.Process(pix);

            var text = page.GetText();
            var confidence = page.GetMeanConfidence();

            _logger.LogDebug("OCR extracted {Chars} chars with {Conf:P0} confidence",
                text.Length, confidence);

            return new OcrResult
            {
                Text = text,
                Confidence = confidence
            };
        }, ct);
    }

    /// <summary>
    /// Lazy initialization: download tessdata and create Tesseract engine
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var tessdataDir = await _downloader.EnsureTessdataAsync(ct);

            _engine = new TesseractEngine(
                tessdataDir,
                _config.TesseractLanguage,
                EngineMode.Default);

            _initialized = true;
            _logger.LogInformation("Tesseract engine initialized with language '{Lang}'",
                _config.TesseractLanguage);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        _engine?.Dispose();
        _initLock.Dispose();
    }
}
