using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.FormSchema;

namespace Mostlylucid.VoiceForm.Services.Orchestration;

/// <summary>
/// Loads form schemas from JSON files
/// </summary>
public class JsonFormSchemaLoader : IFormSchemaLoader
{
    private readonly string _formsPath;
    private readonly ILogger<JsonFormSchemaLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFormSchemaLoader(VoiceFormConfig config, ILogger<JsonFormSchemaLoader> logger)
    {
        _formsPath = config.FormsPath;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public Task<IReadOnlyList<string>> GetFormIdsAsync(CancellationToken cancellationToken = default)
    {
        var formIds = new List<string>();

        if (!Directory.Exists(_formsPath))
        {
            _logger.LogWarning("Forms directory does not exist: {Path}", _formsPath);
            return Task.FromResult<IReadOnlyList<string>>(formIds);
        }

        foreach (var file in Directory.GetFiles(_formsPath, "*.json"))
        {
            formIds.Add(Path.GetFileNameWithoutExtension(file));
        }

        return Task.FromResult<IReadOnlyList<string>>(formIds);
    }

    public async Task<FormDefinition?> LoadFormAsync(string formId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_formsPath, $"{formId}.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Form file not found: {Path}", filePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var form = JsonSerializer.Deserialize<FormDefinition>(json, _jsonOptions);

            if (form != null)
            {
                _logger.LogInformation("Loaded form '{FormId}' with {FieldCount} fields",
                    form.Id, form.Fields.Count);
            }

            return form;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load form '{FormId}'", formId);
            return null;
        }
    }
}
