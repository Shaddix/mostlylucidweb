using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

/// <summary>
/// Serves generated product images from external storage directory.
/// Falls back to placeholder if image not found.
/// </summary>
[ApiController]
[Route("api/images")]
public class ImageController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ImageController> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    public ImageController(IConfiguration configuration, ILogger<ImageController> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    /// <summary>
    /// Get product image by category and product name.
    /// </summary>
    [HttpGet("products/{category}/{productFolder}/{filename}")]
    [ResponseCache(Duration = 86400)] // Cache for 1 day
    public IActionResult GetProductImage(string category, string productFolder, string filename)
    {
        var basePath = _configuration["ImageStorage:BasePath"] ?? @"D:\segmentdata\images";
        var imagePath = Path.Combine(basePath, category, productFolder, filename);

        if (!System.IO.File.Exists(imagePath))
        {
            _logger.LogDebug("Image not found at {Path}, falling back to placeholder", imagePath);
            
            // Redirect to placeholder
            var productName = productFolder.Replace("_", " ");
            return RedirectToAction("GetPlaceholder", "Placeholder", new { category, productName, w = 400, h = 400 });
        }

        if (!_contentTypeProvider.TryGetContentType(filename, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = System.IO.File.OpenRead(imagePath);
        return File(stream, contentType);
    }

    /// <summary>
    /// Get main product image by category and product folder name.
    /// </summary>
    [HttpGet("products/{category}/{productFolder}")]
    [ResponseCache(Duration = 86400)]
    public IActionResult GetMainProductImage(string category, string productFolder, [FromQuery] string variant = "main")
    {
        return GetProductImage(category, productFolder, $"{variant}.png");
    }

    /// <summary>
    /// List available images for a product.
    /// </summary>
    [HttpGet("products/{category}/{productFolder}/list")]
    public IActionResult ListProductImages(string category, string productFolder)
    {
        var basePath = _configuration["ImageStorage:BasePath"] ?? @"D:\segmentdata\images";
        var productPath = Path.Combine(basePath, category, productFolder);

        if (!Directory.Exists(productPath))
        {
            return NotFound(new { error = "Product folder not found" });
        }

        var images = Directory.GetFiles(productPath, "*.png")
            .Select(f => new
            {
                filename = Path.GetFileName(f),
                variant = Path.GetFileNameWithoutExtension(f),
                url = $"/api/images/products/{category}/{productFolder}/{Path.GetFileName(f)}"
            })
            .ToList();

        return Ok(images);
    }

    /// <summary>
    /// Get profile/avatar image.
    /// </summary>
    [HttpGet("profiles/{profileKey}")]
    [ResponseCache(Duration = 86400)]
    public IActionResult GetProfileImage(string profileKey)
    {
        var basePath = _configuration["ImageStorage:BasePath"] ?? @"D:\segmentdata\images";
        var imagePath = Path.Combine(basePath, "profile-images", profileKey, "portrait.png");

        if (!System.IO.File.Exists(imagePath))
        {
            // Return a default avatar SVG
            var svg = GenerateAvatarSvg(profileKey);
            return Content(svg, "image/svg+xml");
        }

        var stream = System.IO.File.OpenRead(imagePath);
        return File(stream, "image/png");
    }

    private static string GenerateAvatarSvg(string profileKey)
    {
        var hash = GetStableHash(profileKey);
        var hue = hash % 360;
        var initials = GetInitials(profileKey);

        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
              <rect width="64" height="64" rx="32" fill="hsl({hue}, 60%, 45%)"/>
              <text x="50%" y="55%" text-anchor="middle" fill="white" font-family="system-ui, sans-serif" font-size="24" font-weight="bold">
                {initials}
              </text>
            </svg>
            """;
    }

    private static int GetStableHash(string input)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return Math.Abs(hash);
        }
    }

    private static string GetInitials(string profileKey)
    {
        if (string.IsNullOrEmpty(profileKey)) return "?";
        
        var parts = profileKey.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        }
        
        return profileKey.Length >= 2 
            ? profileKey[..2].ToUpper() 
            : profileKey.ToUpper();
    }
}
