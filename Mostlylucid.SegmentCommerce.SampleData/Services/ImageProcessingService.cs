using Mostlylucid.SegmentCommerce.SampleData.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Processes generated images: resizes to standard sizes and creates WebP versions.
/// </summary>
public class ImageProcessingService
{
    private readonly GenerationConfig _config;

    // Standard e-commerce image sizes
    public static readonly (int Width, int Height, string Suffix)[] StandardSizes =
    [
        (100, 100, "thumb"),
        (400, 400, "small"),
        (800, 800, "medium"),
        (1200, 1200, "large")
    ];

    public ImageProcessingService(GenerationConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Process a generated image: create resized versions and WebP variants.
    /// </summary>
    public async Task<ProcessedImageSet> ProcessImageAsync(
        GeneratedImage source,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessedImageSet
        {
            SourcePath = source.FilePath,
            Variant = source.Variant,
            IsPrimary = source.IsPrimary
        };

        if (!File.Exists(source.FilePath))
        {
            AnsiConsole.MarkupLine($"[yellow]Source image not found: {source.FilePath}[/]");
            return result;
        }

        var directory = Path.GetDirectoryName(source.FilePath)!;
        var baseName = Path.GetFileNameWithoutExtension(source.FilePath);

        using var image = await Image.LoadAsync(source.FilePath, cancellationToken);

        foreach (var (width, height, suffix) in StandardSizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create resized PNG
            var pngPath = Path.Combine(directory, $"{baseName}_{suffix}.png");
            await CreateResizedImageAsync(image, width, height, pngPath, cancellationToken);
            result.ResizedImages.Add(new ProcessedImage(suffix, pngPath, "image/png", width, height));

            // Create WebP version
            var webpPath = Path.Combine(directory, $"{baseName}_{suffix}.webp");
            await CreateWebPImageAsync(image, width, height, webpPath, cancellationToken);
            result.WebPImages.Add(new ProcessedImage(suffix, webpPath, "image/webp", width, height));
        }

        return result;
    }

    /// <summary>
    /// Process all images for a product.
    /// </summary>
    public async Task<List<ProcessedImageSet>> ProcessProductImagesAsync(
        List<GeneratedImage> sourceImages,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProcessedImageSet>();

        foreach (var source in sourceImages)
        {
            var processed = await ProcessImageAsync(source, cancellationToken);
            results.Add(processed);
        }

        return results;
    }

    /// <summary>
    /// Create colour variant thumbnails from a base image.
    /// </summary>
    public async Task<List<ProcessedImage>> CreateColourSwatchesAsync(
        string sourceImagePath,
        string[] colours,
        int swatchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var swatches = new List<ProcessedImage>();

        if (!File.Exists(sourceImagePath))
            return swatches;

        var directory = Path.GetDirectoryName(sourceImagePath)!;
        var baseName = Path.GetFileNameWithoutExtension(sourceImagePath);

        using var image = await Image.LoadAsync(sourceImagePath, cancellationToken);

        foreach (var colour in colours)
        {
            var safeName = SanitizeFileName(colour);
            var swatchPath = Path.Combine(directory, $"{baseName}_swatch_{safeName}.webp");

            // Create a small square crop from center
            using var swatch = image.Clone(ctx =>
            {
                var size = Math.Min(ctx.GetCurrentSize().Width, ctx.GetCurrentSize().Height);
                var x = (ctx.GetCurrentSize().Width - size) / 2;
                var y = (ctx.GetCurrentSize().Height - size) / 2;

                ctx.Crop(new Rectangle(x, y, size, size))
                   .Resize(swatchSize, swatchSize);
            });

            await swatch.SaveAsync(swatchPath, new WebpEncoder { Quality = 85 }, cancellationToken);
            swatches.Add(new ProcessedImage($"swatch_{safeName}", swatchPath, "image/webp", swatchSize, swatchSize));
        }

        return swatches;
    }

    private static async Task CreateResizedImageAsync(
        Image source,
        int width,
        int height,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using var resized = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(width, height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            });
        });

        await resized.SaveAsPngAsync(outputPath, cancellationToken);
    }

    private static async Task CreateWebPImageAsync(
        Image source,
        int width,
        int height,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using var resized = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(width, height),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            });
        });

        var encoder = new WebpEncoder
        {
            Quality = 85,
            FileFormat = WebpFileFormatType.Lossy
        };

        await resized.SaveAsync(outputPath, encoder, cancellationToken);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_")
            .ToLowerInvariant();
    }
}

/// <summary>
/// Represents a processed image with metadata.
/// </summary>
public record ProcessedImage(string Suffix, string Path, string ContentType, int Width, int Height);

/// <summary>
/// A set of processed images from a single source.
/// </summary>
public class ProcessedImageSet
{
    public string SourcePath { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public List<ProcessedImage> ResizedImages { get; } = [];
    public List<ProcessedImage> WebPImages { get; } = [];

    /// <summary>
    /// Get the best image for a given size preference.
    /// </summary>
    public ProcessedImage? GetImage(string sizeSuffix, bool preferWebP = true)
    {
        var images = preferWebP ? WebPImages : ResizedImages;
        return images.FirstOrDefault(i => i.Suffix == sizeSuffix);
    }
}
