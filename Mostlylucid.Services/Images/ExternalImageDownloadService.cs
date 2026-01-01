using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Shared.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;

namespace Mostlylucid.Services.Images;

/// <summary>
/// Service for downloading external images from blog posts and serving them locally
/// </summary>
public partial class ExternalImageDownloadService
{
    private readonly MostlylucidDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalImageDownloadService> _logger;
    private readonly string _imageStoragePath;
    private readonly HashSet<string> _allowedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        // Add domains to whitelist for downloading (optional safety measure)
        // Empty means all domains allowed
    };

    public ExternalImageDownloadService(
        MostlylucidDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalImageDownloadService> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _imageStoragePath = Path.Combine(environment.WebRootPath, "externalimages");

        // Ensure directory exists
        Directory.CreateDirectory(_imageStoragePath);
    }

    [GeneratedRegex(@"<img\s+([^>]*\s+)?src=[""']([^""']+)[""']([^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex(@"\s+(width|height)=[""']?\d+[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex SizeAttributeRegex();

    /// <summary>
    /// Process a blog post to download external images
    /// </summary>
    public async Task ProcessPostAsync(BlogPostEntity post, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(post.HtmlContent))
        {
            _logger.LogDebug("Post {Slug} has no HTML content, skipping", post.Slug);
            return;
        }

        var externalImages = ExtractExternalImages(post.HtmlContent);
        if (externalImages.Count == 0)
        {
            _logger.LogDebug("Post {Slug} has no external images", post.Slug);
            await MarkAllImagesVerified(post.Slug, cancellationToken);
            return;
        }

        _logger.LogInformation("Processing {Count} external images for post {Slug}", externalImages.Count, post.Slug);

        var verifiedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var updatedHtml = post.HtmlContent;

        foreach (var imageUrl in externalImages)
        {
            try
            {
                verifiedUrls.Add(imageUrl);

                // Check if already downloaded
                var existing = await _dbContext.DownloadedImages
                    .FirstOrDefaultAsync(x => x.PostSlug == post.Slug && x.OriginalUrl == imageUrl, cancellationToken);

                if (existing != null)
                {
                    // Update verification date
                    existing.LastVerifiedDate = DateTimeOffset.UtcNow;
                    _logger.LogDebug("Image already downloaded: {Url} -> {LocalFile}", imageUrl, existing.LocalFileName);

                    // Update HTML to use local URL
                    updatedHtml = ReplaceImageUrl(updatedHtml, imageUrl, $"/externalimages/{existing.LocalFileName}");
                    continue;
                }

                // Download new image
                var downloadedImage = await DownloadImageAsync(imageUrl, post.Slug, cancellationToken);
                if (downloadedImage != null)
                {
                    _dbContext.DownloadedImages.Add(downloadedImage);
                    updatedHtml = ReplaceImageUrl(updatedHtml, imageUrl, $"/externalimages/{downloadedImage.LocalFileName}");
                    _logger.LogInformation("Downloaded image: {Url} -> {LocalFile}", imageUrl, downloadedImage.LocalFileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image {Url} for post {Slug}", imageUrl, post.Slug);
            }
        }

        // Update post HTML if changed
        if (updatedHtml != post.HtmlContent)
        {
            post.HtmlContent = updatedHtml;
            _logger.LogInformation("Updated HTML for post {Slug} with local image URLs", post.Slug);
        }

        // Mark images as verified
        await MarkImagesVerified(post.Slug, verifiedUrls, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Download an external image and save it locally, with archive.org fallback
    /// </summary>
    private async Task<DownloadedImageEntity?> DownloadImageAsync(string url, string postSlug, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Try original URL first
            var response = await TryDownloadFromUrl(client, url, cancellationToken);

            // If original fails, try archive.org Wayback Machine
            if (response == null || !response.IsSuccessStatusCode)
            {
                var archiveUrl = $"https://web.archive.org/web/0/{url}";
                _logger.LogInformation("Original URL failed, trying archive.org: {ArchiveUrl}", archiveUrl);
                response = await TryDownloadFromUrl(client, archiveUrl, cancellationToken);

                if (response == null || !response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download image {Url} (also tried archive.org)", url);
                    return null;
                }
                _logger.LogInformation("Successfully retrieved image from archive.org for {Url}", url);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("URL {Url} is not an image: {ContentType}", url, contentType);
                return null;
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Generate local filename: slug-originalname.ext
            var originalFileName = Path.GetFileName(new Uri(url).LocalPath);
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrEmpty(extension))
            {
                extension = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };
            }

            // Sanitize filename
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
            var localFileName = $"{postSlug}-{sanitizedName}{extension}";

            // Ensure unique filename
            var fullPath = Path.Combine(_imageStoragePath, localFileName);
            var counter = 1;
            while (File.Exists(fullPath))
            {
                localFileName = $"{postSlug}-{sanitizedName}-{counter}{extension}";
                fullPath = Path.Combine(_imageStoragePath, localFileName);
                counter++;
            }

            // Validate image before saving - ensure ImageSharp can process it
            int? width = null;
            int? height = null;
            try
            {
                using var memoryStream = new MemoryStream(imageBytes);
                var format = Image.DetectFormat(memoryStream);
                if (format == null)
                {
                    _logger.LogWarning("Could not detect image format for {Url} - skipping", url);
                    return null;
                }

                memoryStream.Position = 0;
                using var image = Image.Load(memoryStream);
                width = image.Width;
                height = image.Height;

                if (width <= 0 || height <= 0)
                {
                    _logger.LogWarning("Image has invalid dimensions for {Url}: {Width}x{Height} - skipping", url, width, height);
                    return null;
                }
            }
            catch (UnknownImageFormatException ex)
            {
                _logger.LogWarning("Unknown image format for {Url}: {Message} - skipping", url, ex.Message);
                return null;
            }
            catch (InvalidImageContentException ex)
            {
                _logger.LogWarning("Invalid/corrupt image content for {Url}: {Message} - skipping", url, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate image for {Url} - skipping", url);
                return null;
            }

            // Save file (only if validation passed)
            await File.WriteAllBytesAsync(fullPath, imageBytes, cancellationToken);
            _logger.LogInformation("Saved image to {Path} ({Size} bytes)", fullPath, imageBytes.Length);

            return new DownloadedImageEntity
            {
                PostSlug = postSlug,
                OriginalUrl = url,
                LocalFileName = localFileName,
                DownloadedDate = DateTimeOffset.UtcNow,
                LastVerifiedDate = DateTimeOffset.UtcNow,
                FileSize = imageBytes.Length,
                ContentType = contentType,
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Extract external image URLs from HTML content
    /// </summary>
    private List<string> ExtractExternalImages(string html)
    {
        var externalUrls = new List<string>();
        var matches = ImgTagRegex().Matches(html);

        foreach (Match match in matches)
        {
            var url = match.Groups[2].Value;

            // Skip data URLs, relative URLs, and already local URLs
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("/", StringComparison.Ordinal) ||
                url.StartsWith("./", StringComparison.Ordinal) ||
                url.StartsWith("../", StringComparison.Ordinal))
            {
                continue;
            }

            // Must be absolute HTTP/HTTPS URL
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Skip shields.io badges - they are dynamic and should never be inlined
                if (uri.Host.Equals("shields.io", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".shields.io", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Skipping shields.io badge: {Url}", url);
                    continue;
                }

                externalUrls.Add(url);
            }
        }

        return externalUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Replace an image URL in HTML and remove width/height attributes
    /// </summary>
    private string ReplaceImageUrl(string html, string oldUrl, string newUrl)
    {
        // Find all img tags with this URL
        var pattern = $@"<img\s+([^>]*\s+)?src=[""']{Regex.Escape(oldUrl)}[""']([^>]*)>";
        return Regex.Replace(html, pattern, match =>
        {
            var fullTag = match.Value;

            // Remove width and height attributes
            fullTag = SizeAttributeRegex().Replace(fullTag, "");

            // Replace URL
            fullTag = fullTag.Replace(oldUrl, newUrl);

            return fullTag;
        }, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Mark images as verified for a post
    /// </summary>
    private async Task MarkImagesVerified(string postSlug, HashSet<string> verifiedUrls, CancellationToken cancellationToken)
    {
        var images = await _dbContext.DownloadedImages
            .Where(x => x.PostSlug == postSlug && verifiedUrls.Contains(x.OriginalUrl))
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            image.LastVerifiedDate = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Mark all images for a post as verified (when post has no external images)
    /// </summary>
    private async Task MarkAllImagesVerified(string postSlug, CancellationToken cancellationToken)
    {
        var images = await _dbContext.DownloadedImages
            .Where(x => x.PostSlug == postSlug)
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            image.LastVerifiedDate = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Clean up orphaned images (not verified in the last N days)
    /// </summary>
    public async Task CleanupOrphanedImagesAsync(int daysOld = 7, CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-daysOld);

        var orphanedImages = await _dbContext.DownloadedImages
            .Where(x => x.LastVerifiedDate < cutoffDate)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} orphaned images older than {Days} days", orphanedImages.Count, daysOld);

        foreach (var image in orphanedImages)
        {
            try
            {
                var filePath = Path.Combine(_imageStoragePath, image.LocalFileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted orphaned image file: {FileName}", image.LocalFileName);
                }

                _dbContext.DownloadedImages.Remove(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete orphaned image {FileName}", image.LocalFileName);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleanup complete. Removed {Count} orphaned images", orphanedImages.Count);
    }

    /// <summary>
    /// Try to download from a URL, returning null on failure instead of throwing
    /// </summary>
    private async Task<HttpResponseMessage?> TryDownloadFromUrl(HttpClient client, string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP request failed for {Url}", url);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Request timed out for {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Sanitize filename for safe storage
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return sanitized.ToLowerInvariant();
    }

    /// <summary>
    /// Get statistics about downloaded images
    /// </summary>
    public async Task<(int TotalImages, long TotalSize, int PostsWithImages)> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalImages = await _dbContext.DownloadedImages.CountAsync(cancellationToken);
        var totalSize = await _dbContext.DownloadedImages.SumAsync(x => x.FileSize, cancellationToken);
        var postsWithImages = await _dbContext.DownloadedImages
            .Select(x => x.PostSlug)
            .Distinct()
            .CountAsync(cancellationToken);

        return (totalImages, totalSize, postsWithImages);
    }
}
