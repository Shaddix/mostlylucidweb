using Mostlylucid.VoiceForm.Models.Extraction;

namespace Mostlylucid.VoiceForm.Services.Extraction;

/// <summary>
/// Abstraction for LLM-based field extraction.
/// The extractor is a TRANSLATOR - it converts transcript to typed values.
/// It does NOT control flow, validate, or make decisions.
/// </summary>
public interface IFieldExtractor
{
    /// <summary>
    /// Extract a field value from a transcript using the LLM
    /// </summary>
    /// <param name="context">The extraction context (field definition + prompt + transcript)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction response with value and confidence</returns>
    Task<ExtractionResponse> ExtractAsync(
        ExtractionContext context,
        CancellationToken cancellationToken = default);
}
