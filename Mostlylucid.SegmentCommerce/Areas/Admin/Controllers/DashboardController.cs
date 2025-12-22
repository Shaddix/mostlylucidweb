using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mostlylucid.SegmentCommerce.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOnly")] 
public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
