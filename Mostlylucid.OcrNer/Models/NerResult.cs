namespace Mostlylucid.OcrNer.Models;

/// <summary>
/// Result of NER extraction from text
/// </summary>
public class NerResult
{
    /// <summary>
    /// The input text that was analyzed
    /// </summary>
    public string SourceText { get; init; } = string.Empty;

    /// <summary>
    /// All entities found in the text
    /// </summary>
    public List<NerEntity> Entities { get; init; } = [];
}
