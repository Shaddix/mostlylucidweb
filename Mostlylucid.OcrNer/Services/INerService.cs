using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Service for extracting named entities from text using BERT NER.
///
/// Recognizes four entity types:
/// - PER: Person names (e.g. "John Smith", "Marie Curie")
/// - ORG: Organizations (e.g. "Microsoft", "United Nations")
/// - LOC: Locations (e.g. "Seattle", "France")
/// - MISC: Miscellaneous entities (e.g. "English", "World Cup")
/// </summary>
public interface INerService
{
    /// <summary>
    /// Extract named entities from text.
    /// On first call, automatically downloads the BERT NER model (~430MB).
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>NER result containing all found entities with positions and confidence</returns>
    Task<NerResult> ExtractEntitiesAsync(string text, CancellationToken ct = default);
}
