using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Filters;
using Mostlylucid.Services.Announcement;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.API;

[Route("api/announcement")]
[ApiController]
public class AnnouncementAPI : ControllerBase
{
    private readonly IAnnouncementService _announcementService;
    private readonly ILogger<AnnouncementAPI> _logger;

    public AnnouncementAPI(IAnnouncementService announcementService, ILogger<AnnouncementAPI> logger)
    {
        _announcementService = announcementService;
        _logger = logger;
    }

    /// <summary>
    /// Get the active announcement for a language (public endpoint)
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<AnnouncementDto?>> GetActive([FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementService.GetActiveAnnouncementAsync(language, cancellationToken);

        if (announcement == null)
        {
            return Ok(null);
        }

        return Ok(MapToDto(announcement));
    }

    /// <summary>
    /// Get all announcements (requires API token)
    /// </summary>
    [HttpGet("all")]
    [ApiTokenAuth]
    public async Task<ActionResult<List<AnnouncementDto>>> GetAll(CancellationToken cancellationToken = default)
    {
        var announcements = await _announcementService.GetAllAnnouncementsAsync(cancellationToken);
        return Ok(announcements.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get a specific announcement by key and language (requires API token)
    /// </summary>
    [HttpGet("{key}")]
    [ApiTokenAuth]
    public async Task<ActionResult<AnnouncementDto>> Get(string key, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementService.GetAnnouncementAsync(key, language, cancellationToken);

        if (announcement == null)
        {
            return NotFound(new { error = $"Announcement '{key}' not found for language '{language}'" });
        }

        return Ok(MapToDto(announcement));
    }

    /// <summary>
    /// Create or update an announcement (requires API token)
    /// </summary>
    [HttpPost]
    [ApiTokenAuth]
    public async Task<ActionResult<AnnouncementDto>> Upsert([FromBody] CreateAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var announcement = await _announcementService.UpsertAnnouncementAsync(
                request.Key,
                request.Markdown,
                request.Language,
                request.IsActive,
                request.Priority,
                request.StartDate,
                request.EndDate,
                cancellationToken);

            return Ok(MapToDto(announcement));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting announcement {Key}", request.Key);
            return StatusCode(500, new { error = "Failed to save announcement" });
        }
    }

    /// <summary>
    /// Delete an announcement (requires API token)
    /// </summary>
    [HttpDelete("{key}")]
    [ApiTokenAuth]
    public async Task<ActionResult> Delete(string key, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var result = await _announcementService.DeleteAnnouncementAsync(key, language, cancellationToken);

        if (!result)
        {
            return NotFound(new { error = $"Announcement '{key}' not found for language '{language}'" });
        }

        return Ok(new { message = "Announcement deleted" });
    }

    /// <summary>
    /// Deactivate an announcement (requires API token)
    /// </summary>
    [HttpPost("{key}/deactivate")]
    [ApiTokenAuth]
    public async Task<ActionResult> Deactivate(string key, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
    {
        var result = await _announcementService.DeactivateAnnouncementAsync(key, language, cancellationToken);

        if (!result)
        {
            return NotFound(new { error = $"Announcement '{key}' not found for language '{language}'" });
        }

        return Ok(new { message = "Announcement deactivated" });
    }

    /// <summary>
    /// Upload an image for use in announcements (requires API token)
    /// </summary>
    [HttpPost("upload-image")]
    [ApiTokenAuth]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult> UploadImage(IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp, svg" });
        }

        try
        {
            // Generate unique filename
            var fileName = $"announcement-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{extension}";
            var uploadPath = Path.Combine(env.WebRootPath, "articleimages", fileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(uploadPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save file
            await using var stream = new FileStream(uploadPath, FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/articleimages/{fileName}";
            _logger.LogInformation("Uploaded announcement image: {FileName}", fileName);

            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new { error = "Failed to upload image" });
        }
    }

    private static AnnouncementDto MapToDto(AnnouncementEntity entity)
    {
        return new AnnouncementDto
        {
            Id = entity.Id,
            Key = entity.Key,
            Markdown = entity.Markdown,
            HtmlContent = entity.HtmlContent,
            Language = entity.Language,
            IsActive = entity.IsActive,
            Priority = entity.Priority,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
