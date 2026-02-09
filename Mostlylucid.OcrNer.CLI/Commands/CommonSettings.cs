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

    [CommandOption("--json")]
    [Description("Output structured JSON to stdout (implies --quiet, suppresses all logging)")]
    public bool Json { get; init; }

    /// <summary>
    /// Effective quiet mode — true when either --quiet or --json is set.
    /// </summary>
    public bool EffectiveQuiet => Quiet || Json;

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

    [CommandOption("-a|--advanced-preprocess")]
    [Description("Use advanced OpenCV preprocessing (deskew, denoise, binarize)")]
    public bool AdvancedPreprocess { get; init; }

    [CommandOption("-r|--recognizers")]
    [Description("Enable rule-based entity extraction (dates, numbers, URLs, phones, emails, IPs)")]
    public bool Recognizers { get; init; }

    [CommandOption("--culture")]
    [Description("Culture for recognizers, e.g. en-us, de-de (default: en-us)")]
    public string? Culture { get; init; }
}
