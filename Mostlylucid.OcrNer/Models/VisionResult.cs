namespace Mostlylucid.OcrNer.Models;

/// <summary>
/// Result from Florence-2 image captioning
/// </summary>
public class VisionCaptionResult
{
    /// <summary>
    /// Whether captioning succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The generated caption describing the image
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Error message if captioning failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Time taken in milliseconds
    /// </summary>
    public long DurationMs { get; init; }
}

/// <summary>
/// Result from Florence-2 OCR text extraction
/// </summary>
public class VisionOcrResult
{
    /// <summary>
    /// Whether OCR extraction succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The extracted text from the image
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Time taken in milliseconds
    /// </summary>
    public long DurationMs { get; init; }
}
