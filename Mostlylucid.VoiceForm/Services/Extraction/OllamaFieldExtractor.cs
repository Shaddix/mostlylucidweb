using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.Extraction;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Extraction;

/// <summary>
/// Ollama-based field extractor.
/// Uses a constrained prompt to extract field values as strict JSON.
/// The LLM is a translator - it never controls flow or makes decisions.
/// </summary>
public class OllamaFieldExtractor : IFieldExtractor
{
    private readonly HttpClient _httpClient;
    private readonly VoiceFormConfig _config;
    private readonly ILogger<OllamaFieldExtractor> _logger;

    public OllamaFieldExtractor(
        HttpClient httpClient,
        VoiceFormConfig config,
        ILogger<OllamaFieldExtractor> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<ExtractionResponse> ExtractAsync(
        ExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting field '{FieldId}' from transcript: '{Transcript}'",
            context.Field.Id, context.Transcript);

        var prompt = BuildExtractionPrompt(context);

        try
        {
            var request = new OllamaGenerateRequest
            {
                Model = _config.Ollama.Model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = _config.Ollama.Temperature,
                    NumPredict = 256 // Limit output length
                },
                Format = "json" // Request JSON output
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (ollamaResponse?.Response == null)
            {
                _logger.LogWarning("Ollama returned null response");
                return CreateFailedExtraction(context.Field.Id, "LLM returned empty response");
            }

            return ParseExtractionResponse(context.Field.Id, ollamaResponse.Response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract field '{FieldId}'", context.Field.Id);
            return CreateFailedExtraction(context.Field.Id, $"Extraction failed: {ex.Message}");
        }
    }

    private string BuildExtractionPrompt(ExtractionContext context)
    {
        var field = context.Field;
        var constraintsText = BuildConstraintsText(field);
        var examplesText = field.Examples?.Count > 0
            ? string.Join(", ", field.Examples.Select(e => $"\"{e}\""))
            : "none provided";

        // This is the ONLY thing the LLM sees. It cannot control anything else.
        return $$"""
            You are a form field extractor. Extract the requested field value from the transcript.

            FIELD DEFINITION:
            - ID: {{field.Id}}
            - Type: {{field.Type}}
            - Label: {{field.Label}}
            - Constraints: {{constraintsText}}
            - Examples of valid values: {{examplesText}}

            CURRENT PROMPT ASKED: "{{context.Prompt}}"

            TRANSCRIPT: "{{context.Transcript}}"

            Extract the field value and respond with ONLY this JSON (no markdown, no explanation):
            {
              "fieldId": "{{field.Id}}",
              "value": "<extracted value or null if not found>",
              "confidence": <0.0 to 1.0>,
              "needsConfirmation": <true if ambiguous>,
              "reason": "<brief reason if needsConfirmation is true, else null>"
            }

            RULES:
            - Extract ONLY the requested field
            - Normalize dates to ISO 8601 format (YYYY-MM-DD)
            - Normalize phone numbers to digits only
            - Normalize emails to lowercase
            - Set value to null if you cannot extract the field
            - Set confidence based on how clear the extraction was
            - Set needsConfirmation=true if: ambiguous, multiple interpretations, or natural language parsing was required
            """;
    }

    private static string BuildConstraintsText(FieldDefinition field)
    {
        var parts = new List<string>();

        if (field.Constraints != null)
        {
            if (field.Constraints.MinLength.HasValue)
                parts.Add($"min length: {field.Constraints.MinLength}");
            if (field.Constraints.MaxLength.HasValue)
                parts.Add($"max length: {field.Constraints.MaxLength}");
            if (field.Constraints.Min.HasValue)
                parts.Add($"min value: {field.Constraints.Min}");
            if (field.Constraints.Max.HasValue)
                parts.Add($"max value: {field.Constraints.Max}");
            if (!string.IsNullOrEmpty(field.Constraints.Pattern))
                parts.Add($"pattern: {field.Constraints.Pattern}");
            if (field.Constraints.Choices?.Count > 0)
                parts.Add($"choices: {string.Join(", ", field.Constraints.Choices)}");
            if (!string.IsNullOrEmpty(field.Constraints.DateMin))
                parts.Add($"date min: {field.Constraints.DateMin}");
            if (!string.IsNullOrEmpty(field.Constraints.DateMax))
                parts.Add($"date max: {field.Constraints.DateMax}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "none";
    }

    private ExtractionResponse ParseExtractionResponse(string fieldId, string llmResponse)
    {
        try
        {
            // Try to extract JSON from the response (handle potential markdown wrapping)
            var jsonStart = llmResponse.IndexOf('{');
            var jsonEnd = llmResponse.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("Could not find JSON in LLM response: {Response}", llmResponse);
                return CreateFailedExtraction(fieldId, "Invalid JSON response from LLM");
            }

            var json = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var parsed = JsonSerializer.Deserialize<LlmExtractionResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed == null)
            {
                return CreateFailedExtraction(fieldId, "Failed to parse JSON response");
            }

            _logger.LogInformation("Extracted '{Value}' with confidence {Confidence:F2}",
                parsed.Value, parsed.Confidence);

            return new ExtractionResponse(
                FieldId: fieldId,
                Value: parsed.Value,
                Confidence: Math.Clamp(parsed.Confidence, 0.0, 1.0),
                NeedsConfirmation: parsed.NeedsConfirmation,
                Reason: parsed.Reason
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON: {Response}", llmResponse);
            return CreateFailedExtraction(fieldId, "Invalid JSON response from LLM");
        }
    }

    private static ExtractionResponse CreateFailedExtraction(string fieldId, string reason)
    {
        return new ExtractionResponse(
            FieldId: fieldId,
            Value: null,
            Confidence: 0.0,
            NeedsConfirmation: false,
            Reason: reason
        );
    }

    // Ollama API models
    private class OllamaGenerateRequest
    {
        public string Model { get; set; } = "";
        public string Prompt { get; set; } = "";
        public bool Stream { get; set; }
        public OllamaOptions? Options { get; set; }
        public string? Format { get; set; }
    }

    private class OllamaOptions
    {
        public double Temperature { get; set; }
        public int NumPredict { get; set; }
    }

    private class OllamaGenerateResponse
    {
        public string? Response { get; set; }
        public bool Done { get; set; }
    }

    private class LlmExtractionResponse
    {
        public string? FieldId { get; set; }
        public string? Value { get; set; }
        public double Confidence { get; set; }
        public bool NeedsConfirmation { get; set; }
        public string? Reason { get; set; }
    }
}
