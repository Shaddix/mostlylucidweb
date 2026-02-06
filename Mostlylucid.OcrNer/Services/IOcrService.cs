using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Service for extracting text from images using Tesseract OCR.
///
/// Images are automatically preprocessed (grayscale, contrast boost, sharpening)
/// to improve OCR accuracy before being passed to Tesseract.
///
/// Requires Tesseract native libraries to be available on the system.
/// The tessdata language files are auto-downloaded on first use.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extract text from an image file.
    /// On first call, automatically downloads tessdata for the configured language.
    /// </summary>
    /// <param name="imagePath">Path to the image file (PNG, JPEG, TIFF, BMP)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>OCR result with extracted text and confidence score</returns>
    Task<OcrResult> ExtractTextAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// Extract text from an image provided as a byte array.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>OCR result with extracted text and confidence score</returns>
    Task<OcrResult> ExtractTextAsync(byte[] imageBytes, CancellationToken ct = default);
}
