using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Validation;

/// <summary>
/// Deterministic field validator.
/// All validation logic is in code - the LLM has no say here.
/// </summary>
public partial class FormValidatorService : IFormValidator
{
    private readonly ILogger<FormValidatorService> _logger;

    public FormValidatorService(ILogger<FormValidatorService> logger)
    {
        _logger = logger;
    }

    public ValidationResult Validate(FieldDefinition field, string? value)
    {
        _logger.LogDebug("Validating field '{FieldId}' with value '{Value}'", field.Id, value);

        // Null/empty check for required fields
        if (string.IsNullOrWhiteSpace(value))
        {
            if (field.Required)
            {
                return new ValidationResult(false, "This field is required");
            }
            return new ValidationResult(true, NormalizedValue: null);
        }

        // Type-specific validation
        return field.Type switch
        {
            FieldType.Text => ValidateText(field, value),
            FieldType.Email => ValidateEmail(field, value),
            FieldType.Phone => ValidatePhone(field, value),
            FieldType.Date => ValidateDate(field, value),
            FieldType.Number => ValidateNumber(field, value),
            FieldType.Choice => ValidateChoice(field, value),
            FieldType.Boolean => ValidateBoolean(field, value),
            _ => new ValidationResult(true, NormalizedValue: value)
        };
    }

    private ValidationResult ValidateText(FieldDefinition field, string value)
    {
        var constraints = field.Constraints;

        if (constraints?.MinLength.HasValue == true && value.Length < constraints.MinLength)
        {
            return new ValidationResult(false, $"Must be at least {constraints.MinLength} characters");
        }

        if (constraints?.MaxLength.HasValue == true && value.Length > constraints.MaxLength)
        {
            return new ValidationResult(false, $"Must be at most {constraints.MaxLength} characters");
        }

        if (!string.IsNullOrEmpty(constraints?.Pattern))
        {
            if (!Regex.IsMatch(value, constraints.Pattern))
            {
                return new ValidationResult(false, "Value does not match required format");
            }
        }

        return new ValidationResult(true, NormalizedValue: value.Trim());
    }

    private ValidationResult ValidateEmail(FieldDefinition field, string value)
    {
        // Normalize: lowercase, trim
        var normalized = value.Trim().ToLowerInvariant();

        // Basic email pattern
        if (!EmailRegex().IsMatch(normalized))
        {
            return new ValidationResult(false, "Invalid email format");
        }

        return new ValidationResult(true, NormalizedValue: normalized);
    }

    private ValidationResult ValidatePhone(FieldDefinition field, string value)
    {
        // Extract digits only
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 7)
        {
            return new ValidationResult(false, "Phone number too short");
        }

        if (digitsOnly.Length > 15)
        {
            return new ValidationResult(false, "Phone number too long");
        }

        return new ValidationResult(true, NormalizedValue: digitsOnly);
    }

    private ValidationResult ValidateDate(FieldDefinition field, string value)
    {
        // Try to parse as ISO 8601 first
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return ValidateDateConstraints(field, date, value);
        }

        // Try other common formats
        string[] formats = ["M/d/yyyy", "d/M/yyyy", "MMM d, yyyy", "MMMM d, yyyy", "d MMM yyyy", "d MMMM yyyy"];
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                var normalized = date.ToString("yyyy-MM-dd");
                return ValidateDateConstraints(field, date, normalized);
            }
        }

        // Try general parsing as last resort
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            var normalized = date.ToString("yyyy-MM-dd");
            return ValidateDateConstraints(field, date, normalized);
        }

        return new ValidationResult(false, "Could not parse date. Expected format: YYYY-MM-DD");
    }

    private ValidationResult ValidateDateConstraints(FieldDefinition field, DateTime date, string normalizedValue)
    {
        var constraints = field.Constraints;

        if (!string.IsNullOrEmpty(constraints?.DateMin))
        {
            if (DateTime.TryParse(constraints.DateMin, out var minDate) && date < minDate)
            {
                return new ValidationResult(false, $"Date must be on or after {constraints.DateMin}");
            }
        }

        if (!string.IsNullOrEmpty(constraints?.DateMax))
        {
            if (DateTime.TryParse(constraints.DateMax, out var maxDate) && date > maxDate)
            {
                return new ValidationResult(false, $"Date must be on or before {constraints.DateMax}");
            }
        }

        return new ValidationResult(true, NormalizedValue: normalizedValue);
    }

    private ValidationResult ValidateNumber(FieldDefinition field, string value)
    {
        if (!double.TryParse(value, CultureInfo.InvariantCulture, out var number))
        {
            return new ValidationResult(false, "Invalid number format");
        }

        var constraints = field.Constraints;

        if (constraints?.Min.HasValue == true && number < constraints.Min)
        {
            return new ValidationResult(false, $"Must be at least {constraints.Min}");
        }

        if (constraints?.Max.HasValue == true && number > constraints.Max)
        {
            return new ValidationResult(false, $"Must be at most {constraints.Max}");
        }

        return new ValidationResult(true, NormalizedValue: number.ToString(CultureInfo.InvariantCulture));
    }

    private ValidationResult ValidateChoice(FieldDefinition field, string value)
    {
        var choices = field.Constraints?.Choices;

        if (choices == null || choices.Count == 0)
        {
            return new ValidationResult(true, NormalizedValue: value);
        }

        // Case-insensitive match
        var match = choices.FirstOrDefault(c =>
            c.Equals(value, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            return new ValidationResult(false, $"Must be one of: {string.Join(", ", choices)}");
        }

        return new ValidationResult(true, NormalizedValue: match);
    }

    private ValidationResult ValidateBoolean(FieldDefinition field, string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        // Accept various boolean representations
        var trueValues = new[] { "yes", "y", "true", "1", "yeah", "yep", "correct", "affirmative" };
        var falseValues = new[] { "no", "n", "false", "0", "nope", "negative" };

        if (trueValues.Contains(normalized))
        {
            return new ValidationResult(true, NormalizedValue: "true");
        }

        if (falseValues.Contains(normalized))
        {
            return new ValidationResult(true, NormalizedValue: "false");
        }

        return new ValidationResult(false, "Please answer yes or no");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();
}
