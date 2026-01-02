using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Validation;

/// <summary>
/// Abstraction for deterministic field validation.
/// Validation is done in code, NOT by the LLM.
/// The LLM extracts, validation confirms it's valid.
/// </summary>
public interface IFormValidator
{
    /// <summary>
    /// Validate an extracted value against field constraints
    /// </summary>
    /// <param name="field">The field definition with constraints</param>
    /// <param name="value">The extracted value to validate</param>
    /// <returns>Validation result with possible normalized value</returns>
    ValidationResult Validate(FieldDefinition field, string? value);
}
