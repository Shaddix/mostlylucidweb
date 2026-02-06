using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mostlylucid.OcrNer.Services.Preprocessing;

/// <summary>
/// Simple image preprocessor using ImageSharp to prepare images for Tesseract OCR.
///
/// Tesseract works best with:
/// - Grayscale images (reduces complexity)
/// - Good contrast (clear distinction between text and background)
/// - Sharp edges (crisp character boundaries)
/// - Sufficient resolution (at least 300 DPI equivalent)
///
/// This preprocessor applies these steps automatically.
/// Each step can be individually enabled/disabled via <see cref="PreprocessingOptions"/>.
/// </summary>
public class ImagePreprocessor
{
    private readonly ILogger<ImagePreprocessor>? _logger;

    public ImagePreprocessor(ILogger<ImagePreprocessor>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Preprocess an image for optimal OCR results.
    /// Applies grayscale conversion, contrast enhancement, sharpening,
    /// and optional upscaling based on the provided options.
    /// </summary>
    /// <param name="imageBytes">Raw image bytes (PNG, JPEG, TIFF, BMP, etc.)</param>
    /// <param name="options">Preprocessing options (null = use defaults)</param>
    /// <returns>Preprocessed image as PNG bytes</returns>
    public byte[] Preprocess(byte[] imageBytes, PreprocessingOptions? options = null)
    {
        options ??= PreprocessingOptions.Default;

        using var image = Image.Load<Rgba32>(imageBytes);
        _logger?.LogDebug("Preprocessing image: {W}x{H}", image.Width, image.Height);

        image.Mutate(ctx =>
        {
            // Step 1: Upscale small images to improve OCR accuracy
            // Tesseract works best at 300+ DPI. If the image is small,
            // we scale it up so character details aren't lost.
            if (options.EnableUpscale && (image.Width < options.MinWidth || image.Height < options.MinHeight))
            {
                var scale = Math.Max(
                    (float)options.MinWidth / image.Width,
                    (float)options.MinHeight / image.Height);
                scale = Math.Min(scale, options.MaxUpscaleFactor);

                var newWidth = (int)(image.Width * scale);
                var newHeight = (int)(image.Height * scale);

                _logger?.LogDebug("Upscaling from {W}x{H} to {NW}x{NH} (scale: {S:F1}x)",
                    image.Width, image.Height, newWidth, newHeight, scale);

                ctx.Resize(newWidth, newHeight, KnownResamplers.Lanczos3);
            }

            // Step 2: Convert to grayscale
            // Tesseract processes single-channel images faster and more accurately.
            if (options.EnableGrayscale)
            {
                ctx.Grayscale();
            }

            // Step 3: Enhance contrast
            // Makes text stand out from the background. Uses a configurable amount
            // (1.0 = no change, >1.0 = more contrast, <1.0 = less contrast).
            if (options.EnableContrast && options.ContrastAmount != 1.0f)
            {
                ctx.Contrast(options.ContrastAmount);
            }

            // Step 4: Adjust brightness if needed
            // Useful for dark scans or photos. 1.0 = no change.
            if (options.EnableBrightness && options.BrightnessAmount != 1.0f)
            {
                ctx.Brightness(options.BrightnessAmount);
            }

            // Step 5: Sharpen edges
            // Crisp character edges help Tesseract distinguish similar characters.
            // Uses Gaussian sharpen with configurable sigma.
            if (options.EnableSharpen)
            {
                ctx.GaussianSharpen(options.SharpenSigma);
            }
        });

        // Export as PNG (lossless - no additional compression artifacts)
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Preprocess an image file and return the result as bytes.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="options">Preprocessing options (null = use defaults)</param>
    /// <returns>Preprocessed image as PNG bytes</returns>
    public byte[] PreprocessFile(string imagePath, PreprocessingOptions? options = null)
    {
        var imageBytes = File.ReadAllBytes(imagePath);
        return Preprocess(imageBytes, options);
    }
}

/// <summary>
/// Options for image preprocessing. All steps are enabled by default with
/// sensible values tuned for Tesseract OCR.
///
/// For most images, the defaults work well. Adjust these if you have:
/// - Very dark images → increase BrightnessAmount
/// - Low contrast scans → increase ContrastAmount
/// - Blurry photos → increase SharpenSigma (but too much creates artifacts)
/// - Tiny images → reduce MinWidth/MinHeight thresholds
/// </summary>
public class PreprocessingOptions
{
    /// <summary>
    /// Convert to grayscale. Recommended for OCR. Default: true.
    /// </summary>
    public bool EnableGrayscale { get; set; } = true;

    /// <summary>
    /// Apply contrast enhancement. Default: true.
    /// </summary>
    public bool EnableContrast { get; set; } = true;

    /// <summary>
    /// Contrast multiplier. 1.0 = no change, 1.5 = 50% more contrast.
    /// Tesseract typically benefits from slightly boosted contrast (1.3-1.8).
    /// Default: 1.5
    /// </summary>
    public float ContrastAmount { get; set; } = 1.5f;

    /// <summary>
    /// Apply brightness adjustment. Default: false (most scans don't need it).
    /// </summary>
    public bool EnableBrightness { get; set; } = false;

    /// <summary>
    /// Brightness multiplier. 1.0 = no change, 1.2 = 20% brighter.
    /// Default: 1.0
    /// </summary>
    public float BrightnessAmount { get; set; } = 1.0f;

    /// <summary>
    /// Apply Gaussian sharpening. Default: true.
    /// </summary>
    public bool EnableSharpen { get; set; } = true;

    /// <summary>
    /// Gaussian sharpen sigma. Higher values = more sharpening.
    /// Range: 0.5 (subtle) to 3.0 (aggressive). Default: 1.0
    /// </summary>
    public float SharpenSigma { get; set; } = 1.0f;

    /// <summary>
    /// Upscale images that are too small. Default: true.
    /// </summary>
    public bool EnableUpscale { get; set; } = true;

    /// <summary>
    /// Minimum image width in pixels before upscaling kicks in.
    /// Default: 640 (roughly 2 inches at 300 DPI)
    /// </summary>
    public int MinWidth { get; set; } = 640;

    /// <summary>
    /// Minimum image height in pixels before upscaling kicks in.
    /// Default: 480
    /// </summary>
    public int MinHeight { get; set; } = 480;

    /// <summary>
    /// Maximum upscale factor to prevent excessive memory usage.
    /// Default: 3.0 (3x upscale maximum)
    /// </summary>
    public float MaxUpscaleFactor { get; set; } = 3.0f;

    /// <summary>
    /// Default preprocessing options, tuned for Tesseract OCR.
    /// Enables grayscale, contrast boost (1.5x), and light sharpening.
    /// </summary>
    public static PreprocessingOptions Default => new();

    /// <summary>
    /// Minimal preprocessing - only grayscale conversion.
    /// Use when images are already clean scans.
    /// </summary>
    public static PreprocessingOptions Minimal => new()
    {
        EnableContrast = false,
        EnableSharpen = false,
        EnableUpscale = false
    };

    /// <summary>
    /// Aggressive preprocessing for poor quality images.
    /// Higher contrast, stronger sharpening, larger upscale threshold.
    /// </summary>
    public static PreprocessingOptions Aggressive => new()
    {
        ContrastAmount = 1.8f,
        SharpenSigma = 1.5f,
        MinWidth = 1024,
        MinHeight = 768,
        MaxUpscaleFactor = 4.0f
    };
}
