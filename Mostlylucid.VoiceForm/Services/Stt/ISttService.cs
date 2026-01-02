using Mostlylucid.VoiceForm.Models.Extraction;

namespace Mostlylucid.VoiceForm.Services.Stt;

/// <summary>
/// Abstraction for speech-to-text services.
/// Treats STT as a tool that produces potentially lossy output.
/// </summary>
public interface ISttService
{
    /// <summary>
    /// Transcribe audio data to text
    /// </summary>
    /// <param name="audioData">Raw audio bytes</param>
    /// <param name="audioFormat">Audio format (wav, mp3, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcription result with confidence</returns>
    Task<SttResult> TranscribeAsync(
        byte[] audioData,
        string audioFormat = "wav",
        CancellationToken cancellationToken = default);
}
