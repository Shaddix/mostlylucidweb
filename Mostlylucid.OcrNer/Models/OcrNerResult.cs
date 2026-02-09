namespace Mostlylucid.OcrNer.Models;

/// <summary>
/// Combined result of OCR followed by NER
/// </summary>
public class OcrNerResult
{
    /// <summary>
    /// The OCR extraction result (text + confidence)
    /// </summary>
    public OcrResult OcrResult { get; init; } = new();

    /// <summary>
    /// The NER extraction result (entities found in OCR text)
    /// </summary>
    public NerResult NerResult { get; init; } = new();

    /// <summary>
    /// Rule-based recognized signals (dates, numbers, URLs, phones, emails, IPs).
    /// Only populated when EnableRecognizers is true in config.
    /// </summary>
    public RecognizedSignals? Signals { get; init; }
}
