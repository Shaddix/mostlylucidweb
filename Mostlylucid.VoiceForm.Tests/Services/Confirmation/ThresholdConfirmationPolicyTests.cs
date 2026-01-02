using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Services.Confirmation;
using Xunit;

namespace Mostlylucid.VoiceForm.Tests.Services.Confirmation;

public class ThresholdConfirmationPolicyTests
{
    private readonly ThresholdConfirmationPolicy _policy;
    private readonly VoiceFormConfig _config;

    public ThresholdConfirmationPolicyTests()
    {
        var logger = new Mock<ILogger<ThresholdConfirmationPolicy>>();
        _config = new VoiceFormConfig
        {
            DefaultConfidenceThreshold = 0.85
        };
        _policy = new ThresholdConfirmationPolicy(_config, logger.Object);
    }

    #region AlwaysConfirm Tests

    [Fact]
    public void ShouldConfirm_FieldAlwaysConfirm_ReturnsTrue()
    {
        // Arrange
        var field = CreateField("dob", FieldType.Date, alwaysConfirm: true);
        var extraction = new ExtractionResponse("dob", "1985-05-12", 0.99, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.FieldPolicyRequires);
    }

    #endregion

    #region Confidence Threshold Tests

    [Fact]
    public void ShouldConfirm_BelowThreshold_ReturnsTrue()
    {
        // Arrange
        var field = CreateField("name", FieldType.Text);
        var extraction = new ExtractionResponse("name", "John", 0.70, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.LowConfidence);
    }

    [Fact]
    public void ShouldConfirm_AboveThreshold_ReturnsFalse()
    {
        // Arrange
        var field = CreateField("name", FieldType.Text);
        var extraction = new ExtractionResponse("name", "John", 0.95, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeFalse();
        decision.Reason.Should().Be(ConfirmationReason.None);
    }

    [Fact]
    public void ShouldConfirm_CustomFieldThreshold_UsesFieldThreshold()
    {
        // Arrange
        var field = CreateField("name", FieldType.Text, confidenceThreshold: 0.95);
        var extraction = new ExtractionResponse("name", "John", 0.90, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        // 0.90 is above default 0.85 but below field-specific 0.95
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.LowConfidence);
    }

    #endregion

    #region LLM Ambiguity Tests

    [Fact]
    public void ShouldConfirm_LlmFlaggedAmbiguous_BorderlineConfidence_ReturnsTrue()
    {
        // Arrange
        var field = CreateField("name", FieldType.Text);
        // Confidence is above threshold (0.85) but within margin (0.95)
        var extraction = new ExtractionResponse("name", "John", 0.90, true, "Ambiguous");
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.AmbiguousExtraction);
    }

    [Fact]
    public void ShouldConfirm_LlmFlaggedAmbiguous_HighConfidence_ReturnsFalse()
    {
        // Arrange
        var field = CreateField("name", FieldType.Text);
        // Confidence is way above threshold + margin
        var extraction = new ExtractionResponse("name", "John", 0.99, true, "LLM thinks ambiguous but it's not");
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        // Policy ignores LLM's ambiguity flag when confidence is very high
        // This demonstrates "Policy decides, not LLM"
        decision.RequiresConfirmation.Should().BeFalse();
    }

    #endregion

    #region Natural Language Parsing Tests

    [Theory]
    [InlineData("Date parsed from natural language")]
    [InlineData("Inferred from context")]
    [InlineData("natural language interpretation")]
    public void ShouldConfirm_NaturalLanguageParsing_ReturnsTrue(string reason)
    {
        // Arrange
        var field = CreateField("dob", FieldType.Date);
        var extraction = new ExtractionResponse("dob", "1985-05-12", 0.95, false, reason);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.NaturalLanguageParsed);
    }

    [Fact]
    public void ShouldConfirm_NaturalLanguageParsing_DisabledByPolicy_ReturnsFalse()
    {
        // Arrange
        var field = CreateField("dob", FieldType.Date, confirmNaturalLanguage: false);
        var extraction = new ExtractionResponse("dob", "1985-05-12", 0.95, false, "Date parsed from natural language");
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeFalse();
    }

    #endregion

    #region High Value Field Tests

    [Theory]
    [InlineData(FieldType.Email)]
    [InlineData(FieldType.Date)]
    public void ShouldConfirm_HighValueField_ModerateConfidence_ReturnsTrue(FieldType fieldType)
    {
        // Arrange
        var field = CreateField("sensitive", fieldType);
        // 0.90 is above default threshold but below 0.95 for high-value fields
        var extraction = new ExtractionResponse("sensitive", "value", 0.90, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.HighValueField);
    }

    [Fact]
    public void ShouldConfirm_HighValueField_VeryHighConfidence_ReturnsFalse()
    {
        // Arrange
        var field = CreateField("email", FieldType.Email);
        var extraction = new ExtractionResponse("email", "test@example.com", 0.98, false, null);
        var validation = new ValidationResult(true);

        // Act
        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // Assert
        decision.RequiresConfirmation.Should().BeFalse();
    }

    #endregion

    #region Determinism Tests

    [Fact]
    public void ShouldConfirm_SameInputs_ProduceSameOutputs()
    {
        // This verifies the "Ten Commandments" principle:
        // Confirmation is a deterministic rule, not LLM judgment

        var field = CreateField("name", FieldType.Text);
        var extraction = new ExtractionResponse("name", "John", 0.80, true, "Maybe ambiguous");
        var validation = new ValidationResult(true);

        // Call multiple times
        var decision1 = _policy.ShouldConfirm(field, extraction, validation);
        var decision2 = _policy.ShouldConfirm(field, extraction, validation);
        var decision3 = _policy.ShouldConfirm(field, extraction, validation);

        // All should be identical
        decision1.RequiresConfirmation.Should().Be(decision2.RequiresConfirmation);
        decision2.RequiresConfirmation.Should().Be(decision3.RequiresConfirmation);
        decision1.Reason.Should().Be(decision2.Reason);
        decision2.Reason.Should().Be(decision3.Reason);
    }

    [Fact]
    public void ShouldConfirm_PolicyDecides_NotLlm()
    {
        // Demonstrate that even when LLM says "no confirmation needed",
        // policy can override based on rules

        var field = CreateField("dob", FieldType.Date, alwaysConfirm: true);

        // LLM says high confidence, no confirmation needed
        var extraction = new ExtractionResponse("dob", "1985-05-12", 0.99, false, null);
        var validation = new ValidationResult(true);

        var decision = _policy.ShouldConfirm(field, extraction, validation);

        // But policy says confirm anyway because alwaysConfirm=true
        decision.RequiresConfirmation.Should().BeTrue();
        decision.Reason.Should().Be(ConfirmationReason.FieldPolicyRequires);
    }

    #endregion

    #region Helper Methods

    private static FieldDefinition CreateField(
        string id,
        FieldType type,
        bool alwaysConfirm = false,
        double? confidenceThreshold = null,
        bool confirmNaturalLanguage = true)
    {
        return new FieldDefinition
        {
            Id = id,
            Label = id,
            Prompt = $"Enter {id}",
            Type = type,
            Required = true,
            ConfirmationPolicy = new ConfirmationPolicy
            {
                AlwaysConfirm = alwaysConfirm,
                ConfidenceThreshold = confidenceThreshold ?? 0.85,
                ConfirmNaturalLanguageParsing = confirmNaturalLanguage
            }
        };
    }

    #endregion
}
