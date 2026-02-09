namespace Mostlylucid.OcrNer.Models;

/// <summary>
/// A single named entity extracted from text
/// </summary>
public class NerEntity
{
    /// <summary>
    /// The entity text as it appears in the source
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Entity type: PER, ORG, LOC, or MISC
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Character offset where the entity starts in the source text
    /// </summary>
    public int StartOffset { get; init; }

    /// <summary>
    /// Character offset where the entity ends in the source text (exclusive)
    /// </summary>
    public int EndOffset { get; init; }

    public override string ToString() => $"[{Label}] {Text} ({Confidence:P0})";
}
