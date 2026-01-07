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
    private const string ProtocolRelative = "//";
    private const string DataProtocol = "data:";
    private const string BlobProtocol = "blob:";
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
            var finalQueryString = ApplyDefaultOptions(queryString);

            if (IsRootedPath(path))
            {
                link.Url = NormalizeRootedPath(path) + finalQueryString;
                continue;
            }

            if (TryNormalizePrimaryFolderPath(path, out var primaryRelativePath))
            {
                var primaryFolder = _imageConfig?.PrimaryImageFolder ?? DefaultImageFolder;
                link.Url = BuildImageUrl(primaryFolder, primaryRelativePath) + finalQueryString;
                continue;
            }

            var imagePath = FindImagePath(path);
            link.Url = imagePath + finalQueryString;
        }
    }

    private static bool IsExternalUrl(string url) =>
        url.StartsWith(HttpProtocol, StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith(HttpsProtocol, StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith(ProtocolRelative, StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith(DataProtocol, StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith(BlobProtocol, StringComparison.OrdinalIgnoreCase);

    private static bool IsRootedPath(string path) =>
        path.StartsWith("/", StringComparison.Ordinal) ||
        path.StartsWith("\\", StringComparison.Ordinal) ||
        path.StartsWith("~/", StringComparison.Ordinal);

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
        imageName = NormalizeRelativePath(imageName);

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

        imageName = NormalizeRelativePath(imageName);
        foreach (var fallbackFolder in _imageConfig.FallbackFolders)
        {
            var fallbackPath = Path.Combine(_env.ContentRootPath, fallbackFolder, imageName);
            var resolvedPath = File.Exists(fallbackPath)
                ? fallbackPath
                : ResolveFallbackPath(fallbackFolder, imageName);

            if (resolvedPath == null) continue;

            var targetPath = Path.Combine(_env.WebRootPath, primaryFolder, imageName);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!File.Exists(targetPath))
            {
                File.Copy(resolvedPath, targetPath, false);
            }

            return true;
        }

        return false;
    }

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        normalized = normalized.TrimStart('/');

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safeParts = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (safeParts.Count > 0) safeParts.RemoveAt(safeParts.Count - 1);
                continue;
            }
            safeParts.Add(part);
        }

        return string.Join("/", safeParts);
    }

    private static string NormalizeRootedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(1);
        }
        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized.TrimStart('/');
    }

    private bool TryNormalizePrimaryFolderPath(string path, out string primaryRelativePath)
    {
        primaryRelativePath = string.Empty;
        var primaryFolder = _imageConfig?.PrimaryImageFolder;
        if (string.IsNullOrEmpty(primaryFolder)) return false;

        var normalized = path.Replace('\\', '/').TrimStart('/');
        var prefix = primaryFolder.Trim('/').Trim('\\') + "/";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        primaryRelativePath = NormalizeRelativePath(normalized.Substring(prefix.Length));
        return !string.IsNullOrEmpty(primaryRelativePath);
    }

    private string? ResolveFallbackPath(string fallbackFolder, string imageName)
    {
        if (_env == null) return null;

        var normalizedFallback = fallbackFolder.Replace('\\', '/').TrimEnd('/');
        var fallbackRoot = Path.Combine(_env.ContentRootPath, normalizedFallback);
        var fallbackTail = normalizedFallback.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        if (!string.IsNullOrEmpty(fallbackTail) &&
            imageName.StartsWith(fallbackTail + "/", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = imageName.Substring(fallbackTail.Length + 1);
            var trimmedPath = Path.Combine(fallbackRoot, trimmed);
            if (File.Exists(trimmedPath))
            {
                return trimmedPath;
            }
        }

        return null;
    }

    private static string BuildImageUrl(string folder, string imageName) =>
        $"/{folder}/{imageName}";
}
