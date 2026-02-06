using System.Collections;
using System.Diagnostics;
using Florence2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// Florence-2 vision service for image captioning and OCR.
///
/// Florence-2 is a small (~450MB) vision model from Microsoft that runs locally via ONNX.
/// It auto-downloads on first use to {ModelDirectory}/florence2/.
///
/// Capabilities:
/// - Image captioning: "A cat sitting on a red couch in a living room"
/// - OCR: Extracts visible text from photos, screenshots, signs
/// - Detailed captioning: Longer, more descriptive captions
///
/// Thread-safe: uses SemaphoreSlim for lazy model initialization.
/// </summary>
public class VisionService : IVisionService, IDisposable
{
    private readonly ILogger<VisionService> _logger;
    private readonly OcrNerConfig _config;
    private readonly SemaphoreSlim _modelLock = new(1, 1);

    private Florence2Model? _model;
    private bool _modelInitialized;

    public VisionService(
        ILogger<VisionService> logger,
        IOptions<OcrNerConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureModelLoadedAsync(ct);
            return _model != null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<VisionCaptionResult> CaptionAsync(
        string imagePath, bool detailed = true, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(imagePath))
                return new VisionCaptionResult
                {
                    Success = false,
                    Error = $"Image file not found: {imagePath}",
                    DurationMs = sw.ElapsedMilliseconds
                };

            await EnsureModelLoadedAsync(ct);

            if (_model == null)
                return new VisionCaptionResult
                {
                    Success = false,
                    Error = "Florence-2 model not loaded",
                    DurationMs = sw.ElapsedMilliseconds
                };

            using var imgStream = File.OpenRead(imagePath);
            var task = detailed ? TaskTypes.DETAILED_CAPTION : TaskTypes.CAPTION;
            var results = _model.Run(task, [imgStream], null, ct);
            var caption = ExtractText(results);

            // Clean up common Florence-2 artifacts
            caption = CleanCaption(caption);

            return new VisionCaptionResult
            {
                Success = !string.IsNullOrWhiteSpace(caption),
                Caption = caption,
                Error = string.IsNullOrWhiteSpace(caption) ? "No caption generated" : null,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Florence-2 captioning failed for {Path}", imagePath);
            return new VisionCaptionResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public async Task<VisionOcrResult> ExtractTextAsync(string imagePath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(imagePath))
                return new VisionOcrResult
                {
                    Success = false,
                    Error = $"Image file not found: {imagePath}",
                    DurationMs = sw.ElapsedMilliseconds
                };

            await EnsureModelLoadedAsync(ct);

            if (_model == null)
                return new VisionOcrResult
                {
                    Success = false,
                    Error = "Florence-2 model not loaded",
                    DurationMs = sw.ElapsedMilliseconds
                };

            using var imgStream = File.OpenRead(imagePath);
            var results = _model.Run(TaskTypes.OCR, [imgStream], null, ct);
            var text = ExtractText(results)?.Trim();

            return new VisionOcrResult
            {
                Success = !string.IsNullOrWhiteSpace(text),
                Text = text,
                Error = string.IsNullOrWhiteSpace(text) ? "No text detected" : null,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Florence-2 OCR failed for {Path}", imagePath);
            return new VisionOcrResult
            {
                Success = false,
                Error = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Download and load the Florence-2 model on first use.
    /// Uses FlorenceModelDownloader from the Florence2 NuGet package.
    /// </summary>
    private async Task EnsureModelLoadedAsync(CancellationToken ct)
    {
        if (_modelInitialized) return;

        await _modelLock.WaitAsync(ct);
        try
        {
            if (_modelInitialized) return;

            var modelsDir = Path.Combine(_config.ModelDirectory, "florence2");
            Directory.CreateDirectory(modelsDir);

            _logger.LogInformation("Loading Florence-2 models from {Path}. First run will download ~450MB.", modelsDir);

            var modelSource = new FlorenceModelDownloader(modelsDir);
            await modelSource.DownloadModelsAsync(
                status => _logger.LogDebug("Florence-2: {Status}", status),
                null,
                ct);

            _model = new Florence2Model(modelSource);
            _modelInitialized = true;

            _logger.LogInformation("Florence-2 models loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Florence-2 models");
            throw;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    /// <summary>
    /// Extract text from Florence-2 result objects.
    /// Florence-2 returns different types depending on the task.
    /// </summary>
    private string? ExtractText(object results)
    {
        if (results is string str) return str;

        try
        {
            var type = results.GetType();

            // Florence2 uses "PureText" for caption/OCR results
            var propertyNames = new[] { "PureText", "Text", "Caption", "Description", "Content" };
            foreach (var propName in propertyNames)
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var value = prop.GetValue(results)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }

            // If it's a collection, combine text from each item
            if (results is IEnumerable enumerable)
            {
                var texts = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        texts.Add(s);
                        continue;
                    }

                    var itemType = item.GetType();
                    foreach (var propName in propertyNames)
                    {
                        var itemProp = itemType.GetProperty(propName);
                        if (itemProp != null)
                        {
                            var value = itemProp.GetValue(item)?.ToString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                texts.Add(value);
                                break;
                            }
                        }
                    }
                }

                if (texts.Count > 0) return string.Join(" ", texts);
            }

            // Last resort
            var toString = results.ToString();
            if (!string.IsNullOrWhiteSpace(toString)
                && toString != type.Name
                && !toString.Contains("[]")
                && !toString.StartsWith("System."))
                return toString;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting text from Florence2 result");
        }

        return null;
    }

    /// <summary>
    /// Remove common Florence-2 caption artifacts like "The image shows" prefix
    /// </summary>
    private static string? CleanCaption(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) return null;

        var result = caption.Trim();

        var artifacts = new[] { "The image shows", "This image shows", "In this image,", "The picture shows" };
        foreach (var artifact in artifacts)
            if (result.StartsWith(artifact, StringComparison.OrdinalIgnoreCase))
                result = result[artifact.Length..].TrimStart(' ', ',');

        // Capitalize first letter
        if (result.Length > 0 && char.IsLower(result[0]))
            result = char.ToUpper(result[0]) + result[1..];

        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public void Dispose()
    {
        if (_model is IDisposable disposable)
            disposable.Dispose();
        _modelLock.Dispose();
    }
}
