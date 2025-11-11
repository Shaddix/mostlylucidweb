using Microsoft.AspNetCore.Mvc;

namespace Mostlylucid.SecureChat.Demo.Controllers;

public class DemoController : Controller
{
    private readonly ILogger<DemoController> _logger;

    public DemoController(ILogger<DemoController> logger)
    {
        _logger = logger;
    }

    // Boring company website - the "public face"
    public IActionResult Company()
    {
        return View();
    }

    // Support staff interface
    public IActionResult Support()
    {
        return View();
    }

    // 404 page that also has the trigger
    [Route("error/404")]
    public IActionResult NotFound()
    {
        Response.StatusCode = 404;
        return View("Company"); // Use same page with trigger
    }

    public IActionResult Error()
    {
        return View();
    }
}
