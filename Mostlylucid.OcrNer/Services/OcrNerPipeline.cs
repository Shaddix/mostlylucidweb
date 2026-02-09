using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Combines Tesseract OCR and BERT NER into a single pipeline.
///
/// Pipeline steps:
/// 1. Image → preprocessed (grayscale, contrast, sharpen via ImageSharp — or OpenCV when advanced mode)
/// 2. Preprocessed image → text (Tesseract OCR)
/// 3. Text → named entities (BERT NER via ONNX)
/// 4. (Optional) Text → recognized signals (Microsoft.Recognizers.Text)
///
/// All models are downloaded automatically on first use.
/// </summary>
public class OcrNerPipeline : IOcrNerPipeline
{
    private readonly ILogger<OcrNerPipeline> _logger;
    private readonly IOcrService _ocrService;
    private readonly INerService _nerService;
    private readonly ITextRecognizerService _textRecognizer;
    private readonly OcrNerConfig _config;

    public OcrNerPipeline(
        ILogger<OcrNerPipeline> logger,
        IOcrService ocrService,
        INerService nerService,
        ITextRecognizerService textRecognizer,
        IOptions<OcrNerConfig> config)
    {
        _logger = logger;
        _ocrService = ocrService;
        _nerService = nerService;
        _textRecognizer = textRecognizer;
        _config = config.Value;
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

        // Step 3: Optional recognizer signals
        var signals = ExtractSignalsIfEnabled(ocrResult.Text);

        _logger.LogInformation("Pipeline complete: {Entities} entities from {Chars} chars of OCR text",
            nerResult.Entities.Count, ocrResult.Text.Length);

        return new OcrNerResult
        {
            OcrResult = ocrResult,
            NerResult = nerResult,
            Signals = signals
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

        // Step 3: Optional recognizer signals
        var signals = ExtractSignalsIfEnabled(ocrResult.Text);

        return new OcrNerResult
        {
            OcrResult = ocrResult,
            NerResult = nerResult,
            Signals = signals
        };
    }

    private RecognizedSignals? ExtractSignalsIfEnabled(string text)
    {
        if (!_config.EnableRecognizers)
            return null;

        _logger.LogDebug("Running Microsoft.Recognizers.Text extraction");
        var signals = _textRecognizer.ExtractAll(text);

        if (signals.HasAnySignals)
            _logger.LogDebug("Recognizers found: {Dates} dates, {Numbers} numbers, {Urls} URLs, {Phones} phones, {Emails} emails, {IPs} IPs",
                signals.DateTimes.Count, signals.Numbers.Count, signals.Urls.Count,
                signals.PhoneNumbers.Count, signals.Emails.Count, signals.IpAddresses.Count);

        return signals;
    }
}
