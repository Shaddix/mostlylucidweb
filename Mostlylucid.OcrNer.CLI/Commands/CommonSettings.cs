using System.ComponentModel;
using Spectre.Console.Cli;

namespace Mostlylucid.OcrNer.CLI.Commands;

/// <summary>
/// Base settings shared by all commands.
/// Exposes every OcrNerConfig option as a CLI flag so users can override defaults.
/// </summary>
public abstract class CommonSettings : CommandSettings
{
    [CommandOption("-q|--quiet")]
    [Description("Minimal output — no banners or progress bars")]
    public bool Quiet { get; init; }

    [CommandOption("-o|--output")]
    [Description("Output file path (.txt, .md, .json). Omit for console output")]
    public string? Output { get; init; }

    [CommandOption("--model-dir")]
    [Description("Directory for cached models (default: ./models/ocrner/)")]
    public string? ModelDirectory { get; init; }

    [CommandOption("-c|--confidence")]
    [Description("Minimum NER confidence threshold, 0.0-1.0 (default: 0.5)")]
    public float? MinConfidence { get; init; }

    [CommandOption("--language")]
    [Description("Tesseract OCR language code (default: eng)")]
    public string? Language { get; init; }

    [CommandOption("--max-tokens")]
    [Description("Max BERT token sequence length (default: 512)")]
    public int? MaxTokens { get; init; }

    [CommandOption("-p|--preprocess")]
    [Description("Image preprocessing: none, minimal, default, aggressive (default: default)")]
    public string? Preprocess { get; init; }
}
