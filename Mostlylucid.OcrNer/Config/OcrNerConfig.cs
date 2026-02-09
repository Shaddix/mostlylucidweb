namespace Mostlylucid.OcrNer.Config;

/// <summary>
/// Preprocessing intensity level for images before OCR.
/// Higher levels improve results on poor quality images but take longer.
/// </summary>
public enum PreprocessingLevel
{
    /// <summary>No preprocessing — pass image as-is</summary>
    None,

    /// <summary>Grayscale only — for already clean scans</summary>
    Minimal,

    /// <summary>Grayscale + contrast boost + light sharpen (recommended)</summary>
    Default,

    /// <summary>Strong contrast + sharpen + larger upscale — for poor quality photos</summary>
    Aggressive
}

/// <summary>
/// Configuration for OCR and NER services
/// </summary>
public class OcrNerConfig
{
    /// <summary>
    /// Directory where ONNX models and tessdata are cached.
    /// Defaults to {AppBaseDir}/models/ocrner/
    /// </summary>
    public string ModelDirectory { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "models", "ocrner");

    /// <summary>
    /// Enable OCR (requires Tesseract native libraries).
    /// When false, only NER-from-text is available.
    /// </summary>
    public bool EnableOcr { get; set; } = true;

    /// <summary>
    /// Tesseract language code (e.g. "eng", "fra", "deu")
    /// </summary>
    public string TesseractLanguage { get; set; } = "eng";

    /// <summary>
    /// Maximum sequence length for BERT NER model (tokens).
    /// Texts longer than this are chunked.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 512;

    /// <summary>
    /// Minimum confidence threshold for NER predictions.
    /// Entities below this threshold are filtered out.
    /// </summary>
    public float MinConfidence { get; set; } = 0.5f;

    /// <summary>
    /// HuggingFace model repository for BERT NER ONNX model
    /// </summary>
    public string NerModelRepo { get; set; } = "protectai/bert-base-NER-onnx";

    /// <summary>
    /// URL template for tessdata downloads.
    /// {0} is replaced with the language code.
    /// </summary>
    public string TessdataUrlTemplate { get; set; } =
        "https://github.com/tesseract-ocr/tessdata/raw/main/{0}.traineddata";

    /// <summary>
    /// Image preprocessing level before OCR.
    /// Controls grayscale, contrast, sharpening, and upscaling.
    /// Default is recommended for most images.
    /// </summary>
    public PreprocessingLevel Preprocessing { get; set; } = PreprocessingLevel.Default;

    /// <summary>
    /// Enable advanced OpenCV-based preprocessing (deskew, denoise, binarization).
    /// When true, uses OpenCvPreprocessor instead of the basic ImageSharp preprocessor.
    /// Requires OpenCvSharp4 runtime libraries.
    /// Default: false
    /// </summary>
    public bool EnableAdvancedPreprocessing { get; set; } = false;

    /// <summary>
    /// Enable rule-based entity extraction using Microsoft.Recognizers.Text.
    /// Extracts dates, numbers, URLs, phone numbers, emails, and IP addresses.
    /// Default: false
    /// </summary>
    public bool EnableRecognizers { get; set; } = false;

    /// <summary>
    /// Culture/language for Microsoft.Recognizers.Text extraction.
    /// Supported: en-us, en-gb, es-es, fr-fr, de-de, pt-br, zh-cn, ja-jp, etc.
    /// Default: "en-us"
    /// </summary>
    public string RecognizerCulture { get; set; } = "en-us";
}
