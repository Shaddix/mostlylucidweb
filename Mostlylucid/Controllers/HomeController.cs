using System.Diagnostics;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Models;
using Mostlylucid.Models.Blog;
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

        // Use the same index logic as BlogController
        var request = new BlogIndexRequest(page, pageSize, startDate, endDate, language, orderBy, orderDir, null, category);
        var result = await BlogViewService.GetIndexDataAsync(request);

        result.Posts.LinkUrl = Url.Action("Index", "Home", new { startDate, endDate, language, orderBy, orderDir, category });

        var indexPageViewModel = new IndexPageViewModel
        {
            Posts = result.Posts,
            Authenticated = authenticateResult.LoggedIn,
            Name = authenticateResult.Name,
            AvatarUrl = authenticateResult.AvatarUrl
        };

        // For paging requests (has pagerequest header)
        if (Request.IsPageRequest())
            return PartialView("_BlogSummaryList", result.Posts);

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