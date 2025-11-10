using System.Diagnostics;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Models;
using Mostlylucid.Services;
using Mostlylucidblog.Models;
using MarkdownBaseService = Mostlylucid.Services.Markdown.MarkdownBaseService;

namespace Mostlylucid.Controllers;

public class HomeController(BaseControllerService baseControllerService, ILogger<HomeController> logger)
    : BaseController(baseControllerService, logger)
{
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request", "pagerequest" },
        VaryByQueryKeys = new[] { "page", "pageSize", "startDate", "endDate", "language", "orderBy", "orderDir" })]
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, DateTime? startDate = null, DateTime? endDate = null,
        string language = MarkdownBaseService.EnglishLanguage, string orderBy = "date", string orderDir = "desc", [FromHeader] bool pagerequest = false)
    {
        var authenticateResult = await GetUserInfo();
        var posts = await BlogViewService.GetPagedPosts(page, pageSize, language: language, startDate: startDate, endDate: endDate,orderBy, orderDir);

        // Apply ordering on the result set
        if (posts?.Data != null)
        {
            bool asc = string.Equals(orderDir, "asc", StringComparison.OrdinalIgnoreCase);
            switch ((orderBy ?? "date").ToLowerInvariant())
            {
                case "title":
                    posts.Data = (asc ? posts.Data.OrderBy(p => p.Title) : posts.Data.OrderByDescending(p => p.Title)).ToList();
                    break;
                case "date":
                default:
                    posts.Data = (asc ? posts.Data.OrderBy(p => p.PublishedDate) : posts.Data.OrderByDescending(p => p.PublishedDate)).ToList();
                    break;
            }
        }

        posts.LinkUrl = Url.Action("Index", "Home", new { startDate, endDate, language, orderBy, orderDir });
        if (pagerequest && Request.IsHtmx()) return PartialView("_BlogSummaryList", posts);
        var indexPageViewModel = new IndexPageViewModel
        {
            Posts = posts, Authenticated = authenticateResult.LoggedIn, Name = authenticateResult.Name,
            AvatarUrl = authenticateResult.AvatarUrl
        };
        if (Request.IsHtmx())
        {
           return PartialView("_HomePartial", indexPageViewModel);
        }
        return View(indexPageViewModel);
    }



    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet("typeahead")]
    public IActionResult TypeAhead()
    {
        return PartialView("_TypeAhead");
    }
    
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [HttpGet]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}