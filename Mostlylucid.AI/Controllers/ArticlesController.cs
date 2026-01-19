using Htmx;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;

namespace Mostlylucid.AI.Controllers;

public class ArticlesController : AIBaseController
{
    public ArticlesController(AIBaseControllerService baseControllerService, ILogger<ArticlesController> logger)
        : base(baseControllerService, logger)
    {
    }

    public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
    {
        var model = await AIArticleService.GetArticlesAsync(page, pageSize);
        PopulateAnalytics(model);

        if (Request.IsHtmx())
        {
            return PartialView(model);
        }

        ViewBag.Title = "AI Articles";
        ViewBag.Description = "Articles about AI, machine learning, RAG systems, LLMs, and practical AI implementation.";

        return View(model);
    }

    [Route("articles/{slug}")]
    public async Task<IActionResult> Post(string slug, string? language = null)
    {
        var article = await AIArticleService.GetArticleBySlugAsync(slug, language);

        if (article == null)
        {
            // If not an AI article, redirect to the main blog
            return Redirect($"https://mostlylucid.net/{slug}");
        }

        var model = new ArticleViewModel
        {
            Title = article.Title,
            Slug = article.Slug,
            HtmlContent = article.HtmlContent,
            PublishedDate = article.PublishedDate,
            UpdatedDate = article.UpdatedDate?.DateTime,
            Categories = article.Categories,
            WordCount = article.WordCount,
            Language = article.Language,
            Languages = article.Languages
        };

        PopulateAnalytics(model);

        if (Request.IsHtmx())
        {
            return PartialView(model);
        }

        ViewBag.Title = article.Title;
        ViewBag.Description = article.PlainTextContent?.Length > 160
            ? article.PlainTextContent.Substring(0, 160) + "..."
            : article.PlainTextContent;

        return View(model);
    }
}
