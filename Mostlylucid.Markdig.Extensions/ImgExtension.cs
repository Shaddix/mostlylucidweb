using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Hosting;
using Mostlylucid.Shared.Config.Markdown;

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

    /// <summary>
    /// Parameterless constructor for backward compatibility (fallback mode)
    /// Uses default settings without file checking
    /// </summary>
    public ImgExtension()
    {
        _env = null;
        _imageConfig = null;
    }

    public ImgExtension(IWebHostEnvironment env, ImageConfig imageConfig)
    {
        _env = env;
        _imageConfig = imageConfig;
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
    /// Applies default ImageSharp options if not specified in querystring
    /// </summary>
    private string ApplyDefaultOptions(string existingQueryString)
    {
        var hasFormat = existingQueryString.Contains(FormatParam, StringComparison.OrdinalIgnoreCase);
        var hasQuality = existingQueryString.Contains(QualityParam, StringComparison.OrdinalIgnoreCase);

        if (hasFormat && hasQuality) return existingQueryString;

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

            var targetPath = Path.Combine(_env.WebRootPath, primaryFolder, imageName);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                return true;
            }

            if (!File.Exists(targetPath))
            {
                File.Copy(fallbackPath, targetPath, false);
            }

            return true;
        }

        return false;
    }

    private static string BuildImageUrl(string folder, string imageName) =>
        $"/{folder}/{imageName}";
}
