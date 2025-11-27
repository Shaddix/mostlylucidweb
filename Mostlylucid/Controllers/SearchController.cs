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
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request", "pagerequest" }, VaryByQueryKeys = new[] { "query", "page", "pageSize", "language", "dateRange", "startDate", "endDate" })]
    public async Task<IActionResult> Search(
        string? query,
        int page = 1,
        int pageSize = 10,
        string? language = null,
        DateRangeOption dateRange = DateRangeOption.AllTime,
        DateTime? startDate = null,
        DateTime? endDate = null,
        [FromHeader] bool pagerequest = false)
    {
        // Calculate date range based on option
        var (calculatedStartDate, calculatedEndDate) = CalculateDateRange(dateRange, startDate, endDate);

        // Get available languages for the filter dropdown
        var availableLanguages = await searchService.GetAvailableLanguagesAsync();

        if (string.IsNullOrEmpty(query?.Trim()))
        {
            var emptyModel = new SearchResultsModel
            {
                Query = query,
                SearchResults = new(),
                AvailableLanguages = availableLanguages,
                Filters = new SearchFilters
                {
                    Language = language,
                    DateRange = dateRange,
                    StartDate = calculatedStartDate,
                    EndDate = calculatedEndDate
                }
            };
            if (Request.IsHtmx()) return PartialView("SearchResults", emptyModel);
            return View("SearchResults", emptyModel);
        }

        var searchResults = await searchService.HybridSearchWithPagingAsync(
            query,
            language,
            calculatedStartDate,
            calculatedEndDate,
            page,
            pageSize);

        var searchModel = new SearchResultsModel
        {
            Query = query,
            SearchResults = searchResults.ToPostListViewModel(),
            AvailableLanguages = availableLanguages,
            Filters = new SearchFilters
            {
                Language = language,
                DateRange = dateRange,
                StartDate = calculatedStartDate,
                EndDate = calculatedEndDate
            }
        };
        searchModel = await PopulateBaseModel(searchModel);

        // Build link URL with current filters
        var linkUrl = Url.Action("Search", "Search", new { query, language, dateRange, startDate, endDate });
        searchModel.SearchResults.LinkUrl = linkUrl;

        if (pagerequest && Request.IsHtmx()) return PartialView("_SearchResultsPartial", searchModel.SearchResults);

        if (Request.IsHtmx()) return PartialView("SearchResults", searchModel);
        return View("SearchResults", searchModel);
    }

    private static (DateTime? StartDate, DateTime? EndDate) CalculateDateRange(DateRangeOption dateRange, DateTime? startDate, DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        return dateRange switch
        {
            DateRangeOption.LastWeek => (now.AddDays(-7), now),
            DateRangeOption.LastMonth => (now.AddMonths(-1), now),
            DateRangeOption.LastYear => (now.AddYears(-1), now),
            DateRangeOption.Custom => (startDate, endDate),
            _ => (null, null) // AllTime - no date filter
        };
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