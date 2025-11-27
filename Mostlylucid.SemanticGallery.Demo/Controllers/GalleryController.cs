using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SemanticGallery.Demo.Models;
using Mostlylucid.SemanticGallery.Demo.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Mostlylucid.SemanticGallery.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GalleryController : ControllerBase
{
    private readonly ILogger<GalleryController> _logger;
    private readonly SimplifiedImageAnalysisService _analysisService;
    private readonly InMemoryGalleryService _galleryService;
    private readonly IWebHostEnvironment _environment;

    public GalleryController(
        ILogger<GalleryController> logger,
        SimplifiedImageAnalysisService analysisService,
        InMemoryGalleryService galleryService,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _analysisService = analysisService;
        _galleryService = galleryService;
        _environment = environment;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
    public async Task<IActionResult> UploadImage(IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { error = "No image provided" });

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(image.ContentType.ToLower()))
            return BadRequest(new { error = "Invalid image type. Allowed: JPEG, PNG, WebP" });

        try
        {
            _logger.LogInformation("Processing upload: {FileName}, Size: {Size} bytes",
                image.FileName, image.Length);

            // Create uploads directory if it doesn't exist
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(image.FileName)}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);
            var webPath = $"/uploads/{uniqueFileName}";

            // Save the file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }

            // Create thumbnail
            var thumbnailPath = await CreateThumbnailAsync(filePath, uploadsPath);

            // Analyze the image
            using var stream = image.OpenReadStream();
            var (caption, extractedText) = await _analysisService.AnalyzeImageAsync(stream);

            // Detect faces
            stream.Position = 0;
            var faces = await _analysisService.DetectFacesAsync(stream);

            // Create gallery image object
            var galleryImage = new GalleryImage
            {
                FileName = image.FileName,
                FilePath = webPath,
                Caption = caption,
                ExtractedText = extractedText,
                Faces = faces,
                Tags = new List<string>()
            };

            // Index the image
            await _galleryService.IndexImageAsync(galleryImage, caption);

            _logger.LogInformation("Successfully processed image {FileName}", image.FileName);

            return Ok(new
            {
                success = true,
                image = galleryImage,
                message = $"Image processed successfully. {(string.IsNullOrEmpty(extractedText) ? "No text detected." : "Text extracted.")} Found {faces.Count} face(s)."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image {FileName}", image.FileName);
            return StatusCode(500, new { error = "Failed to process image", details = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] int limit = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                // Return all images if no query
                var allImages = await _galleryService.GetAllImagesAsync();
                var allResults = allImages.Take(limit).Select(img => new SearchResult
                {
                    Image = img,
                    Score = 1.0f,
                    MatchReason = "Recent upload"
                }).ToList();

                return Ok(new
                {
                    query = query ?? "all",
                    results = allResults,
                    count = allResults.Count
                });
            }

            // Semantic search
            var results = await _galleryService.SemanticSearchAsync(query, limit);

            return Ok(new
            {
                query = query,
                results = results,
                count = results.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching gallery");
            return StatusCode(500, new { error = "Search failed", details = ex.Message });
        }
    }

    [HttpGet("search/person/{personName}")]
    public async Task<IActionResult> SearchByPerson(string personName, [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(personName))
            return BadRequest(new { error = "Person name cannot be empty" });

        try
        {
            var results = await _galleryService.SearchByPersonAsync(personName, limit);

            return Ok(new
            {
                personName = personName,
                results = results,
                count = results.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by person {PersonName}", personName);
            return StatusCode(500, new { error = "Search failed", details = ex.Message });
        }
    }

    [HttpGet("images")]
    public async Task<IActionResult> GetAllImages()
    {
        try
        {
            var images = await _galleryService.GetAllImagesAsync();
            return Ok(new
            {
                images = images,
                count = images.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting images");
            return StatusCode(500, new { error = "Failed to get images" });
        }
    }

    [HttpGet("images/{imageId}")]
    public async Task<IActionResult> GetImageById(Guid imageId)
    {
        try
        {
            var image = await _galleryService.GetImageByIdAsync(imageId);
            if (image == null)
                return NotFound(new { error = "Image not found" });

            return Ok(image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image {ImageId}", imageId);
            return StatusCode(500, new { error = "Failed to get image" });
        }
    }

    private async Task<string> CreateThumbnailAsync(string originalPath, string uploadsPath)
    {
        try
        {
            using var image = await Image.LoadAsync(originalPath);

            // Resize to thumbnail (300x300 max)
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(300, 300),
                Mode = ResizeMode.Max
            }));

            var thumbnailFileName = $"thumb_{Path.GetFileName(originalPath)}";
            var thumbnailPath = Path.Combine(uploadsPath, thumbnailFileName);

            await image.SaveAsJpegAsync(thumbnailPath);

            return $"/uploads/{thumbnailFileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating thumbnail");
            return string.Empty;
        }
    }
}
