using Microsoft.Extensions.Logging;
using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Combines Tesseract OCR and BERT NER into a single pipeline.
///
/// Pipeline steps:
/// 1. Image → preprocessed (grayscale, contrast, sharpen via ImageSharp)
/// 2. Preprocessed image → text (Tesseract OCR)
/// 3. Text → named entities (BERT NER via ONNX)
///
/// All models are downloaded automatically on first use.
/// </summary>
public class OcrNerPipeline : IOcrNerPipeline
{
    private readonly ILogger<OcrNerPipeline> _logger;
    private readonly IOcrService _ocrService;
    private readonly INerService _nerService;

    public OcrNerPipeline(
        ILogger<OcrNerPipeline> logger,
        IOcrService ocrService,
        INerService nerService)
    {
        _logger = logger;
        _ocrService = ocrService;
        _nerService = nerService;
    }

    /// <inheritdoc />
    public async Task<OcrNerResult> ProcessImageAsync(string imagePath, CancellationToken ct = default)
    {
        _logger.LogDebug("Processing image: {Path}", imagePath);

        // Step 1: OCR
        var ocrResult = await _ocrService.ExtractTextAsync(imagePath, ct);

        if (string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            _logger.LogDebug("No text extracted from image");
            return new OcrNerResult
            {
                OcrResult = ocrResult,
                NerResult = new NerResult { SourceText = string.Empty }
            };
        }

        _logger.LogDebug("OCR extracted {Chars} chars, running NER", ocrResult.Text.Length);

        // Step 2: NER on extracted text
        var nerResult = await _nerService.ExtractEntitiesAsync(ocrResult.Text, ct);

        _logger.LogInformation("Pipeline complete: {Entities} entities from {Chars} chars of OCR text",
            nerResult.Entities.Count, ocrResult.Text.Length);

        return new OcrNerResult
        {
            OcrResult = ocrResult,
            NerResult = nerResult
        };
    }

    /// <inheritdoc />
    public async Task<OcrNerResult> ProcessImageAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        _logger.LogDebug("Processing image from bytes ({Size} bytes)", imageBytes.Length);

        // Step 1: OCR
        var ocrResult = await _ocrService.ExtractTextAsync(imageBytes, ct);

        if (string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            return new OcrNerResult
            {
                OcrResult = ocrResult,
                NerResult = new NerResult { SourceText = string.Empty }
            };
        }

        // Step 2: NER
        var nerResult = await _nerService.ExtractEntitiesAsync(ocrResult.Text, ct);

        return new OcrNerResult
        {
            OcrResult = ocrResult,
            NerResult = nerResult
        };
    }
}
