using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

/// <summary>
/// API controller for demo user management.
/// Returns demo users for the tracking mode dropdown and handles login/logout.
/// </summary>
[Route("api/demo-users")]
public class DemoUsersController : Controller
{
    private readonly IDemoPersonaService _demoUserService;
    private readonly ILogger<DemoUsersController> _logger;

    public DemoUsersController(IDemoPersonaService demoUserService, ILogger<DemoUsersController> logger)
    {
        _demoUserService = demoUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all demo users for the dropdown.
    /// Returns HTML partial for HTMX.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDemoUsers()
    {
        var users = await _demoUserService.GetDemoUsersAsync();
        return PartialView("Partials/_DemoUserList", users);
    }

    /// <summary>
    /// Get demo users as JSON (for non-HTMX clients)
    /// </summary>
    [HttpGet("json")]
    public async Task<ActionResult<List<DemoUserDto>>> GetDemoUsersJson()
    {
        var users = await _demoUserService.GetDemoUsersAsync();
        return Ok(users);
    }

    /// <summary>
    /// Login as a demo user.
    /// Links current session to demo user's profile.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] DemoLoginRequest request)
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return BadRequest(new { error = "No session found" });
        }

        var result = await _demoUserService.LoginAsync(sessionKey, request.DemoUserId);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            success = true,
            user = new
            {
                id = result.DemoUser!.Id,
                name = result.DemoUser.Name,
                persona = result.DemoUser.Persona,
                avatarColor = result.DemoUser.AvatarColor
            },
            profileId = result.Profile!.Id
        });
    }

    /// <summary>
    /// Logout current demo user.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return BadRequest(new { error = "No session found" });
        }

        await _demoUserService.LogoutAsync(sessionKey);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Get current logged-in demo user (if any).
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrEmpty(sessionKey))
        {
            return Ok(new { loggedIn = false });
        }

        var user = await _demoUserService.GetCurrentDemoUserAsync(sessionKey);

        if (user == null)
        {
            return Ok(new { loggedIn = false });
        }

        return Ok(new
        {
            loggedIn = true,
            user = new
            {
                id = user.Id,
                name = user.Name,
                persona = user.Persona,
                avatarColor = user.AvatarColor,
                interests = user.Interests
            }
        });
    }

    /// <summary>
    /// Get session key from header, query, or HttpContext.Items.
    /// </summary>
    private string? GetSessionKey()
    {
        // Try X-Session-ID header first (from TrackingManager)
        if (Request.Headers.TryGetValue("X-Session-ID", out var headerValue) && 
            !string.IsNullOrEmpty(headerValue.ToString()))
        {
            return headerValue.ToString();
        }

        // Try _sid query parameter
        if (Request.Query.TryGetValue("_sid", out var queryValue) && 
            !string.IsNullOrEmpty(queryValue.ToString()))
        {
            return queryValue.ToString();
        }

        // Try HttpContext.Items (set by middleware)
        if (HttpContext.Items.TryGetValue("SessionKey", out var itemValue) && 
            itemValue is string sessionKey)
        {
            return sessionKey;
        }

        return null;
    }
}

/// <summary>
/// Request for demo user login.
/// </summary>
public record DemoLoginRequest(string DemoUserId);
