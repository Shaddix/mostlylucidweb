using System.ComponentModel.DataAnnotations;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Mapper;
using Mostlylucid.Models.Blog;
using Mostlylucid.Models.Search;
using Mostlylucid.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Shared.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.Controllers;

[Route("search")]
public class SearchController(
    BaseControllerService baseControllerService,
    BlogSearchService searchService,
    ISemanticSearchService semanticSearchService,
    ILogger<SearchController> logger)
    : BaseController(baseControllerService, logger)
{
    [HttpGet]
    [Route("")]

    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request","pagerquest" },VaryByQueryKeys = new[] {"query", "page", "pageSize" })]
    public async Task<IActionResult> Search(string? query, int page = 1, int pageSize = 10,[FromHeader] bool pagerequest=false)
    {
        if(string.IsNullOrEmpty(query?.Trim()))
        {
            var emptyModel = new SearchResultsModel
            {
                Query = query,
                SearchResults = new ()
            };
            if (Request.IsHtmx()) return PartialView("SearchResults", emptyModel);
            return View("SearchResults", emptyModel);
        }
        var searchResults = await searchService.HybridSearchWithPagingAsync(query, page, pageSize);
 
        var searchModel = new SearchResultsModel
        {
            Query = query,
            SearchResults = searchResults.ToPostListViewModel()
        };
        searchModel = await PopulateBaseModel(searchModel);
        var linkUrl = Url.Action("Search", "Search");
        searchModel.SearchResults.LinkUrl = linkUrl;
        if(pagerequest && Request.IsHtmx()) return PartialView("_SearchResultsPartial", searchModel.SearchResults);
        
        if (Request.IsHtmx()) return PartialView("SearchResults", searchModel);
        return View("SearchResults", searchModel);
    }

    [HttpGet]
    [Route("{query}")]
    public  IActionResult InitialSearch([FromRoute] string query)
    {
        return RedirectToAction("Search", new { query });
    }

    [HttpGet]
    [Route("semantic")]
    [OutputCache(Duration = 3600, VaryByQueryKeys = new[] {"query", "limit"})]
    public async Task<IActionResult> SemanticSearch(string? query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be empty");
        }

        var results = await semanticSearchService.SearchAsync(query, limit);

        if (Request.IsHtmx())
        {
            return PartialView("_SemanticSearchResults", results);
        }

        return Json(results);
    }

    [HttpGet]
    [Route("related/{slug}/{language}")]
    [OutputCache(Duration = 7200, VaryByRouteValueNames = new[] {"slug", "language"})]
    public async Task<IActionResult> RelatedPosts(string slug, string language, int limit = 5)
    {
        var results = await semanticSearchService.GetRelatedPostsAsync(slug, language, limit);

        if (Request.IsHtmx())
        {
            return PartialView("_RelatedPosts", results);
        }

        return Json(results);
    }

}