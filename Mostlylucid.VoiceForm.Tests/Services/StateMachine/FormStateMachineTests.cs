using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;
using Mostlylucid.VoiceForm.Models.State;
using Mostlylucid.VoiceForm.Services.StateMachine;
using Xunit;

namespace Mostlylucid.VoiceForm.Tests.Services.StateMachine;

public class FormStateMachineTests
{
    private readonly FormStateMachine _stateMachine;

    public FormStateMachineTests()
    {
        var logger = new Mock<ILogger<FormStateMachine>>();
        _stateMachine = new FormStateMachine(logger.Object);
    }

    #region StartSession Tests

    [Fact]
    public void StartSession_WithValidForm_CreatesSessionWithCorrectState()
    {
        // Arrange
        var form = CreateTestForm();

        // Act
        var session = _stateMachine.StartSession(form);

        // Assert
        session.Should().NotBeNull();
        session.Form.Should().Be(form);
        session.Status.Should().Be(FormStatus.InProgress);
        session.CurrentFieldIndex.Should().Be(0);
        session.FieldStates.Should().HaveCount(2);
    }

    [Fact]
    public void StartSession_FirstFieldIsMarkedInProgress()
    {
        // Arrange
        var form = CreateTestForm();

        // Act
        var session = _stateMachine.StartSession(form);

        // Assert
        var firstFieldState = session.GetFieldState("name");
        firstFieldState.Status.Should().Be(FieldStatus.InProgress);
    }

    #endregion

    #region GetCurrentField Tests

    [Fact]
    public void GetCurrentField_ReturnsFirstFieldAfterStart()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // Act
        var currentField = _stateMachine.GetCurrentField();

        // Assert
        currentField.Should().NotBeNull();
        currentField!.Id.Should().Be("name");
    }

    [Fact]
    public void GetCurrentField_ReturnsNullWhenNoSession()
    {
        // Act
        var currentField = _stateMachine.GetCurrentField();

        // Assert
        currentField.Should().BeNull();
    }

    #endregion

    #region ProcessExtraction Tests

    [Fact]
    public void ProcessExtraction_ValidValueNoConfirmation_AutoConfirmsAndMovesToNextField()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", "John Smith", 0.95, false, null);
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);

        // Act
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiresConfirmation.Should().BeFalse();

        var nameState = result.Session.GetFieldState("name");
        nameState.Status.Should().Be(FieldStatus.Confirmed);
        nameState.Value.Should().Be("John Smith");

        // Should move to next field
        _stateMachine.GetCurrentField()!.Id.Should().Be("email");
    }

    [Fact]
    public void ProcessExtraction_ValidValueWithConfirmation_SetsAwaitingConfirmation()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", "John Smith", 0.75, true, "Low confidence");
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(true, ConfirmationReason.LowConfidence);

        // Act
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiresConfirmation.Should().BeTrue();
        result.PendingValue.Should().Be("John Smith");

        var nameState = result.Session.GetFieldState("name");
        nameState.Status.Should().Be(FieldStatus.AwaitingConfirmation);
        nameState.PendingValue.Should().Be("John Smith");
    }

    [Fact]
    public void ProcessExtraction_ValidationFailed_StaysInProgress()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", "J", 0.95, false, null);
        var validation = new ValidationResult(false, "Too short");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);

        // Act
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Too short");

        var nameState = result.Session.GetFieldState("name");
        nameState.Status.Should().Be(FieldStatus.InProgress);
    }

    [Fact]
    public void ProcessExtraction_NullValue_StaysInProgress()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", null, 0.0, false, "Could not extract");
        var validation = new ValidationResult(false, "Required");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);

        // Act
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Could not extract");
    }

    [Fact]
    public void ProcessExtraction_UsesNormalizedValue()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", "  john smith  ", 0.95, false, null);
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);

        // Act
        var result = _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Assert
        var nameState = result.Session.GetFieldState("name");
        nameState.Value.Should().Be("John Smith"); // Normalized, not original
    }

    #endregion

    #region ConfirmValue Tests

    [Fact]
    public void ConfirmValue_WhenAwaitingConfirmation_ConfirmsAndMovesToNextField()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // First, get to awaiting confirmation state
        var extraction = new ExtractionResponse("name", "John Smith", 0.75, true, null);
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(true, ConfirmationReason.LowConfidence);
        _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Act
        var result = _stateMachine.ConfirmValue();

        // Assert
        result.Success.Should().BeTrue();

        var nameState = result.Session.GetFieldState("name");
        nameState.Status.Should().Be(FieldStatus.Confirmed);
        nameState.Value.Should().Be("John Smith");
        nameState.PendingValue.Should().BeNull();

        // Should move to next field
        _stateMachine.GetCurrentField()!.Id.Should().Be("email");
    }

    [Fact]
    public void ConfirmValue_WhenNotAwaitingConfirmation_ReturnsFailure()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // Act (no extraction was done, still in progress)
        var result = _stateMachine.ConfirmValue();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No value awaiting confirmation");
    }

    #endregion

    #region RejectValue Tests

    [Fact]
    public void RejectValue_WhenAwaitingConfirmation_GoesBackToInProgress()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // First, get to awaiting confirmation state
        var extraction = new ExtractionResponse("name", "John Smith", 0.75, true, null);
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(true, ConfirmationReason.LowConfidence);
        _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Act
        var result = _stateMachine.RejectValue();

        // Assert
        result.Success.Should().BeTrue();

        var nameState = result.Session.GetFieldState("name");
        nameState.Status.Should().Be(FieldStatus.InProgress);
        nameState.PendingValue.Should().BeNull();

        // Should still be on same field
        _stateMachine.GetCurrentField()!.Id.Should().Be("name");
    }

    [Fact]
    public void RejectValue_ReturnsReprompt()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        var extraction = new ExtractionResponse("name", "John Smith", 0.75, true, null);
        var validation = new ValidationResult(true, NormalizedValue: "John Smith");
        var confirmation = new ConfirmationDecision(true, ConfirmationReason.LowConfidence);
        _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Act
        var result = _stateMachine.RejectValue();

        // Assert
        result.Message.Should().Be("Please say your name again"); // Reprompt from form
    }

    #endregion

    #region SkipField Tests

    [Fact]
    public void SkipField_OptionalField_SkipsAndMovesToNext()
    {
        // Arrange
        var form = CreateFormWithOptionalField();
        _stateMachine.StartSession(form);

        // Move to optional field
        var extraction = new ExtractionResponse("name", "John", 0.95, false, null);
        var validation = new ValidationResult(true, NormalizedValue: "John");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);
        _stateMachine.ProcessExtraction(extraction, validation, confirmation);

        // Now on optional "nickname" field

        // Act
        var result = _stateMachine.SkipField();

        // Assert
        result.Success.Should().BeTrue();

        var nicknameState = result.Session.GetFieldState("nickname");
        nicknameState.Status.Should().Be(FieldStatus.Skipped);
    }

    [Fact]
    public void SkipField_RequiredField_ReturnsFailure()
    {
        // Arrange
        var form = CreateTestForm(); // All fields required
        _stateMachine.StartSession(form);

        // Act
        var result = _stateMachine.SkipField();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    #endregion

    #region IsComplete Tests

    [Fact]
    public void IsComplete_WhenAllFieldsConfirmed_ReturnsTrue()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // Complete first field
        var extraction1 = new ExtractionResponse("name", "John", 0.95, false, null);
        var validation1 = new ValidationResult(true, NormalizedValue: "John");
        var confirmation1 = new ConfirmationDecision(false, ConfirmationReason.None);
        _stateMachine.ProcessExtraction(extraction1, validation1, confirmation1);

        // Complete second field
        var extraction2 = new ExtractionResponse("email", "john@example.com", 0.95, false, null);
        var validation2 = new ValidationResult(true, NormalizedValue: "john@example.com");
        var confirmation2 = new ConfirmationDecision(false, ConfirmationReason.None);
        _stateMachine.ProcessExtraction(extraction2, validation2, confirmation2);

        // Act
        var isComplete = _stateMachine.IsComplete();

        // Assert
        isComplete.Should().BeTrue();
        _stateMachine.CurrentSession!.Status.Should().Be(FormStatus.Completed);
    }

    [Fact]
    public void IsComplete_WhenFieldsPending_ReturnsFalse()
    {
        // Arrange
        var form = CreateTestForm();
        _stateMachine.StartSession(form);

        // Act
        var isComplete = _stateMachine.IsComplete();

        // Assert
        isComplete.Should().BeFalse();
    }

    #endregion

    #region Determinism Tests

    [Fact]
    public void StateMachine_SameInputsProduceSameOutputs_IsDeterministic()
    {
        // This test verifies the "Ten Commandments" principle:
        // Same inputs always produce the same state transitions

        // Run 1
        var logger1 = new Mock<ILogger<FormStateMachine>>();
        var sm1 = new FormStateMachine(logger1.Object);
        var form1 = CreateTestForm();
        sm1.StartSession(form1);

        var extraction = new ExtractionResponse("name", "John", 0.85, false, null);
        var validation = new ValidationResult(true, NormalizedValue: "John");
        var confirmation = new ConfirmationDecision(false, ConfirmationReason.None);
        var result1 = sm1.ProcessExtraction(extraction, validation, confirmation);

        // Run 2 - identical inputs
        var logger2 = new Mock<ILogger<FormStateMachine>>();
        var sm2 = new FormStateMachine(logger2.Object);
        var form2 = CreateTestForm();
        sm2.StartSession(form2);

        var result2 = sm2.ProcessExtraction(extraction, validation, confirmation);

        // Assert - results must be identical
        result1.Success.Should().Be(result2.Success);
        result1.RequiresConfirmation.Should().Be(result2.RequiresConfirmation);
        result1.Session.GetFieldState("name").Status.Should().Be(result2.Session.GetFieldState("name").Status);
        result1.Session.GetFieldState("name").Value.Should().Be(result2.Session.GetFieldState("name").Value);
    }

    #endregion

    #region Helper Methods

    private static FormDefinition CreateTestForm()
    {
        return new FormDefinition
        {
            Id = "test-form",
            Name = "Test Form",
            Fields =
            [
                new FieldDefinition
                {
                    Id = "name",
                    Label = "Name",
                    Prompt = "What is your name?",
                    Reprompt = "Please say your name again",
                    Type = FieldType.Text,
                    Required = true
                },

                new FieldDefinition
                {
                    Id = "email",
                    Label = "Email",
                    Prompt = "What is your email?",
                    Type = FieldType.Email,
                    Required = true
                }
            ]
        };
    }

    private static FormDefinition CreateFormWithOptionalField()
    {
        return new FormDefinition
        {
            Id = "test-form",
            Name = "Test Form",
            Fields =
            [
                new FieldDefinition
                {
                    Id = "name",
                    Label = "Name",
                    Prompt = "What is your name?",
                    Type = FieldType.Text,
                    Required = true
                },

                new FieldDefinition
                {
                    Id = "nickname",
                    Label = "Nickname",
                    Prompt = "What is your nickname?",
                    Type = FieldType.Text,
                    Required = false // Optional
                },

                new FieldDefinition
                {
                    Id = "email",
                    Label = "Email",
                    Prompt = "What is your email?",
                    Type = FieldType.Email,
                    Required = true
                }
            ]
        };
    }

    #endregion
}
