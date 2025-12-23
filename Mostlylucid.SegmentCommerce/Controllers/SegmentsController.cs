using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Services.Segments;

namespace Mostlylucid.SegmentCommerce.Controllers;

/// <summary>
/// Controller for segment visualization page and API endpoints.
/// </summary>
public class SegmentsController : Controller
{
    private readonly ISegmentService _segmentService;
    private readonly ISegmentVisualizationService _visualizationService;
    private readonly IDemoUserService _demoUserService;

    public SegmentsController(
        ISegmentService segmentService,
        ISegmentVisualizationService visualizationService,
        IDemoUserService demoUserService)
    {
        _segmentService = segmentService;
        _visualizationService = visualizationService;
        _demoUserService = demoUserService;
    }

    #region Pages

    /// <summary>
    /// Segment explorer page - visualize customer segments.
    /// </summary>
    [HttpGet("/segments")]
    [HttpGet("/segments/explorer")]
    public IActionResult Explorer()
    {
        return View();
    }

    #endregion

    #region API Endpoints

    /// <summary>
    /// Get all segment definitions.
    /// </summary>
    [HttpGet("/api/segments")]
    public IActionResult GetSegments()
    {
        var segments = _segmentService.GetSegments();
        return Ok(segments.Select(s => new
        {
            s.Id,
            s.Name,
            s.Description,
            s.Icon,
            s.Color,
            s.MembershipThreshold,
            s.Tags,
            RuleCount = s.Rules.Count
        }));
    }

    /// <summary>
    /// Get segment details by ID.
    /// </summary>
    [HttpGet("/api/segments/{id}")]
    public IActionResult GetSegment(string id)
    {
        var segment = _segmentService.GetSegment(id);
        if (segment == null) return NotFound();
        return Ok(segment);
    }

    /// <summary>
    /// Get visualization data for the segment explorer.
    /// Returns 2D positions for all profiles colored by segment.
    /// </summary>
    [HttpGet("/api/segments/visualization")]
    public async Task<IActionResult> GetVisualization([FromQuery] int? limit = 500)
    {
        var data = await _visualizationService.GetVisualizationDataAsync(limit);
        return Ok(data);
    }

    /// <summary>
    /// Get detailed segment analysis for a specific profile.
    /// </summary>
    [HttpGet("/api/segments/profile/{profileId:guid}")]
    public async Task<IActionResult> GetProfileDetail(Guid profileId)
    {
        var detail = await _visualizationService.GetProfileDetailAsync(profileId);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    /// <summary>
    /// Get list of demo users available for "login as" functionality.
    /// </summary>
    [HttpGet("/api/segments/demo-users")]
    public async Task<IActionResult> GetDemoUsers([FromQuery] int count = 10)
    {
        var users = await _demoUserService.GetDemoUsersAsync(count);
        return Ok(users);
    }

    /// <summary>
    /// Get demo users filtered by segment.
    /// </summary>
    [HttpGet("/api/segments/demo-users/by-segment/{segmentId}")]
    public async Task<IActionResult> GetDemoUsersBySegment(string segmentId, [FromQuery] int count = 5)
    {
        var users = await _demoUserService.GetDemoUsersBySegmentAsync(segmentId, count);
        return Ok(users);
    }

    /// <summary>
    /// "Login" as a demo user - sets session to simulate this profile.
    /// </summary>
    [HttpPost("/api/segments/demo-users/{profileId:guid}/login")]
    public async Task<IActionResult> LoginAsDemoUser(Guid profileId)
    {
        var result = await _demoUserService.LoginAsDemoUserAsync(profileId, HttpContext);
        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new
        {
            message = "Now browsing as demo user",
            profile = result.Profile,
            segments = result.Segments
        });
    }

    /// <summary>
    /// Logout from demo user mode.
    /// </summary>
    [HttpPost("/api/segments/demo-users/logout")]
    public IActionResult LogoutDemoUser()
    {
        _demoUserService.LogoutDemoUser(HttpContext);
        return Ok(new { message = "Logged out of demo mode" });
    }

    /// <summary>
    /// Get current demo user if logged in.
    /// </summary>
    [HttpGet("/api/segments/demo-users/current")]
    public async Task<IActionResult> GetCurrentDemoUser()
    {
        var current = await _demoUserService.GetCurrentDemoUserAsync(HttpContext);
        if (current == null)
            return Ok(new { loggedIn = false });

        return Ok(new
        {
            loggedIn = true,
            profile = current.Profile,
            segments = current.Segments
        });
    }

    #endregion
}
