using Microsoft.AspNetCore.Mvc;

namespace Mostlylucid.Workflow.Demo.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Workflow");
    }
}
