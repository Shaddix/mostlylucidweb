namespace Mostlylucid.OcrNer.Models;

/// <summary>
/// Result of OCR text extraction from an image
/// </summary>
public class OcrResult
{
    /// <summary>
    /// The extracted text from the image
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Mean confidence from Tesseract (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; init; }
}
