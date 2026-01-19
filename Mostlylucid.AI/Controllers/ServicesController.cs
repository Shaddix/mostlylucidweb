using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;

namespace Mostlylucid.AI.Controllers;

public class ServicesController : AIBaseController
{
    public ServicesController(AIBaseControllerService baseControllerService, ILogger<ServicesController> logger)
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

        ViewBag.Title = "Services";
        ViewBag.Description = "AI consultancy services including RAG development, local LLM setup, document intelligence, semantic search, and AI strategy.";

        return View(model);
    }
}
