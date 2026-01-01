using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Mostlylucid.Shared.Config.Markdown;
using SixLabors.ImageSharp;

namespace Mostlylucid.Markdig.Extensions;

public class ImgExtension : IMarkdownExtension
{
    private const string HttpProtocol = "http:";
    private const string HttpsProtocol = "https:";
    private const string FormatParam = "format=";
    private const string QualityParam = "quality=";
    private const string QuerySeparator = "?";
    private const string ParamSeparator = "&";
    private const string DefaultImageFolder = "articleimages";
    private const string DefaultFormat = "webp";
    private const int DefaultQuality = 50;

    private readonly IWebHostEnvironment? _env;
    private readonly ImageConfig? _imageConfig;
    private readonly ILogger<ImgExtension>? _logger;

    /// <summary>
    /// Parameterless constructor for backward compatibility (fallback mode)
    /// Uses default settings without file checking
    /// </summary>
    public ImgExtension()
    {
        _env = null;
        _imageConfig = null;
        _logger = null;
    }

    public ImgExtension(IWebHostEnvironment env, ImageConfig imageConfig, ILogger<ImgExtension>? logger = null)
    {
        _env = env;
        _imageConfig = imageConfig;
        _logger = logger;
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.DocumentProcessed += ChangeImgPath;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private void ChangeImgPath(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!link.IsImage) continue;

            var url = link.Url;
            if (string.IsNullOrEmpty(url)) continue;
            if (IsExternalUrl(url)) continue;

            var (path, queryString) = ParseUrl(url);
            var imagePath = FindImagePath(path);
            var finalQueryString = ApplyDefaultOptions(queryString);

            link.Url = imagePath + finalQueryString;
        }
    }

    private static bool IsExternalUrl(string url) =>
        url.StartsWith(HttpProtocol, StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith(HttpsProtocol, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Splits URL into path and querystring
    /// </summary>
    private (string path, string queryString) ParseUrl(string url)
    {
        var questionMarkIndex = url.IndexOf('?');
        if (questionMarkIndex >= 0)
        {
            return (url.Substring(0, questionMarkIndex), url.Substring(questionMarkIndex));
        }
        return (url, string.Empty);
    }

    /// <summary>
    /// Applies default ImageSharp options only if:
    /// - AutoProcess is enabled in config, OR
    /// - The URL already has processing params (opt-in)
    /// </summary>
    private string ApplyDefaultOptions(string existingQueryString)
    {
        // If no existing query string and auto-process is disabled, serve as-is
        var autoProcess = _imageConfig?.AutoProcess ?? false;
        if (string.IsNullOrEmpty(existingQueryString) && !autoProcess)
        {
            return existingQueryString;
        }

        var hasFormat = existingQueryString.Contains(FormatParam, StringComparison.OrdinalIgnoreCase);
        var hasQuality = existingQueryString.Contains(QualityParam, StringComparison.OrdinalIgnoreCase);

        // If already fully specified, return as-is
        if (hasFormat && hasQuality) return existingQueryString;

        // Only add defaults if there's already some query params (opt-in) or auto-process is on
        if (string.IsNullOrEmpty(existingQueryString) && !autoProcess)
        {
            return existingQueryString;
        }

        var separator = string.IsNullOrEmpty(existingQueryString) ? QuerySeparator : ParamSeparator;
        var additions = new List<string>();

        if (!hasFormat)
        {
            var format = _imageConfig?.DefaultFormat ?? DefaultFormat;
            additions.Add($"{FormatParam}{format}");
        }

        if (!hasQuality)
        {
            var quality = _imageConfig?.DefaultQuality ?? DefaultQuality;
            additions.Add($"{QualityParam}{quality}");
        }

        return additions.Count > 0
            ? existingQueryString + separator + string.Join(ParamSeparator, additions)
            : existingQueryString;
    }

    /// <summary>
    /// Finds the actual path where the image exists
    /// Checks primary folder first, then fallback folders
    /// </summary>
    private string FindImagePath(string imageName)
    {
        imageName = imageName.TrimStart('/');

        if (_env == null || _imageConfig == null)
        {
            return BuildImageUrl(DefaultImageFolder, imageName);
        }

        var primaryFolder = _imageConfig.PrimaryImageFolder;
        var primaryPath = Path.Combine(_env.WebRootPath, primaryFolder, imageName);

        if (File.Exists(primaryPath))
        {
            return BuildImageUrl(primaryFolder, imageName);
        }

        if (TryCopyFromFallbackFolders(imageName, primaryFolder))
        {
            return BuildImageUrl(primaryFolder, imageName);
        }

        return BuildImageUrl(primaryFolder, imageName);
    }

    private bool TryCopyFromFallbackFolders(string imageName, string primaryFolder)
    {
        if (_env == null || _imageConfig == null) return false;

        foreach (var fallbackFolder in _imageConfig.FallbackFolders)
        {
            var fallbackPath = Path.Combine(_env.ContentRootPath, fallbackFolder, imageName);
            if (!File.Exists(fallbackPath)) continue;

            // Validate image before copying
            if (!IsValidImage(fallbackPath))
            {
                _logger?.LogWarning("Skipping invalid/corrupt image: {ImagePath}", fallbackPath);
                continue;
            }

            var targetPath = Path.Combine(_env.WebRootPath, primaryFolder, imageName);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!File.Exists(targetPath))
            {
                File.Copy(fallbackPath, targetPath, false);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates that a file is a valid image that ImageSharp can process
    /// </summary>
    private bool IsValidImage(string filePath)
    {
        try
        {
            // Try to identify the image format - this is fast and doesn't load the full image
            using var stream = File.OpenRead(filePath);
            var format = Image.DetectFormat(stream);

            if (format == null)
            {
                _logger?.LogDebug("Could not detect image format for: {FilePath}", filePath);
                return false;
            }

            // Reset stream position and try to load the image to verify it's not corrupt
            stream.Position = 0;
            using var image = Image.Load(stream);

            // Additional sanity checks
            if (image.Width <= 0 || image.Height <= 0)
            {
                _logger?.LogDebug("Image has invalid dimensions: {FilePath} ({Width}x{Height})",
                    filePath, image.Width, image.Height);
                return false;
            }

            return true;
        }
        catch (UnknownImageFormatException ex)
        {
            _logger?.LogWarning("Unknown image format for {FilePath}: {Message}", filePath, ex.Message);
            return false;
        }
        catch (InvalidImageContentException ex)
        {
            _logger?.LogWarning("Invalid image content for {FilePath}: {Message}", filePath, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to validate image: {FilePath}", filePath);
            return false;
        }
    }

    private static string BuildImageUrl(string folder, string imageName) =>
        $"/{folder}/{imageName}";
}
