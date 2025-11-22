using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Services.BrokenLinks;

namespace Mostlylucid.Controllers;

/// <summary>
/// API controller for broken link mappings used by client-side JavaScript
/// </summary>
[Route("api/brokenlinks")]
[ApiController]
public class BrokenLinksController : ControllerBase
{
    private readonly IBrokenLinkService _brokenLinkService;
    private readonly ILogger<BrokenLinksController> _logger;

    public BrokenLinksController(IBrokenLinkService brokenLinkService, ILogger<BrokenLinksController> logger)
    {
        _brokenLinkService = brokenLinkService;
        _logger = logger;
    }

    /// <summary>
    /// Get all broken link to archive URL mappings for client-side link replacement
    /// </summary>
    [HttpGet("mappings")]
    [OutputCache(Duration = 300)] // Cache for 5 minutes
    public async Task<IActionResult> GetMappings(CancellationToken cancellationToken)
    {
        try
        {
            var mappings = await _brokenLinkService.GetBrokenLinkMappingsAsync(cancellationToken);
            return Ok(mappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching broken link mappings");
            return StatusCode(500, "Error fetching broken link mappings");
        }
    }
}
