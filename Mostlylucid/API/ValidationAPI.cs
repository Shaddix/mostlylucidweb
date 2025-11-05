using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Blog.ValidationService;
using Mostlylucid.Controllers;
using Mostlylucid.Services;

namespace Mostlylucid.API;

[Route("api/[controller]")]
[ApiController]
public class ValidationController(
    BlogValidationService validationService,
    BaseControllerService baseControllerService,
    ILogger<ValidationController> logger) : BaseController(baseControllerService, logger)
{
    /// <summary>
    /// Validates blog content integrity
    /// Checks for orphaned entries, missing files, broken links, etc.
    /// </summary>
    [HttpGet("blog")]
    public async Task<ActionResult<ValidationResult>> ValidateBlog()
    {
        var user = await GetUserInfo();
        if (!user.IsAdmin)
        {
            return Forbid();
        }

        var result = await validationService.ValidateAsync();

        if (!result.IsValid)
        {
            return Ok(result); // Return 200 but with IsValid = false
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets validation report as plain text
    /// </summary>
    [HttpGet("blog/report")]
    public async Task<ActionResult<string>> GetValidationReport()
    {
        var user = await GetUserInfo();
        if (!user.IsAdmin)
        {
            return Forbid();
        }

        var result = await validationService.ValidateAsync();
        return Content(result.ToString(), "text/plain");
    }
}
