namespace Mostlylucid.VoiceForm.Models.FormSchema;

/// <summary>
/// Validation constraints for a form field
/// </summary>
public class FieldConstraints
{
    /// <summary>
    /// Minimum length for text fields
    /// </summary>
    public int? MinLength { get; set; }

    /// <summary>
    /// Maximum length for text fields
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Minimum value for number fields
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Maximum value for number fields
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Regex pattern for validation
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Valid choices for choice fields
    /// </summary>
    public List<string>? Choices { get; set; }

    /// <summary>
    /// Minimum date for date fields (ISO 8601)
    /// </summary>
    public string? DateMin { get; set; }

    /// <summary>
    /// Maximum date for date fields (ISO 8601)
    /// </summary>
    public string? DateMax { get; set; }
}
