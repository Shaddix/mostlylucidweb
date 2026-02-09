using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Extracts structured entities (dates, numbers, URLs, phones, emails, IPs)
/// from text using Microsoft.Recognizers.Text.
/// </summary>
public interface ITextRecognizerService
{
    /// <summary>
    /// Extract all recognizable entities from text.
    /// </summary>
    /// <param name="text">Input text to analyze</param>
    /// <param name="culture">Culture for recognition (e.g., "en-us"). Defaults to config value.</param>
    /// <returns>All recognized signals</returns>
    RecognizedSignals ExtractAll(string text, string? culture = null);
}
