using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.VoiceForm.Config;
using Mostlylucid.VoiceForm.Models.Extraction;

namespace Mostlylucid.VoiceForm.Services.Stt;

/// <summary>
/// Whisper STT implementation using the openai-whisper-asr-webservice Docker image.
/// API: POST /asr with multipart form data
/// </summary>
public class WhisperDockerService : ISttService
{
    private readonly HttpClient _httpClient;
    private readonly VoiceFormConfig _config;
    private readonly ILogger<WhisperDockerService> _logger;

    public WhisperDockerService(
        HttpClient httpClient,
        VoiceFormConfig config,
        ILogger<WhisperDockerService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<SttResult> TranscribeAsync(
        byte[] audioData,
        string audioFormat = "wav",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting transcription of {Bytes} bytes of {Format} audio",
            audioData.Length, audioFormat);

        var startTime = DateTime.UtcNow;

        try
        {
            using var content = new MultipartFormDataContent();

            // Add audio file
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(audioFormat));
            content.Add(audioContent, "audio_file", $"audio.{audioFormat}");

            // Add parameters
            content.Add(new StringContent("json"), "output");
            content.Add(new StringContent("transcribe"), "task");
            content.Add(new StringContent("en"), "language");

            var response = await _httpClient.PostAsync("/asr", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<WhisperResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var duration = DateTime.UtcNow - startTime;

            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogWarning("Whisper returned empty or null result");
                return new SttResult(
                    Transcript: "",
                    Confidence: 0.0,
                    Duration: duration,
                    RawResponse: responseJson
                );
            }

            // Whisper doesn't provide per-segment confidence in the simple API,
            // so we use a heuristic based on response characteristics
            var confidence = EstimateConfidence(result);

            _logger.LogInformation("Transcription complete: '{Transcript}' (confidence: {Confidence:F2})",
                result.Text, confidence);

            return new SttResult(
                Transcript: result.Text.Trim(),
                Confidence: confidence,
                Duration: duration,
                Language: result.Language,
                RawResponse: responseJson
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe audio");
            throw;
        }
    }

    private static string GetMimeType(string format) => format.ToLowerInvariant() switch
    {
        "wav" => "audio/wav",
        "mp3" => "audio/mpeg",
        "ogg" => "audio/ogg",
        "webm" => "audio/webm",
        "m4a" => "audio/m4a",
        "flac" => "audio/flac",
        _ => "application/octet-stream"
    };

    private static double EstimateConfidence(WhisperResponse response)
    {
        // Heuristics for confidence when not provided:
        // - Longer, coherent text = higher confidence
        // - Very short text or "[BLANK_AUDIO]" = lower confidence
        // - Detected language matching expected = higher confidence

        if (string.IsNullOrWhiteSpace(response.Text))
            return 0.0;

        if (response.Text.Contains("[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
            return 0.1;

        var wordCount = response.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Base confidence on word count (more words = more confident in detection)
        var confidence = wordCount switch
        {
            0 => 0.1,
            1 => 0.6,
            2 => 0.75,
            3 or 4 => 0.85,
            _ => 0.9
        };

        // If we have segments with individual confidence, use those
        if (response.Segments is { Count: > 0 })
        {
            var avgSegmentConfidence = response.Segments
                .Where(s => s.AvgLogprob.HasValue)
                .Select(s => Math.Exp(s.AvgLogprob!.Value)) // Convert log probability
                .DefaultIfEmpty(confidence)
                .Average();

            confidence = avgSegmentConfidence;
        }

        return Math.Clamp(confidence, 0.0, 1.0);
    }

    private class WhisperResponse
    {
        public string? Text { get; set; }
        public string? Language { get; set; }
        public List<WhisperSegment>? Segments { get; set; }
    }

    private class WhisperSegment
    {
        public double? Start { get; set; }
        public double? End { get; set; }
        public string? Text { get; set; }
        public double? AvgLogprob { get; set; }
    }
}
