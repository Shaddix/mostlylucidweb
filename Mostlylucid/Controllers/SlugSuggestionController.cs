using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Services;
using Mostlylucid.Services.Blog;

namespace Mostlylucid.Controllers;

/// <summary>
/// API Controller for slug suggestion and redirect learning system
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class SlugSuggestionController : BaseController
{
    private readonly ISlugSuggestionService? _slugSuggestionService;

    public SlugSuggestionController(
        BaseControllerService baseControllerService,
        ILogger<SlugSuggestionController> logger,
        ISlugSuggestionService? slugSuggestionService = null)
        : base(baseControllerService, logger)
    {
        _slugSuggestionService = slugSuggestionService;
    }

    /// <summary>
    /// Track when a user clicks on a suggestion from the 404 page
    /// </summary>
    [HttpPost("track-click")]
    public async Task<IActionResult> TrackClick(
        [FromBody] TrackClickRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_slugSuggestionService == null)
        {
            return Ok(new { success = false, message = "Slug suggestion service not available" });
        }

        if (string.IsNullOrWhiteSpace(request.RequestedSlug) ||
            string.IsNullOrWhiteSpace(request.ClickedSlug))
        {
            return BadRequest(new { success = false, message = "Invalid request" });
        }

        try
        {
            // Get user info
            var userIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await _slugSuggestionService.RecordSuggestionClickAsync(
                request.RequestedSlug,
                request.ClickedSlug,
                request.Language ?? "en",
                request.SuggestionPosition,
                request.OriginalScore,
                userIp,
                userAgent,
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error tracking suggestion click");
            return Ok(new { success = false, message = "Error tracking click" });
        }
    }
}

/// <summary>
/// Request model for tracking suggestion clicks
/// </summary>
public class TrackClickRequest
{
    public string RequestedSlug { get; set; } = string.Empty;
    public string ClickedSlug { get; set; } = string.Empty;
    public string? Language { get; set; }
    public int SuggestionPosition { get; set; }
    public double OriginalScore { get; set; }
}
