using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Mostlylucid.OcrNer.Services.Preprocessing;

/// <summary>
/// Advanced image preprocessor using OpenCV.
/// Chains: Quality Assessment -> Deskew -> Denoise -> Binarize.
/// Only active when EnableAdvancedPreprocessing is true in config.
/// Falls back to the ImageSharp-based ImagePreprocessor when disabled.
/// </summary>
public class OpenCvPreprocessor
{
    private readonly ILogger<OpenCvPreprocessor> _logger;
    private readonly ImageQualityAssessor _qualityAssessor = new();
    private readonly SkewCorrector _skewCorrector = new();
    private readonly NoiseReducer _noiseReducer = new();
    private readonly InkExtractor _inkExtractor = new();

    public OpenCvPreprocessor(ILogger<OpenCvPreprocessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Run the full OpenCV preprocessing pipeline on raw image bytes.
    /// Returns preprocessed PNG bytes ready for Tesseract.
    /// </summary>
    public byte[] Preprocess(byte[] imageBytes)
    {
        using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
        if (mat.Empty())
        {
            _logger.LogWarning("OpenCV could not decode image, returning original bytes");
            return imageBytes;
        }

        _logger.LogDebug("OpenCV preprocessing: {W}x{H} image", mat.Width, mat.Height);

        // Step 1: Quality assessment
        var report = _qualityAssessor.Analyze(mat);
        _logger.LogDebug("Quality: blur={Blur:F1}, skew={Skew:F1}deg, noise={Noise:F1}, contrast={Contrast:F2}",
            report.BlurScore, report.SkewAngle, report.NoiseLevel, report.ContrastScore);

        if (report.Recommendations.Length > 0)
            _logger.LogDebug("Recommendations: {Recs}", string.Join("; ", report.Recommendations));

        var current = mat.Clone();

        try
        {
            // Step 2: Deskew if needed
            if (Math.Abs(report.SkewAngle) > 2.0)
            {
                var deskewResult = _skewCorrector.Deskew(current);
                current.Dispose();
                current = deskewResult.Image;
                _logger.LogDebug("Deskewed by {Angle:F1} degrees using {Method}",
                    deskewResult.Angle, deskewResult.Method);
            }

            // Convert to grayscale for remaining steps
            using var gray = current.Channels() == 1
                ? current.Clone()
                : current.CvtColor(ColorConversionCodes.BGR2GRAY);

            // Step 3: Denoise if needed
            Mat denoised;
            if (report.NoiseLevel > 15)
            {
                denoised = _noiseReducer.Denoise(gray, NoiseReducer.DenoiseMethod.Bilateral);
                _logger.LogDebug("Applied bilateral denoising");
            }
            else if (report.NoiseLevel > 8)
            {
                denoised = _noiseReducer.Denoise(gray, NoiseReducer.DenoiseMethod.Gaussian);
                _logger.LogDebug("Applied gaussian denoising");
            }
            else
            {
                denoised = gray.Clone();
            }

            // Step 4: Binarize - choose method based on quality
            Mat binarized;
            if (report.ContrastScore < 0.3)
            {
                binarized = _inkExtractor.Extract(denoised, InkExtractor.BinarizationMethod.ClaheOtsu);
                _logger.LogDebug("Applied CLAHE+Otsu binarization for low contrast");
            }
            else if (report.BrightnessUniformity > 0.25)
            {
                binarized = _inkExtractor.Extract(denoised, InkExtractor.BinarizationMethod.Adaptive);
                _logger.LogDebug("Applied adaptive binarization for uneven illumination");
            }
            else
            {
                binarized = _inkExtractor.Extract(denoised, InkExtractor.BinarizationMethod.Otsu);
                _logger.LogDebug("Applied Otsu binarization");
            }

            // Invert back: Tesseract expects black text on white background
            using var output = new Mat();
            Cv2.BitwiseNot(binarized, output);

            // Encode as PNG
            Cv2.ImEncode(".png", output, out var pngBytes);

            denoised.Dispose();
            binarized.Dispose();

            _logger.LogDebug("OpenCV preprocessing complete: output {Bytes} bytes", pngBytes.Length);
            return pngBytes;
        }
        finally
        {
            current.Dispose();
        }
    }

    /// <summary>
    /// Preprocess an image file and return preprocessed PNG bytes.
    /// </summary>
    public byte[] PreprocessFile(string imagePath)
    {
        var imageBytes = File.ReadAllBytes(imagePath);
        return Preprocess(imageBytes);
    }
}
