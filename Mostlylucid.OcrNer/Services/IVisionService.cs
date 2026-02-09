using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Service for understanding images using Florence-2, a lightweight vision model.
///
/// Florence-2 is a small (~450MB) ONNX model from Microsoft that runs locally.
/// It can caption images, extract text (OCR), and describe what it sees.
///
/// This is different from Tesseract OCR:
/// - Tesseract is specialized for reading text from document images
/// - Florence-2 understands the whole image (objects, scenes, people, text)
///
/// The model is auto-downloaded on first use.
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// Generate a caption describing the image content.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="detailed">If true, generates a more detailed caption</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Caption result with description text</returns>
    Task<VisionCaptionResult> CaptionAsync(string imagePath, bool detailed = true, CancellationToken ct = default);

    /// <summary>
    /// Extract text from an image using Florence-2's built-in OCR.
    /// For document-quality OCR, use <see cref="IOcrService"/> instead.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>OCR result with extracted text</returns>
    Task<VisionOcrResult> ExtractTextAsync(string imagePath, CancellationToken ct = default);

    /// <summary>
    /// Check if the Florence-2 model is available and loaded.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
