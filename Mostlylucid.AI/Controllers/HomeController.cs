using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;

namespace Mostlylucid.AI.Controllers;

public class HomeController : AIBaseController
{
    public HomeController(AIBaseControllerService baseControllerService, ILogger<HomeController> logger)
        : base(baseControllerService, logger)
    {
    }

    public async Task<IActionResult> Index()
    {
        var recentArticles = await AIArticleService.GetArticlesAsync(page: 1, pageSize: 3);

        var model = new HomeViewModel
        {
            RecentArticles = recentArticles.Data
        };

        PopulateAnalytics(model);

        if (Request.IsHtmx())
        {
            return PartialView(model);
        }

        ViewBag.Title = "AI Consultancy";
        ViewBag.Description = "Practical AI solutions for businesses. Specializing in RAG systems, LLM integration, document intelligence, and semantic search.";

        return View(model);
    }
}
