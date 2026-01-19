using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;

namespace Mostlylucid.AI.Controllers;

public class AboutController : AIBaseController
{
    public AboutController(AIBaseControllerService baseControllerService, ILogger<AboutController> logger)
        : base(baseControllerService, logger)
    {
    }

    public IActionResult Index()
    {
        var model = new AIBaseViewModel();
        PopulateAnalytics(model);

        if (Request.IsHtmx())
        {
            return PartialView(model);
        }

        ViewBag.Title = "About";
        ViewBag.Description = "Scott Galloway - AI consultant with 15+ years enterprise experience, specializing in RAG systems, LLM integration, and practical AI solutions.";

        return View(model);
    }
}
