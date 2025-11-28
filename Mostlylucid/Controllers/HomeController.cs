using System.Diagnostics;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Models;
using mostlylucid.pagingtaghelper.Helpers;
using Mostlylucid.Services;
using Mostlylucidblog.Models;
using MarkdownBaseService = Mostlylucid.Services.Markdown.MarkdownBaseService;

namespace Mostlylucid.Controllers;

public class HomeController(BaseControllerService baseControllerService, ILogger<HomeController> logger)
    : BaseController(baseControllerService, logger)
{
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request", "pagerequest", "homerequest", "Cookie" },
        VaryByQueryKeys = new[] { "page", "pageSize", "startDate", "endDate", "language", "orderBy", "orderDir", "category" })]
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, DateTime? startDate = null, DateTime? endDate = null,
        string language = MarkdownBaseService.EnglishLanguage, string orderBy = "date", string orderDir = "desc",
        string? category = null, [FromHeader] bool homerequest = false)
    {
        var authenticateResult = await GetUserInfo();

        // Use category filter if specified, otherwise use date/order filters
        var posts = !string.IsNullOrEmpty(category)
            ? await BlogViewService.GetPostsByCategory(category, page, pageSize, language)
            : await BlogViewService.GetPagedPosts(page, pageSize, language: language, startDate: startDate, endDate: endDate, orderBy, orderDir);

        posts.LinkUrl = Url.Action("Index", "Home", new { startDate, endDate, language, orderBy, orderDir, category });

        var indexPageViewModel = new IndexPageViewModel
        {
            Posts = posts,
            Authenticated = authenticateResult.LoggedIn,
            Name = authenticateResult.Name,
            AvatarUrl = authenticateResult.AvatarUrl
        };

        // For paging requests (has pagerequest header)
        if (Request.IsPageRequest())
            return PartialView("_BlogSummaryList", posts);

        // For home button click (has homerequest header)
        if (homerequest)
            return PartialView("_HomePartial", indexPageViewModel);

        // Full page load
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