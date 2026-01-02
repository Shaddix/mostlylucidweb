namespace Mostlylucid.VoiceForm.Config;

public class VoiceFormConfig : IConfigSection
{
    public static string Section => "VoiceForm";

    /// <summary>
    /// Path to form schema files
    /// </summary>
    public string FormsPath { get; set; } = "Data/SampleForms";

    /// <summary>
    /// SQLite database path for event log
    /// </summary>
    public string EventLogDbPath { get; set; } = "data/voiceform-events.db";

    /// <summary>
    /// Default confidence threshold for auto-confirmation (0.0-1.0)
    /// </summary>
    public double DefaultConfidenceThreshold { get; set; } = 0.85;

    /// <summary>
    /// Maximum recording duration in seconds
    /// </summary>
    public int MaxRecordingSeconds { get; set; } = 30;

    /// <summary>
    /// Whisper STT service configuration
    /// </summary>
    public WhisperConfig Whisper { get; set; } = new();

    /// <summary>
    /// Ollama field extraction configuration
    /// </summary>
    public OllamaExtractorConfig Ollama { get; set; } = new();
}

public class WhisperConfig
{
    /// <summary>
    /// Base URL for Whisper Docker service
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whisper model to use (tiny, base, small, medium, large)
    /// </summary>
    public string Model { get; set; } = "base.en";
}

public class OllamaExtractorConfig
{
    /// <summary>
    /// Base URL for Ollama service
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use for extraction
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Temperature for LLM (low for deterministic extraction)
    /// </summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
