using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Services.Validation;
using Xunit;

namespace Mostlylucid.VoiceForm.Tests.Services.Validation;

public class FormValidatorServiceTests
{
    private readonly FormValidatorService _validator;

    public FormValidatorServiceTests()
    {
        var logger = new Mock<ILogger<FormValidatorService>>();
        _validator = new FormValidatorService(logger.Object);
    }

    #region Text Validation

    [Fact]
    public void Validate_TextFieldWithValidValue_ReturnsValid()
    {
        // Arrange
        var field = CreateTextField("name", required: true);
        var value = "John Smith";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be("John Smith");
    }

    [Fact]
    public void Validate_RequiredTextFieldWithNull_ReturnsInvalid()
    {
        // Arrange
        var field = CreateTextField("name", required: true);

        // Act
        var result = _validator.Validate(field, null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("required");
    }

    [Fact]
    public void Validate_OptionalTextFieldWithNull_ReturnsValid()
    {
        // Arrange
        var field = CreateTextField("nickname", required: false);

        // Act
        var result = _validator.Validate(field, null);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TextFieldBelowMinLength_ReturnsInvalid()
    {
        // Arrange
        var field = CreateTextField("name", minLength: 3);
        var value = "Jo";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least 3 characters");
    }

    [Fact]
    public void Validate_TextFieldAboveMaxLength_ReturnsInvalid()
    {
        // Arrange
        var field = CreateTextField("code", maxLength: 5);
        var value = "ABCDEFGH";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at most 5 characters");
    }

    #endregion

    #region Email Validation

    [Theory]
    [InlineData("john@example.com", "john@example.com")]
    [InlineData("JOHN@EXAMPLE.COM", "john@example.com")]
    [InlineData("john.smith@example.co.uk", "john.smith@example.co.uk")]
    public void Validate_ValidEmail_ReturnsValidAndNormalized(string input, string expected)
    {
        // Arrange
        var field = CreateEmailField("email");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@tld")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    public void Validate_InvalidEmail_ReturnsInvalid(string input)
    {
        // Arrange
        var field = CreateEmailField("email");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("email");
    }

    #endregion

    #region Phone Validation

    [Theory]
    [InlineData("555-123-4567", "5551234567")]
    [InlineData("(555) 123-4567", "5551234567")]
    [InlineData("+1 555 123 4567", "15551234567")]
    [InlineData("5551234567", "5551234567")]
    public void Validate_ValidPhone_ReturnsValidAndNormalized(string input, string expected)
    {
        // Arrange
        var field = CreatePhoneField("phone");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("12345678901234567890")] // Too long
    public void Validate_InvalidPhone_ReturnsInvalid(string input)
    {
        // Arrange
        var field = CreatePhoneField("phone");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Date Validation

    [Theory]
    [InlineData("1985-05-12", "1985-05-12")]
    [InlineData("5/12/1985", "1985-05-12")]
    [InlineData("May 12, 1985", "1985-05-12")]
    public void Validate_ValidDate_ReturnsValidAndNormalized(string input, string expected)
    {
        // Arrange
        var field = CreateDateField("dob");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Fact]
    public void Validate_DateBeforeMin_ReturnsInvalid()
    {
        // Arrange
        var field = CreateDateField("dob", dateMin: "1900-01-01", dateMax: "2010-01-01");
        var value = "1850-05-12";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("on or after");
    }

    [Fact]
    public void Validate_DateAfterMax_ReturnsInvalid()
    {
        // Arrange
        var field = CreateDateField("dob", dateMin: "1900-01-01", dateMax: "2010-01-01");
        var value = "2020-05-12";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("on or before");
    }

    [Fact]
    public void Validate_InvalidDateFormat_ReturnsInvalid()
    {
        // Arrange
        var field = CreateDateField("dob");
        var value = "not a date";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("parse date");
    }

    #endregion

    #region Number Validation

    [Theory]
    [InlineData("42", "42")]
    [InlineData("3.14", "3.14")]
    [InlineData("-10", "-10")]
    public void Validate_ValidNumber_ReturnsValidAndNormalized(string input, string expected)
    {
        // Arrange
        var field = CreateNumberField("age");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Fact]
    public void Validate_NumberBelowMin_ReturnsInvalid()
    {
        // Arrange
        var field = CreateNumberField("age", min: 0, max: 150);
        var value = "-5";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least 0");
    }

    [Fact]
    public void Validate_NumberAboveMax_ReturnsInvalid()
    {
        // Arrange
        var field = CreateNumberField("age", min: 0, max: 150);
        var value = "200";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at most 150");
    }

    #endregion

    #region Boolean Validation

    [Theory]
    [InlineData("yes", "true")]
    [InlineData("YES", "true")]
    [InlineData("y", "true")]
    [InlineData("true", "true")]
    [InlineData("yeah", "true")]
    [InlineData("correct", "true")]
    public void Validate_TrueBoolean_ReturnsValid(string input, string expected)
    {
        // Arrange
        var field = CreateBooleanField("consent");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Theory]
    [InlineData("no", "false")]
    [InlineData("NO", "false")]
    [InlineData("n", "false")]
    [InlineData("false", "false")]
    [InlineData("nope", "false")]
    public void Validate_FalseBoolean_ReturnsValid(string input, string expected)
    {
        // Arrange
        var field = CreateBooleanField("consent");

        // Act
        var result = _validator.Validate(field, input);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be(expected);
    }

    [Fact]
    public void Validate_InvalidBoolean_ReturnsInvalid()
    {
        // Arrange
        var field = CreateBooleanField("consent");
        var value = "maybe";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("yes or no");
    }

    #endregion

    #region Choice Validation

    [Fact]
    public void Validate_ValidChoice_ReturnsValid()
    {
        // Arrange
        var field = CreateChoiceField("color", ["Red", "Green", "Blue"]);
        var value = "green"; // Case insensitive

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeTrue();
        result.NormalizedValue.Should().Be("Green"); // Returns canonical form
    }

    [Fact]
    public void Validate_InvalidChoice_ReturnsInvalid()
    {
        // Arrange
        var field = CreateChoiceField("color", ["Red", "Green", "Blue"]);
        var value = "Yellow";

        // Act
        var result = _validator.Validate(field, value);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("one of");
    }

    #endregion

    #region Helper Methods

    private static FieldDefinition CreateTextField(string id, bool required = true, int? minLength = null, int? maxLength = null)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Text,
            Required = required,
            Constraints = new FieldConstraints
            {
                MinLength = minLength,
                MaxLength = maxLength
            }
        };
    }

    private static FieldDefinition CreateEmailField(string id, bool required = true)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Email,
            Required = required
        };
    }

    private static FieldDefinition CreatePhoneField(string id, bool required = true)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Phone,
            Required = required
        };
    }

    private static FieldDefinition CreateDateField(string id, bool required = true, string? dateMin = null, string? dateMax = null)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Date,
            Required = required,
            Constraints = new FieldConstraints
            {
                DateMin = dateMin,
                DateMax = dateMax
            }
        };
    }

    private static FieldDefinition CreateNumberField(string id, bool required = true, double? min = null, double? max = null)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Number,
            Required = required,
            Constraints = new FieldConstraints
            {
                Min = min,
                Max = max
            }
        };
    }

    private static FieldDefinition CreateBooleanField(string id, bool required = true)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Boolean,
            Required = required
        };
    }

    private static FieldDefinition CreateChoiceField(string id, List<string> choices, bool required = true)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = FieldType.Choice,
            Required = required,
            Constraints = new FieldConstraints
            {
                Choices = choices
            }
        };
    }

    #endregion
}
