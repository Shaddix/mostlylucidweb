namespace Mostlylucid.LlmAltText.Models;

/// <summary>
/// Configuration options for alt text generation
/// </summary>
public class AltTextOptions
{
    /// <summary>
    /// Directory where Florence-2 models will be downloaded and stored
    /// Default: "./models"
    /// Note: Models are approximately 800MB and will be downloaded on first use
    /// </summary>
    public string ModelPath { get; set; } = "./models";

    /// <summary>
    /// Custom prompt for alt text generation
    /// Default provides descriptive, accessible alt text
    /// </summary>
    public string AltTextPrompt { get; set; } =
        "Provide 2-3 complete, descriptive alt text sentences in English. Do not stop mid-sentence; include context, subjects, and visible relationships. Avoid fragments and keep under 90 words.";

    /// <summary>
    /// Default task type for alt text generation
    /// Options: CAPTION, DETAILED_CAPTION, MORE_DETAILED_CAPTION
    /// Default: MORE_DETAILED_CAPTION
    /// </summary>
    public string DefaultTaskType { get; set; } = "MORE_DETAILED_CAPTION";

    /// <summary>
    /// Enable detailed diagnostic logging for model initialization and processing
    /// </summary>
    public bool EnableDiagnosticLogging { get; set; } = true;

    /// <summary>
    /// Maximum word count for generated alt text
    /// Default: 90 (recommended for accessibility)
    /// </summary>
    public int MaxWords { get; set; } = 90;
}
