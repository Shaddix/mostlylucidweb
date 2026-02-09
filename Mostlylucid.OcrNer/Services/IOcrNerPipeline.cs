using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Combined OCR + NER pipeline.
///
/// Extracts text from an image using Tesseract OCR, then finds named entities
/// (people, organizations, locations) in the extracted text using BERT NER.
///
/// This is the main entry point if you want to go from image → entities in one call.
///
/// For text-only NER (no image), use <see cref="INerService"/> directly.
/// For OCR-only (no entity extraction), use <see cref="IOcrService"/> directly.
/// For image understanding (captioning), use <see cref="IVisionService"/> directly.
/// </summary>
public interface IOcrNerPipeline
{
    /// <summary>
    /// Extract text from an image and find named entities in it.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Combined OCR + NER result</returns>
    Task<OcrNerResult> ProcessImageAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// Extract text from image bytes and find named entities in it.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Combined OCR + NER result</returns>
    Task<OcrNerResult> ProcessImageAsync(byte[] imageBytes, CancellationToken ct = default);
}
